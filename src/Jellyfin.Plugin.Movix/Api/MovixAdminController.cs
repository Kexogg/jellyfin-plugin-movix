using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Movix.Models;
using Jellyfin.Plugin.Movix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Movix.Api;

/// <summary>Administrator-only Movix authentication and setup API.</summary>
[ApiController]
[Route("Movix/Admin")]
[Authorize(Policy = "RequiresElevation")]
public sealed class MovixAdminController(
    MovixSessionManager sessions,
    MovixLiveTvSetupService setup,
    TimeProvider clock) : ControllerBase
{
    [HttpGet("Status")]
    public Task<MovixStatusDto> GetStatus(CancellationToken cancellationToken) => setup.GetStatusAsync(cancellationToken);

    [HttpGet("Regions")]
    public async Task<IReadOnlyList<RegionDto>> GetRegions(CancellationToken cancellationToken) =>
        [.. (await sessions.GetRegionsAsync(cancellationToken)).Select(region => new RegionDto(region.ExtId, region.Code, region.Title))];

    [HttpPost("Login/Password")]
    public async Task<IActionResult> LoginPassword([FromBody] PasswordLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Region))
        {
            return BadRequest("Username, password, and region are required.");
        }

        await sessions.LoginPasswordAsync(request.Username, request.Password, request.Region, cancellationToken);
        return Ok(await setup.GetStatusAsync(cancellationToken));
    }

    [HttpPost("Login/Sms/Agreements")]
    public async Task<IReadOnlyList<SmsAgreementDto>> GetSmsAgreements([FromBody] SmsPhoneRequest request, CancellationToken cancellationToken)
    {
        var agreements = await sessions.GetSmsAgreementsAsync(request.Phone, cancellationToken);
        return [.. agreements.Select(agreement => new SmsAgreementDto(agreement.AgreementId, agreement.Region, agreement.Title))];
    }

    [HttpPost("Login/Sms/Request")]
    public async Task<SmsChallengeDto> RequestSms([FromBody] SmsStartRequest request, CancellationToken cancellationToken)
    {
        var id = await sessions.SendSmsAsync(request.Phone, new SmsAgreement(request.AgreementId, request.Region, request.AgreementId), cancellationToken);
        return new SmsChallengeDto(id);
    }

    [HttpPost("Login/Sms/Confirm")]
    public async Task<IActionResult> ConfirmSms([FromBody] SmsConfirmRequest request, CancellationToken cancellationToken)
    {
        await sessions.ConfirmSmsAsync(request.ChallengeId, request.Code, cancellationToken);
        return Ok(await setup.GetStatusAsync(cancellationToken));
    }

    [HttpPost("Connect")]
    public async Task<MovixStatusDto> Connect(CancellationToken cancellationToken) => await setup.ConnectAsync(cancellationToken);

    [HttpPost("Refresh")]
    public async Task<MovixStatusDto> Refresh(CancellationToken cancellationToken) => await setup.RefreshAsync(cancellationToken);

    [HttpPost("Disconnect")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        await setup.DisconnectAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("Diagnostics")]
    public async Task<object> Diagnostics(CancellationToken cancellationToken)
    {
        var status = await setup.GetStatusAsync(cancellationToken);
        var config = Plugin.Instance?.Configuration;
        return new
        {
            PluginVersion = Plugin.Instance?.Version?.ToString(),
            JellyfinTarget = "12.0.0",
            ApiHost = Uri.TryCreate(config?.ApiBaseUrl, UriKind.Absolute, out var api) ? api.Host : null,
            EpgMode = MovixLiveTvSetupService.IsXmlTvUrl(config?.EpgBaseUrl) ? "ExternalXmlTv" : "OfficialMovixApi",
            ExternalXmlTvHost = Uri.TryCreate(config?.EpgBaseUrl, UriKind.Absolute, out var epg) ? epg.Host : null,
            Status = status,
            GeneratedAt = clock.GetUtcNow(),
        };
    }
}

public sealed record PasswordLoginRequest(string Username, string Password, string Region);

public sealed record SmsPhoneRequest(string Phone);

public sealed record SmsStartRequest(string Phone, string AgreementId, string Region);

public sealed record SmsConfirmRequest(string ChallengeId, string Code);

public sealed record RegionDto(string Id, long Code, string Title);

public sealed record SmsAgreementDto(string Id, string Region, string Title);

public sealed record SmsChallengeDto(string ChallengeId);
