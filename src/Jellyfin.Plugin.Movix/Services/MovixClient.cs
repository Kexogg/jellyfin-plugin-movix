using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Movix.Configuration;
using Jellyfin.Plugin.Movix.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Movix.Services;

public sealed class MovixClient(HttpClient httpClient, TimeProvider clock, ILogger<MovixClient> logger) : IMovixClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TokenResponse> GetDeviceTokenAsync(string deviceId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            GetEndpoints().DeviceToken(deviceId, clock.GetUtcNow().ToUnixTimeSeconds()),
            deviceId,
            null);
        return await SendJsonAsync<TokenResponse>(request, cancellationToken);
    }

    public async Task<TokenResponse> RefreshTokenAsync(string token, string deviceId,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, GetEndpoints().RefreshToken, deviceId, token);
        return await SendJsonAsync<TokenResponse>(request, cancellationToken);
    }

    public async Task<IReadOnlyList<MovixRegion>> GetRegionsAsync(string token, string deviceId,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, GetEndpoints().Regions, deviceId, token);
        var response = await SendJsonAsync<MovixRegionsResponse>(request, cancellationToken);
        if (response.Domains is null)
        {
            throw new MovixApiException("Movix returned no region list.");
        }

        return
        [
            .. response.Domains
                .Select(item => new MovixRegion(
                    item.ExternalId ?? string.Empty,
                    item.Code,
                    item.Title ?? string.Empty))
                .Where(item => item.ExtId.Length > 0 && item.Title.Length > 0),
        ];
    }

    public async Task<TokenResponse> LoginPasswordAsync(string token, string deviceId, string username, string password,
        string region, CancellationToken cancellationToken)
    {
        var endpoints = GetEndpoints();
        using var login = CreateRequest(HttpMethod.Post, endpoints.PasswordLogin, deviceId, token);
        login.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [MovixApiContract.Fields.Username] = username,
            [MovixApiContract.Fields.Password] = password,
            [MovixApiContract.Fields.Region] = region,
        });
        var loginResponse = await SendJsonAsync<MovixPasswordLoginResponse>(login, cancellationToken)
            ;
        var sso = loginResponse.Sso ??
                  throw new MovixApiException("Movix did not return an SSO key.");

        using var exchange = CreateRequest(HttpMethod.Get, endpoints.SubscriberTokenBySso(sso), deviceId, token);
        return await SendJsonAsync<TokenResponse>(exchange, cancellationToken);
    }

    public async Task<IReadOnlyList<SmsAgreement>> RequestSmsAsync(string token, string deviceId, string phone,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, GetEndpoints().AgreementList, deviceId, token);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [MovixApiContract.Fields.PhoneNumber] = phone,
        });
        var response = await SendJsonAsync<MovixAgreementListResponse>(request, cancellationToken)
            ;
        return response.Agreements is null
            ? throw new MovixApiException("Movix returned no agreement list.")
            : ParseAgreements(response.Agreements);
    }

    private static SmsAgreement[] ParseAgreements(
        IEnumerable<MovixAgreementResponse> agreements) =>
    [
        .. agreements
            .Select(item =>
            {
                var id = item.AgreementId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                var region = item.Domain ?? string.Empty;
                var number = item.AgreementNumber;
                var address = item.Address;
                var title = string.Join(" — ",
                    new[] { number, address }.Where(value => !string.IsNullOrWhiteSpace(value)));
                return new SmsAgreement(id, region, string.IsNullOrWhiteSpace(title) ? id : title);
            })
            .Where(item => item.AgreementId.Length > 0 && item.Region.Length > 0),
    ];

    public async Task<SmsAgreement> SendSmsAsync(string token, string deviceId, string phone, SmsAgreement agreement,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, GetEndpoints().SmsRequest, deviceId, token);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [MovixApiContract.Fields.Phone] = phone,
            [MovixApiContract.Fields.AgreementId] = agreement.AgreementId,
            [MovixApiContract.Fields.Region] = agreement.Region,
        });
        var response = await SendJsonAsync<MovixSmsResponse>(request, cancellationToken);
        if (response.Agreements?.WasSent != true)
        {
            throw new MovixApiException(response.Agreements?.Error ?? response.Error ??
                "Movix did not send an SMS code.");
        }

        var agreementId = response.Agreements.Agreements?.FirstOrDefault()?.AgreementId ??
                          throw new MovixApiException("Movix sent the SMS but returned no agreement identifier.");

        return agreement with { AgreementId = agreementId.ToString(CultureInfo.InvariantCulture) };
    }

    public async Task<TokenResponse> ConfirmSmsAsync(string token, string deviceId, string phone,
        SmsAgreement agreement, string code, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, GetEndpoints().SmsConfirm, deviceId, token);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [MovixApiContract.Fields.Phone] = phone,
            [MovixApiContract.Fields.Region] = agreement.Region,
            [MovixApiContract.Fields.AgreementId] = agreement.AgreementId,
            [MovixApiContract.Fields.SmsCode] = code,
        });
        return await SendJsonAsync<TokenResponse>(request, cancellationToken);
    }

    public async Task<bool> IsBoundAsync(string token, string deviceId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, GetEndpoints().MultiscreenStatus, deviceId, token);
        var response = await SendJsonAsync<MovixBindingStatusResponse>(request, cancellationToken)
            ;
        return response.Status?.IsBound == true;
    }

    public async Task BindDeviceAsync(string token, string deviceId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, GetEndpoints().BindDevice, deviceId, token);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        { [MovixApiContract.Fields.Title] = MovixApiContract.BoundDeviceTitle });
        await SendWithoutResultAsync(request, cancellationToken);
    }

    public async Task UnbindOwnDeviceAsync(string token, string deviceId, CancellationToken cancellationToken)
    {
        var endpoints = GetEndpoints();
        using var listRequest = CreateRequest(HttpMethod.Get, endpoints.Devices, deviceId, token);
        var response = await SendJsonAsync<MovixDevicesResponse>(listRequest, cancellationToken);

        var ownDevice = response.Devices?.FirstOrDefault(item =>
            string.Equals(item.ExternalId, deviceId, StringComparison.Ordinal));
        if (ownDevice is null)
        {
            return;
        }

        using var unbind = CreateRequest(HttpMethod.Post, endpoints.UnbindDevice, deviceId, token);
        unbind.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [MovixApiContract.Fields.DeviceId] = ownDevice.Id.ToString(CultureInfo.InvariantCulture),
        });
        await SendWithoutResultAsync(unbind, cancellationToken);
    }

    public async Task<IReadOnlyList<MovixChannel>> GetChannelsAsync(string token, string deviceId, bool includeAdult,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, GetEndpoints().Channels, deviceId, token);
        var response = await SendJsonAsync<MovixChannelsResponse>(request, cancellationToken);
        if (response.Channels is null)
        {
            throw new MovixApiException("Movix returned no channel collection.");
        }

        var result = new List<MovixChannel>();
        foreach (var item in response.Channels)
        {
            var name = item.Title;
            if (item.Id <= 0 || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (item.IsAdult && !includeAdult)
            {
                continue;
            }

            var hls = 0L;
            var poster = 0L;
            if (item.Resources is not null)
            {
                foreach (var resource in item.Resources)
                {
                    if (string.Equals(resource.Category, MovixApiContract.HlsResourceCategory,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        hls = resource.Id;
                    }
                    else if (string.Equals(resource.Category, MovixApiContract.ChannelLogoResourceCategory,
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        poster = resource.Id;
                    }
                }
            }

            if (hls <= 0)
            {
                continue;
            }

            result.Add(new MovixChannel(
                item.Id,
                name,
                item.Description,
                item.ChannelNumber.ToString(CultureInfo.InvariantCulture),
                hls,
                poster <= 0 ? null : BuildContentUrl(poster),
                item.IsAdult,
                item.EpgChannelId));
        }

        return result;
    }

    public async Task<Uri> GetStreamUrlAsync(string token, string deviceId, MovixChannel channel,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get,
            GetEndpoints().Stream(channel.Id, channel.HlsResourceId), deviceId, token);
        var response = await SendJsonAsync<MovixStreamResponse>(request, cancellationToken);
        if (!Uri.TryCreate(response.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new MovixApiException("Movix returned an invalid stream URL.");
        }

        return uri;
    }

    public async Task<IReadOnlyList<MovixProgram>> GetProgramsAsync(string token, string deviceId,
        IReadOnlyList<MovixChannel> channels, DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var epgChannels = channels.Where(channel => channel.EpgChannelId > 0).ToArray();
        if (epgChannels.Length == 0)
        {
            return [];
        }

        using var request = CreateRequest(HttpMethod.Get, GetEndpoints().EpgSchedule(from, to), deviceId, token);
        var response = await SendJsonAsync<MovixEpgResponse>(request, cancellationToken,
                "The official Movix EPG endpoint returned an empty response.")
            ;
        if (response.Channels is null)
        {
            throw new MovixApiException("Movix returned no EPG channel collection.");
        }

        var channelMap = epgChannels
            .GroupBy(channel => channel.EpgChannelId)
            .ToDictionary(group => group.Key, group => group.First().Id);
        var result = new List<MovixProgram>();
        foreach (var epgChannel in response.Channels)
        {
            if (epgChannel.EpgChannelId <= 0 || epgChannel.Schedule is null)
            {
                continue;
            }

            foreach (var item in epgChannel.Schedule)
            {
                var title = item.Title;
                var epgChannelId = item.ChannelId > 0 ? item.ChannelId : epgChannel.EpgChannelId;
                if (!channelMap.TryGetValue(epgChannelId, out var channelId) ||
                    string.IsNullOrWhiteSpace(title) ||
                    item.Start <= 0 ||
                    item.Duration <= 0)
                {
                    continue;
                }

                var start = DateTimeOffset.FromUnixTimeSeconds(item.Start);
                result.Add(new MovixProgram(item.Id.ToString(CultureInfo.InvariantCulture), channelId, title,
                    null, start, start.AddSeconds(item.Duration), null));
            }
        }

        return result;
    }

    private static PluginConfiguration GetConfiguration() => Plugin.Instance?.Configuration ??
                                                             throw new InvalidOperationException(
                                                                 "Movix plugin is not initialized.");

    private static string BuildContentUrl(long resourceId) =>
        MovixApiContract.ContentResourceUrl(GetConfiguration().ContentBaseUrl, resourceId);

    private static MovixApiContract.Endpoints GetEndpoints() => new(GetConfiguration().ApiBaseUrl);

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string deviceId, string? token)
    {
        var config = GetConfiguration();
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation(MovixApiContract.Headers.DeviceInfo, deviceId);
        request.Headers.TryAddWithoutValidation(MovixApiContract.Headers.View, MovixApiContract.View);
        request.Headers.TryAddWithoutValidation(MovixApiContract.Headers.AppVersion, config.ClientVersion);
        request.Headers.UserAgent.ParseAdd($"{MovixApiContract.ApplicationId}/{config.ClientVersion}");
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.TryAddWithoutValidation(MovixApiContract.Headers.AuthToken, token);
        }

        return request;
    }

    private async Task<T> SendJsonAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken,
        string? emptyResponseMessage = null)
    {
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
        {
            throw new MovixApiException(emptyResponseMessage ?? "Movix returned an empty response.",
                response.StatusCode);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new MovixApiException("Movix returned an anti-bot HTML page instead of JSON.", response.StatusCode);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                   ?? throw new MovixApiException(emptyResponseMessage ?? "Movix returned an empty response.",
                       response.StatusCode);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new MovixApiException("Movix returned an incompatible JSON response.", response.StatusCode, ex);
        }
    }

    private async Task SendWithoutResultAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode;
                response.Dispose();
                throw new MovixApiException($"Movix request failed with HTTP {(int)status}.", status);
            }

            return response;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Movix request to {Host} failed with HTTP transport error", request.RequestUri?.Host);
            throw new MovixApiException("The Movix service could not be reached.", ex.StatusCode, ex);
        }
    }
}
