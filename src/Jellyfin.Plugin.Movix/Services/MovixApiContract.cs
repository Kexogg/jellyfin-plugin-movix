using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;

namespace Jellyfin.Plugin.Movix.Services;

/// <summary>Movix API paths and protocol constants.</summary>
internal static class MovixApiContract
{
    internal const string ApplicationId = "com.ertelecom.domrutv";
    internal const string ClientId = "er_android_device";
    internal const string SsoSystem = "er";
    internal const string View = "stb3";
    internal const string BoundDeviceTitle = "Jellyfin Movix";
    internal const string EpgSelection = "channel_id,id,start,duration,title,epg_channel_id";
    internal const string HlsResourceCategory = "hls";
    internal const string ChannelLogoResourceCategory = "newdesign_channel_logo_blueprint";

    internal static class Headers
    {
        internal const string AppVersion = "X-App-Version";
        internal const string AuthToken = "X-Auth-Token";
        internal const string DeviceInfo = "X-Device-Info";
        internal const string View = "View";
    }

    internal static class Fields
    {
        internal const string AgreementId = "agr_id";
        internal const string DeviceId = "device_id";
        internal const string Password = "password";
        internal const string Phone = "phone";
        internal const string PhoneNumber = "phone_number";
        internal const string Region = "region";
        internal const string SmsCode = "sms_code";
        internal const string Title = "title";
        internal const string Username = "username";
    }

    internal sealed class Endpoints(string baseUrl)
    {
        internal Uri DeviceToken(string deviceId, long timestamp) => Build(
            Paths.DeviceToken,
            new Dictionary<string, string?>
            {
                ["client_id"] = ClientId,
                ["device_id"] = deviceId,
                ["timestamp"] = timestamp.ToString(CultureInfo.InvariantCulture),
            });

        internal Uri RefreshToken => Build(Paths.RefreshToken);

        internal Uri Regions => Build(Paths.Regions);

        internal Uri PasswordLogin => Build(Paths.PasswordLogin);

        internal Uri SubscriberTokenBySso(string sso) => Build(
            Paths.SubscriberTokenBySso,
            new Dictionary<string, string?>
            {
                ["sso_system"] = SsoSystem,
                ["sso_key"] = sso,
            });

        internal Uri AgreementList => Build(Paths.AgreementList);

        internal Uri SmsRequest => Build(Paths.SmsRequest);

        internal Uri SmsConfirm => Build(Paths.SmsConfirm);

        internal Uri MultiscreenStatus => Build(Paths.MultiscreenStatus);

        internal Uri BindDevice => Build(Paths.BindDevice);

        internal Uri Devices => Build(Paths.Devices);

        internal Uri UnbindDevice => Build(Paths.UnbindDevice);

        internal Uri Channels => Build(Paths.Channels);

        internal Uri Stream(long assetId, long resourceId) => Build(string.Create(
            CultureInfo.InvariantCulture,
            $"/resource/get_url/{assetId}/{resourceId}"));

        internal Uri EpgSchedule(DateTimeOffset from, DateTimeOffset to) => Build(
            Paths.EpgSchedule,
            new Dictionary<string, string?>
            {
                ["select"] = EpgSelection,
                ["start_from"] = from.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ["start_to"] = to.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            });

        private Uri Build(string path, IDictionary<string, string?>? query = null)
        {
            var endpoint = new Uri($"{baseUrl.TrimEnd('/')}{path}", UriKind.Absolute);
            return query is null
                ? endpoint
                : new Uri(QueryHelpers.AddQueryString(endpoint.AbsoluteUri, query), UriKind.Absolute);
        }
    }

    internal static string ContentResourceUrl(string baseUrl, long resourceId) => string.Create(
        CultureInfo.InvariantCulture,
        $"{baseUrl.TrimEnd('/')}/r{resourceId}");

    private static class Paths
    {
        internal const string DeviceToken = "/token/device";
        internal const string RefreshToken = "/token/refresh";
        internal const string Regions = "/er/misc/domains";
        internal const string PasswordLogin = "/er/ssoauth/auth";
        internal const string SubscriberTokenBySso = "/token/subscriber_device/by_sso";
        internal const string AgreementList = "/er/misc/subscriber/agreement_list";
        internal const string SmsRequest = "/er/sms/auth";
        internal const string SmsConfirm = "/er/sms/check";
        internal const string MultiscreenStatus = "/er/multiscreen/status";
        internal const string BindDevice = "/er/multiscreen/device/bind";
        internal const string Devices = "/er/multiscreen/devices";
        internal const string UnbindDevice = "/er/multiscreen/device/unbind";
        internal const string Channels = "/channel_list/multiscreen";
        internal const string EpgSchedule = "/epg/get_schedule";
    }

    internal static class PluginRoutes
    {
        private const string StreamPrefix = "stream/";
        private const string StreamExtension = ".m3u8";

        internal const string IptvBase = "Movix/iptv";
        internal const string Playlist = "playlist.m3u";
        internal const string Epg = "epg.xml";
        internal const string Stream = StreamPrefix + "{channelId:long}" + StreamExtension;
        private const string PlaylistPath = "/" + IptvBase + "/" + Playlist;
        private const string EpgPath = "/" + IptvBase + "/" + Epg;

        internal static string PlaylistUrl(string baseUrl) => BuildUrl(baseUrl, PlaylistPath);

        internal static string EpgUrl(string baseUrl) => BuildUrl(baseUrl, EpgPath);

        internal static string StreamUrl(string baseUrl, long channelId) => BuildUrl(
            baseUrl,
            string.Create(
                CultureInfo.InvariantCulture,
                $"/{IptvBase}/{StreamPrefix}{channelId}{StreamExtension}"));

        private static string BuildUrl(string baseUrl, string path) => $"{baseUrl.TrimEnd('/')}{path}";
    }
}
