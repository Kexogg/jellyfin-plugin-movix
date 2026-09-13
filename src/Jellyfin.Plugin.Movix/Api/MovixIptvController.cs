using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Movix.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Movix.Api;

/// <summary>Loopback-only IPTV endpoints consumed by Jellyfin.</summary>
[ApiController]
[Route(MovixApiContract.PluginRoutes.IptvBase)]
public sealed class MovixIptvController(MovixGatewayService gateway) : ControllerBase
{
    [HttpGet(MovixApiContract.PluginRoutes.Playlist)]
    [AllowAnonymous]
    [Produces("audio/x-mpegurl")]
    public async Task<IActionResult> GetPlaylist(CancellationToken cancellationToken)
    {
        if (!IsLoopback())
        {
            return NotFound();
        }

        try
        {
            return Content(await gateway.GetPlaylistAsync(cancellationToken), "audio/x-mpegurl");
        }
        catch (MovixApiException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet(MovixApiContract.PluginRoutes.Epg)]
    [AllowAnonymous]
    [Produces("application/xml")]
    public async Task<IActionResult> GetEpg(CancellationToken cancellationToken)
    {
        if (!IsLoopback())
        {
            return NotFound();
        }

        try
        {
            return Content(await gateway.GetEpgAsync(false, cancellationToken), "application/xml; charset=utf-8");
        }
        catch (MovixApiException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet(MovixApiContract.PluginRoutes.Stream)]
    [AllowAnonymous]
    public async Task<IActionResult> GetStream(long channelId, CancellationToken cancellationToken)
    {
        if (!IsLoopback())
        {
            return NotFound();
        }

        try
        {
            var uri = await gateway.ResolveStreamAsync(channelId, cancellationToken);
            Response.Headers.CacheControl = "no-store, no-cache";
            return RedirectPreserveMethod(uri.ToString());
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (MovixApiException ex)
        {
            return Problem(ex.Message, statusCode: ex.StatusCode == HttpStatusCode.Unauthorized ? StatusCodes.Status401Unauthorized : StatusCodes.Status502BadGateway);
        }
    }

    private bool IsLoopback() => HttpContext.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address);
}
