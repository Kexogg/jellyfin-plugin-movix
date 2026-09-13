using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Movix.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Movix.Services;

public sealed class MovixSessionManager(
    IMovixClient client,
    MovixSessionStore store,
    TimeProvider clock,
    ILogger<MovixSessionManager> logger)
{
    private static readonly TimeSpan RenewalMargin = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private readonly ConcurrentDictionary<string, SmsChallenge> _smsChallenges = new(StringComparer.Ordinal);
    private MovixSession? _session;

    public string? LastError { get; private set; }

    public async Task<MovixSession> EnsureDeviceSessionAsync(CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            _session ??= await store.LoadAsync(cancellationToken);
            if (_session is not null && !_session.IsExpiring(clock, RenewalMargin))
            {
                return _session;
            }

            var session = await RenewSessionAsync(cancellationToken);
            LastError = null;
            return session;
        }
        catch (MovixApiException ex)
        {
            LastError = ex.Message;
            throw;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task<IReadOnlyList<MovixRegion>> GetRegionsAsync(CancellationToken cancellationToken)
    {
        var session = await EnsureDeviceSessionAsync(cancellationToken);
        return await client.GetRegionsAsync(session.Token, session.DeviceId, cancellationToken);
    }

    public async Task LoginPasswordAsync(string username, string password, string region, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        var session = await EnsureDeviceSessionAsync(cancellationToken);
        var token = await client.LoginPasswordAsync(session.Token, session.DeviceId, username, password, region, cancellationToken);
        await SetSessionAsync(
            new MovixSession(session.DeviceId, token.Token, token.Expires, true, token.IsBound ?? false),
            cancellationToken);
    }

    public async Task<IReadOnlyList<SmsAgreement>> GetSmsAgreementsAsync(string phone, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        PurgeSmsChallenges();
        var session = await EnsureDeviceSessionAsync(cancellationToken);
        return await client.RequestSmsAsync(session.Token, session.DeviceId, phone, cancellationToken);
    }

    public async Task<string> SendSmsAsync(string phone, SmsAgreement agreement, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        ArgumentException.ThrowIfNullOrWhiteSpace(agreement.AgreementId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agreement.Region);
        var session = await EnsureDeviceSessionAsync(cancellationToken);
        var confirmedAgreement = await client.SendSmsAsync(session.Token, session.DeviceId, phone, agreement, cancellationToken);
        var id = Guid.NewGuid().ToString("N");
        _smsChallenges[id] = new SmsChallenge(phone, confirmedAgreement, clock.GetUtcNow().AddMinutes(10));
        return id;
    }

    public async Task ConfirmSmsAsync(string challengeId, string code, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(challengeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        PurgeSmsChallenges();
        if (!_smsChallenges.TryRemove(challengeId, out var challenge))
        {
            throw new InvalidOperationException("The SMS challenge is missing or expired.");
        }

        var session = await EnsureDeviceSessionAsync(cancellationToken);
        var token = await client.ConfirmSmsAsync(session.Token, session.DeviceId, challenge.Phone, challenge.Agreement, code, cancellationToken);
        await SetSessionAsync(
            new MovixSession(session.DeviceId, token.Token, token.Expires, true, token.IsBound ?? false),
            cancellationToken);
    }

    public async Task<MovixSession> EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        var session = await EnsureDeviceSessionAsync(cancellationToken);
        if (!session.IsAuthenticated)
        {
            throw new MovixApiException("Movix authentication is required.", HttpStatusCode.Unauthorized);
        }

        return session;
    }

    public async Task EnsureBoundAsync(CancellationToken cancellationToken)
    {
        var session = await EnsureAuthenticatedAsync(cancellationToken);
        if (!await client.IsBoundAsync(session.Token, session.DeviceId, cancellationToken))
        {
            await client.BindDeviceAsync(session.Token, session.DeviceId, cancellationToken);
        }

        if (!session.IsBound)
        {
            await SetSessionAsync(session with { IsBound = true }, cancellationToken);
        }
    }

    public async Task<T> ExecuteAuthenticatedAsync<T>(Func<MovixSession, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var session = await EnsureAuthenticatedAsync(cancellationToken);
        try
        {
            return await operation(session, cancellationToken);
        }
        catch (MovixApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await ForceRenewAsync(cancellationToken);
            session = await EnsureAuthenticatedAsync(cancellationToken);
            return await operation(session, cancellationToken);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        var session = _session ?? await store.LoadAsync(cancellationToken);
        if (session is { IsBound: true })
        {
            try
            {
                await client.UnbindOwnDeviceAsync(session.Token, session.DeviceId, cancellationToken);
            }
            catch (MovixApiException ex)
            {
                logger.LogWarning(ex, "Movix device could not be unbound");
                LastError = "Local disconnect succeeded, but the Movix device could not be unbound.";
            }
        }

        _session = null;
        _smsChallenges.Clear();
        await store.DeleteAsync();
    }

    public async Task<MovixSession?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            return _session ??= await store.LoadAsync(cancellationToken);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task ForceRenewAsync(CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            _session ??= await store.LoadAsync(cancellationToken);
            await RenewSessionAsync(cancellationToken);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task<MovixSession> RenewSessionAsync(CancellationToken cancellationToken)
    {
        var current = _session;
        var deviceId = current?.DeviceId ?? MovixSessionStore.CreateDeviceId();
        var token = current is null
            ? await client.GetDeviceTokenAsync(deviceId, cancellationToken)
            : await client.RefreshTokenAsync(current.Token, deviceId, cancellationToken);
        _session = new MovixSession(
            deviceId,
            token.Token,
            token.Expires,
            current?.IsAuthenticated ?? false,
            token.IsBound ?? current?.IsBound ?? false);
        await store.SaveAsync(_session, cancellationToken);
        return _session;
    }

    private async Task SetSessionAsync(MovixSession session, CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            _session = session;
            await store.SaveAsync(session, cancellationToken);
            LastError = null;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private void PurgeSmsChallenges()
    {
        var now = clock.GetUtcNow();
        foreach (var item in _smsChallenges.Where(item => item.Value.ExpiresAt <= now))
        {
            _smsChallenges.TryRemove(item.Key, out _);
        }
    }
}
