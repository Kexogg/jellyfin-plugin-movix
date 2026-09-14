using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Jellyfin.Plugin.Movix.Models;
using MediaBrowser.Controller;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Movix.Services;

public sealed class MovixGatewayService(
    IMovixClient client,
    MovixSessionManager sessions,
    IServerApplicationHost applicationHost,
    TimeProvider clock,
    ILogger<MovixGatewayService> logger)
{
    private static readonly TimeSpan ChannelCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan EpgCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan EpgStaleDuration = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private readonly SemaphoreSlim _epgLock = new(1, 1);
    private ChannelSnapshot? _channels;
    private EpgSnapshot? _epg;

    public int SupportedChannels => _channels?.Channels.Count ?? 0;

    public string? LastError { get; private set; }

    public string GetInternalBaseUrl() => applicationHost.GetLocalApiUrl("127.0.0.1", "http", applicationHost.HttpPort).TrimEnd('/');

    public async Task<IReadOnlyList<MovixChannel>> GetChannelsAsync(bool force, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Movix plugin is not initialized.");
        if (!force && _channels is not null &&
            _channels.IncludesAdultChannels == config.IncludeAdultChannels &&
            clock.GetUtcNow() - _channels.LoadedAt < ChannelCacheDuration)
        {
            return _channels.Channels;
        }

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            if (!force && _channels is not null &&
                _channels.IncludesAdultChannels == config.IncludeAdultChannels &&
                clock.GetUtcNow() - _channels.LoadedAt < ChannelCacheDuration)
            {
                return _channels.Channels;
            }

            var channels = await sessions.ExecuteAuthenticatedAsync(
                (session, token) => client.GetChannelsAsync(session.Token, session.DeviceId, config.IncludeAdultChannels, token),
                cancellationToken);
            _channels = new ChannelSnapshot(channels, clock.GetUtcNow(), config.IncludeAdultChannels);
            LastError = null;
            return channels;
        }
        catch (MovixApiException ex)
        {
            LastError = ex.Message;
            if (_channels is not null && _channels.IncludesAdultChannels == config.IncludeAdultChannels)
            {
                logger.LogWarning(ex, "Using stale Movix channel list");
                return _channels.Channels;
            }

            throw;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async Task<string> GetPlaylistAsync(CancellationToken cancellationToken)
    {
        var channels = await GetChannelsAsync(false, cancellationToken);
        var baseUrl = GetInternalBaseUrl();
        var builder = new StringBuilder("#EXTM3U\n");
        foreach (var channel in channels.OrderBy(ChannelOrder).ThenBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("#EXTINF:-1 tvg-id=\"").Append(EscapeM3u($"movix-{channel.Id}"))
                .Append("\" tvg-name=\"").Append(EscapeM3u(channel.Name)).Append('"');
            if (!string.IsNullOrWhiteSpace(channel.Number))
            {
                builder.Append(" tvg-chno=\"").Append(EscapeM3u(channel.Number)).Append('"');
            }

            if (!string.IsNullOrWhiteSpace(channel.LogoUrl))
            {
                builder.Append(" tvg-logo=\"").Append(EscapeM3u(channel.LogoUrl)).Append('"');
            }

            builder.Append(" group-title=\"Movix\",").Append(channel.Name.Replace('\n', ' ')).Append('\n')
                .Append(MovixApiContract.PluginRoutes.StreamUrl(baseUrl, channel.Id)).Append('\n');
        }

        return builder.ToString();
    }

    public async Task<Uri> ResolveStreamAsync(long channelId, CancellationToken cancellationToken)
    {
        var channel = (await GetChannelsAsync(false, cancellationToken)).FirstOrDefault(item => item.Id == channelId);
        channel ??= (await GetChannelsAsync(true, cancellationToken)).FirstOrDefault(item => item.Id == channelId);

        if (channel is null)
        {
            throw new FileNotFoundException("The Movix channel is unavailable.");
        }

        return await sessions.ExecuteAuthenticatedAsync(
            (session, token) => client.GetStreamUrlAsync(session.Token, session.DeviceId, channel, token),
            cancellationToken);
    }

    public async Task<string> GetEpgAsync(bool force, CancellationToken cancellationToken)
    {
        if (!force && _epg is not null && clock.GetUtcNow() - _epg.LoadedAt < EpgCacheDuration)
        {
            return WriteXmlTv(await GetChannelsAsync(false, cancellationToken), _epg.Programs);
        }

        await _epgLock.WaitAsync(cancellationToken);
        try
        {
            if (!force && _epg is not null && clock.GetUtcNow() - _epg.LoadedAt < EpgCacheDuration)
            {
                return WriteXmlTv(await GetChannelsAsync(false, cancellationToken), _epg.Programs);
            }

            var channels = await GetChannelsAsync(false, cancellationToken);
            try
            {
                var now = clock.GetUtcNow();
                var programs = await sessions.ExecuteAuthenticatedAsync(
                    (session, token) => client.GetProgramsAsync(session.Token, session.DeviceId, channels, now.AddHours(-6), now.AddHours(72), token),
                    cancellationToken);
                _epg = new EpgSnapshot(programs, now);
                LastError = null;
            }
            catch (MovixApiException ex) when (_epg is not null && clock.GetUtcNow() - _epg.LoadedAt < EpgStaleDuration)
            {
                LastError = ex.Message;
                logger.LogWarning(ex, "Using stale Movix EPG");
            }

            if (_epg is null)
            {
                throw new MovixApiException("Movix EPG is unavailable and no cached guide exists.");
            }

            return WriteXmlTv(channels, _epg.Programs);
        }
        finally
        {
            _epgLock.Release();
        }
    }

    public void Invalidate()
    {
        _channels = null;
        _epg = null;
    }

    internal static string WriteXmlTv(IReadOnlyList<MovixChannel> channels, IReadOnlyList<MovixProgram> programs)
    {
        var builder = new StringBuilder();
        using var textWriter = new Utf8StringWriter(builder);
        using var writer = XmlWriter.Create(textWriter, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false });
        writer.WriteStartDocument();
        writer.WriteStartElement("tv");
        writer.WriteAttributeString("generator-info-name", "Movix for Jellyfin");
        foreach (var channel in channels)
        {
            writer.WriteStartElement("channel");
            writer.WriteAttributeString("id", $"movix-{channel.Id}");
            writer.WriteElementString("display-name", channel.Name);
            if (channel.LogoUrl is not null)
            {
                writer.WriteStartElement("icon");
                writer.WriteAttributeString("src", channel.LogoUrl);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        foreach (var program in programs.Where(program => channels.Any(channel => channel.Id == program.ChannelId)).OrderBy(program => program.Start))
        {
            writer.WriteStartElement("programme");
            writer.WriteAttributeString("channel", $"movix-{program.ChannelId}");
            writer.WriteAttributeString("start", FormatXmlTvDate(program.Start));
            writer.WriteAttributeString("stop", FormatXmlTvDate(program.End));
            writer.WriteElementString("title", program.Title);
            if (!string.IsNullOrWhiteSpace(program.Description))
            {
                writer.WriteElementString("desc", program.Description);
            }

            if (program.IconUrl is not null)
            {
                writer.WriteStartElement("icon");
                writer.WriteAttributeString("src", program.IconUrl);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();
        return builder.ToString();
    }

    private static string EscapeM3u(string value) => value.Replace("\\", @"\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');

    private static double ChannelOrder(MovixChannel channel) => double.TryParse(channel.Number, CultureInfo.InvariantCulture, out var number) ? number : double.MaxValue;

    private static string FormatXmlTvDate(DateTimeOffset value) => value.UtcDateTime.ToString("yyyyMMddHHmmss +0000", CultureInfo.InvariantCulture);

    private sealed class Utf8StringWriter(StringBuilder builder) : StringWriter(builder, CultureInfo.InvariantCulture)
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
