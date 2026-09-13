using System;
using Jellyfin.Plugin.Movix.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Movix;

/// <summary>Registers Movix plugin services with Jellyfin.</summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<IMovixClient, MovixClient>(client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddSingleton<MovixSessionStore>();
        services.AddSingleton<MovixSessionManager>();
        services.AddSingleton<MovixGatewayService>();
        services.AddSingleton<MovixLiveTvSetupService>();
    }
}
