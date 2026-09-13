using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Movix.Models;

public sealed record MovixSession(string DeviceId, string Token, long Expires, bool IsAuthenticated, bool IsBound)
{
    public bool IsExpiring(TimeProvider clock, TimeSpan margin) =>
        DateTimeOffset.FromUnixTimeSeconds(Expires) <= clock.GetUtcNow().Add(margin);
}

public sealed class TokenResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("expires")]
    public long Expires { get; set; }

    [JsonPropertyName("is_bound")]
    public bool? IsBound { get; set; }
}

public sealed record MovixRegion(string ExtId, long Code, string Title);

public sealed record SmsAgreement(string AgreementId, string Region, string Title);

public sealed record SmsChallenge(string Phone, SmsAgreement Agreement, DateTimeOffset ExpiresAt);

public sealed record MovixChannel(
    long Id,
    string Name,
    string? Description,
    string? Number,
    long HlsResourceId,
    string? LogoUrl,
    bool IsAdult,
    long EpgChannelId);

public sealed record MovixProgram(string Id, long ChannelId, string Title, string? Description, DateTimeOffset Start, DateTimeOffset End, string? IconUrl);

public sealed record ChannelSnapshot(IReadOnlyList<MovixChannel> Channels, DateTimeOffset LoadedAt, bool IncludesAdultChannels);

public sealed record EpgSnapshot(IReadOnlyList<MovixProgram> Programs, DateTimeOffset LoadedAt);

/// <summary>Sanitized connection state returned to a Jellyfin administrator.</summary>
public sealed record MovixStatusDto(
    string State,
    bool IsAuthenticated,
    bool IsBound,
    DateTimeOffset? TokenExpiresAt,
    int SupportedChannels,
    bool TunerConfigured,
    bool GuideConfigured,
    string? LastError);
