namespace JevSharp.Abstractions.Exceptions;
/// <summary>A successful response violated the evaluation contract.</summary>
public sealed class JevInvalidResponseException(string provider, int attempts, string? requestId = null) : JevException($"{provider} returned an invalid evaluation response.", provider, attempts)
{
    /// <summary>Gets the provider request correlation ID.</summary>
    public string? RequestId { get; } = requestId;
}
