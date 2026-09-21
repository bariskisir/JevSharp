using System.Net;
namespace JevSharp.Abstractions.Exceptions;
/// <summary>The provider denied access (HTTP 403).</summary>
public sealed class JevAuthorizationException(string provider, int attempts, string? requestId = null, string? details = null) : JevApiException(provider, attempts, HttpStatusCode.Forbidden, requestId, details);
