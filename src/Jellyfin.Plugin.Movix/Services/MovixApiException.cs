using System;
using System.Net;

namespace Jellyfin.Plugin.Movix.Services;

public sealed class MovixApiException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
