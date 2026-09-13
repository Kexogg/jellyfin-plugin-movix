using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.Movix.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Movix;

/// <summary>Connects Movix subscription to Jellyfin Live TV.</summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>Initializes a new instance of the <see cref="Plugin"/> class.</summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer) => Instance = this;

    /// <summary>Gets the singleton plugin instance.</summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Movix connector for Jellyfin";

    /// <inheritdoc />
    public override string Description => "Movix Live TV through Jellyfin's M3U and XMLTV pipeline.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("6fd31852-e733-47bb-b23d-bf7ddc78f6bd");

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new()
        {
            Name = Name,
            EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace),
        },
    ];
}
