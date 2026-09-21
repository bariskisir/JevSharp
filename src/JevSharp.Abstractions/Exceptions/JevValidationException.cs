using System.Net;
namespace JevSharp.Abstractions.Exceptions;
/// <summary>The provider rejected the request shape (HTTP 400 or 422).</summary>
public sealed class JevValidationException(string provider, int attempts, HttpStatusCode statusCode, string? requestId = null, string? details = null) : JevApiException(provider, attempts, statusCode, requestId, details);
