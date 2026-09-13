using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Movix.Services;

internal class MovixErrorResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("sms_error_text")]
    public string? SmsErrorText { get; set; }

    internal string? Error => Message ?? Reason ?? SmsErrorText;
}

internal sealed class MovixRegionsResponse
{
    [JsonPropertyName("domains")]
    public List<MovixRegionResponse>? Domains { get; set; }
}

internal sealed class MovixRegionResponse
{
    [JsonPropertyName("extid")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("code")]
    public long Code { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

internal sealed class MovixPasswordLoginResponse
{
    [JsonPropertyName("sso")]
    public string? Sso { get; set; }
}

internal sealed class MovixAgreementListResponse
{
    [JsonPropertyName("agreements")]
    public List<MovixAgreementResponse>? Agreements { get; set; }
}

internal sealed class MovixAgreementResponse
{
    [JsonPropertyName("agreement_id")]
    public int? AgreementId { get; set; }

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("agreement_number")]
    public string? AgreementNumber { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

internal sealed class MovixSmsResponse : MovixErrorResponse
{
    [JsonPropertyName("agreements")]
    public MovixSmsResultResponse? Agreements { get; set; }
}

internal sealed class MovixSmsResultResponse : MovixErrorResponse
{
    [JsonPropertyName("send_sms")]
    public int SendSms { get; set; }

    [JsonPropertyName("agreement")]
    public List<MovixSmsAgreementResponse>? Agreements { get; set; }

    internal bool WasSent => SendSms != 0;
}

internal sealed class MovixSmsAgreementResponse
{
    [JsonPropertyName("agr_id")]
    public int? AgreementId { get; set; }
}

internal sealed class MovixBindingStatusResponse
{
    [JsonPropertyName("status")]
    public MovixBindingStatus? Status { get; set; }
}

internal sealed class MovixBindingStatus
{
    [JsonPropertyName("is_bound")]
    public bool IsBound { get; set; }
}

internal sealed class MovixDevicesResponse
{
    [JsonPropertyName("devices")]
    public List<MovixDeviceResponse>? Devices { get; set; }
}

internal sealed class MovixDeviceResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("extid")]
    public string? ExternalId { get; set; }
}

internal sealed class MovixChannelsResponse
{
    [JsonPropertyName("collection")]
    public List<MovixChannelResponse>? Channels { get; set; }
}

internal sealed class MovixChannelResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("er_lcn")]
    public long ChannelNumber { get; set; }

    [JsonPropertyName("is_porno")]
    public bool IsAdult { get; set; }

    [JsonPropertyName("resources")]
    public List<MovixResourceResponse>? Resources { get; set; }

    [JsonPropertyName("epg_channel_id")]
    public long EpgChannelId { get; set; }
}

internal sealed class MovixResourceResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }
}

internal sealed class MovixStreamResponse
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal sealed class MovixEpgResponse
{
    [JsonPropertyName("channels")]
    public List<MovixEpgChannelResponse>? Channels { get; set; }
}

internal sealed class MovixEpgChannelResponse
{
    [JsonPropertyName("epg_channel_id")]
    public long EpgChannelId { get; set; }

    [JsonPropertyName("schedule")]
    public List<MovixProgramResponse>? Schedule { get; set; }
}

internal sealed class MovixProgramResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("channel_id")]
    public long ChannelId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("start")]
    public long Start { get; set; }

    [JsonPropertyName("duration")]
    public long Duration { get; set; }

}
