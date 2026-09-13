using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Movix.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Movix.Services;

public sealed class MovixSessionStore(IApplicationPaths applicationPaths)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _path = Path.Combine(applicationPaths.PluginConfigurationsPath, "movix-session.json");

    public async Task<MovixSession?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<MovixSession>(stream, JsonOptions, cancellationToken);
    }

    public async Task SaveAsync(MovixSession session, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, session, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Move(temporary, _path, true);
    }

    public Task DeleteAsync()
    {
        File.Delete(_path);
        return Task.CompletedTask;
    }

    public static string CreateDeviceId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
}
