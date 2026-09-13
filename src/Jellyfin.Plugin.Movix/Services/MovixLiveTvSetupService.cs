using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Movix.Configuration;
using Jellyfin.Plugin.Movix.Models;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Movix.Services;

public sealed class MovixLiveTvSetupService(
    MovixSessionManager sessions,
    MovixGatewayService gateway,
    ITunerHostManager tunerHostManager,
    IListingsManager listingsManager,
    ILogger<MovixLiveTvSetupService> logger)
{
    public async Task<MovixStatusDto> ConnectAsync(CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("Movix plugin is not initialized.");
        await sessions.EnsureBoundAsync(cancellationToken);
        gateway.Invalidate();
        await gateway.GetChannelsAsync(true, cancellationToken);

        var config = plugin.Configuration;
        var baseUrl = gateway.GetInternalBaseUrl();
        var tuner = await tunerHostManager.SaveTunerHost(new TunerHostInfo
        {
            Id = config.TunerId,
            Type = "m3u",
            FriendlyName = "Movix",
            Url = MovixApiContract.PluginRoutes.PlaylistUrl(baseUrl),
            TunerCount = config.SimultaneousStreams,
            AllowHWTranscoding = true,
            AllowFmp4TranscodingContainer = false,
            AllowStreamSharing = false,
            IgnoreDts = true,
        });
        config.TunerId = tuner.Id;

        try
        {
            var useExternalGuide = IsXmlTvUrl(config.EpgBaseUrl);
            var listingsPath = useExternalGuide
                ? config.EpgBaseUrl
                : MovixApiContract.PluginRoutes.EpgUrl(baseUrl);
            if (!useExternalGuide)
            {
                await gateway.GetEpgAsync(true, cancellationToken);
            }

            var listings = await listingsManager.SaveListingProvider(new ListingsProviderInfo
            {
                Id = config.ListingsId,
                Type = "xmltv",
                Path = listingsPath,
                EnableAllTuners = false,
                EnabledTuners = [tuner.Id],
                PreferredLanguage = "ru",
            }, false, false);
            config.ListingsId = listings.Id;
        }
        catch (MovixApiException ex)
        {
            logger.LogWarning(ex, "Movix connected without EPG");
        }

        plugin.UpdateConfiguration(config);
        return await GetStatusAsync(cancellationToken);
    }

    public async Task<MovixStatusDto> RefreshAsync(CancellationToken cancellationToken)
    {
        gateway.Invalidate();
        return await ConnectAsync(cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("Movix plugin is not initialized.");
        var config = plugin.Configuration;
        if (!string.IsNullOrWhiteSpace(config.ListingsId))
        {
            listingsManager.DeleteListingsProvider(config.ListingsId);
        }

        if (!string.IsNullOrWhiteSpace(config.TunerId))
        {
            tunerHostManager.DeleteTunerHost(config.TunerId);
        }

        config.ListingsId = null;
        config.TunerId = null;
        plugin.UpdateConfiguration(config);
        gateway.Invalidate();
        await sessions.DisconnectAsync(cancellationToken);
    }

    public async Task<MovixStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var session = await sessions.GetCurrentAsync(cancellationToken);
        var authenticated = session is { IsAuthenticated: true };
        var guideConfigured = !string.IsNullOrWhiteSpace(config.ListingsId);
        var tunerConfigured = !string.IsNullOrWhiteSpace(config.TunerId);
        var state = (authenticated, tunerConfigured, guideConfigured) switch
        {
            (false, _, _) => "NeedsLogin",
            (true, false, _) => "Authenticated",
            (true, true, false) => "ConnectedNoGuide",
            _ => "Connected",
        };
        return new MovixStatusDto(
            state,
            authenticated,
            session?.IsBound ?? false,
            session is null ? null : DateTimeOffset.FromUnixTimeSeconds(session.Expires),
            gateway.SupportedChannels,
            tunerConfigured,
            guideConfigured,
            gateway.LastError ?? sessions.LastError);
    }

    internal static bool IsXmlTvUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var path = uri.AbsolutePath;
        return path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".xml.gz", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
    }
}
