using System.Net;
namespace JevSharp.Abstractions.Exceptions;
/// <summary>The provider rejected authentication (HTTP 401).</summary>
public sealed class JevAuthenticationException(string provider, int attempts, string? requestId = null, string? details = null) : JevApiException(provider, attempts, HttpStatusCode.Unauthorized, requestId, details);
