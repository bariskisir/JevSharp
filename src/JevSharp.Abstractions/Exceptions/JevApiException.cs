using System.Net;

namespace JevSharp.Abstractions.Exceptions;

/// <summary>An unsuccessful HTTP response from a provider.</summary>
public class JevApiException : JevException
{
    /// <summary>Initializes an HTTP failure. Details are redacted and excluded from Message/ToString.</summary>
    /// <param name="provider">The configured provider.</param>
    /// <param name="attempts">The attempts made.</param>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="requestId">The provider correlation ID.</param>
    /// <param name="details">Optional redacted response details; may contain application data.</param>
    /// <param name="retryAfter">The provider's suggested delay.</param>
    public JevApiException(string provider, int attempts, HttpStatusCode statusCode, string? requestId = null, string? details = null, TimeSpan? retryAfter = null) : base($"{provider} returned HTTP {(int)statusCode} after {attempts} attempt(s).", provider, attempts)
    {
        StatusCode = statusCode;
        RequestId = requestId;
        Details = details;
        RetryAfter = retryAfter;
    }

    /// <summary>Gets the HTTP status code.</summary>
    public HttpStatusCode StatusCode { get; }
    /// <summary>Gets the request correlation ID.</summary>
    public string? RequestId { get; }
    /// <summary>Gets redacted, bounded response details; treat them as sensitive application data.</summary>
    public string? Details { get; }
    /// <summary>Gets the Retry-After value, when valid.</summary>
    public TimeSpan? RetryAfter { get; }
}
