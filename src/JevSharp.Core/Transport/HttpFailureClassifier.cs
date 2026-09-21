using System.Net;

namespace JevSharp.Core.Transport;

/// <summary>Shares provider-neutral transient failure rules across retry, failover, and HTTP resilience.</summary>
internal static class HttpFailureClassifier
{
    /// <summary>Identifies HTTP statuses eligible for another attempt.</summary>
    internal static bool IsTransient(HttpStatusCode status) => (int)status is 408 or 429 or 500 or 502 or 503 or 504 or 524 or 529;

    /// <summary>Identifies recoverable failures while sending or reading HTTP data.</summary>
    internal static bool IsTransient(Exception? exception) => exception is IOException
        or HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError or HttpRequestError.ResponseEnded };
}
