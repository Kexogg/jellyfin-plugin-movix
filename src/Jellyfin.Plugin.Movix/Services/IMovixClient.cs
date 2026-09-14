using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Movix.Models;

namespace Jellyfin.Plugin.Movix.Services;

public interface IMovixClient
{
    Task<TokenResponse> GetDeviceTokenAsync(string deviceId, CancellationToken cancellationToken);

    Task<TokenResponse> RefreshTokenAsync(string token, string deviceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MovixRegion>> GetRegionsAsync(string token, string deviceId, CancellationToken cancellationToken);

    Task<TokenResponse> LoginPasswordAsync(string token, string deviceId, string username, string password, string region, CancellationToken cancellationToken);

    Task<IReadOnlyList<SmsAgreement>> RequestSmsAsync(string token, string deviceId, string phone, CancellationToken cancellationToken);

    Task<SmsAgreement> SendSmsAsync(string token, string deviceId, string phone, SmsAgreement agreement, CancellationToken cancellationToken);

    Task<TokenResponse> ConfirmSmsAsync(string token, string deviceId, string phone, SmsAgreement agreement, string code, CancellationToken cancellationToken);

    Task<bool> IsBoundAsync(string token, string deviceId, CancellationToken cancellationToken);

    Task BindDeviceAsync(string token, string deviceId, CancellationToken cancellationToken);

    Task UnbindOwnDeviceAsync(string token, string deviceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MovixChannel>> GetChannelsAsync(string token, string deviceId, bool includeAdult, CancellationToken cancellationToken);

    Task<Uri> GetStreamUrlAsync(string token, string deviceId, MovixChannel channel, CancellationToken cancellationToken);

    Task<IReadOnlyList<MovixProgram>> GetProgramsAsync(string token, string deviceId, IReadOnlyList<MovixChannel> channels, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
