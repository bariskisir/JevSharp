using System.Net;
namespace JevSharp.Abstractions.Exceptions;
/// <summary>The provider rate limited the request (HTTP 429).</summary>
public sealed class JevRateLimitException(string provider, int attempts, string? requestId = null, string? details = null, TimeSpan? retryAfter = null) : JevApiException(provider, attempts, HttpStatusCode.TooManyRequests, requestId, details, retryAfter);
