using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Movix.Configuration;

/// <summary>Movix plugin settings.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Movix discovery API base URL.</summary>
    [Required]
    [SuppressMessage("Major Code Smell", "S1075:URIs should not be hardcoded", Justification = "Administrator-overridable bootstrap default persisted in plugin configuration.")]
    public string ApiBaseUrl { get; set; } = "https://discovery-stb3.ertelecom.ru";

    /// <summary>Optional external XMLTV URL. An empty value uses the official Movix EPG API.</summary>
    public string EpgBaseUrl { get; set; } = string.Empty;

    /// <summary>Movix public content base URL.</summary>
    [Required]
    [SuppressMessage("Major Code Smell", "S1075:URIs should not be hardcoded", Justification = "Administrator-overridable bootstrap default persisted in plugin configuration.")]
    public string ContentBaseUrl { get; set; } = "https://er-cdn.ertelecom.ru/content/public/";

    /// <summary>Emulated client application version.</summary>
    [Required]
    public string ClientVersion { get; set; } = "4.0.30";

    /// <summary>Whether adult channels are included.</summary>
    public bool IncludeAdultChannels { get; set; }

    /// <summary>Simultaneous tuner limit.</summary>
    [Range(1, 4)]
    public int SimultaneousStreams { get; set; } = 4;

    /// <summary>Plugin-created Jellyfin tuner identifier.</summary>
    public string? TunerId { get; set; }

    /// <summary>Plugin-created Jellyfin listings identifier.</summary>
    public string? ListingsId { get; set; }
}
