using System.Text.Json;

namespace JevSharp.Abstractions.Responses;

/// <summary>Provider-reported usage; absent values are never inferred.</summary>
/// <param name="InputTokens">Input tokens, when supplied.</param>
/// <param name="OutputTokens">Output tokens, when supplied.</param>
/// <param name="Cost">Reported cost in USD, when supplied.</param>
public sealed record JevUsage(long? InputTokens, long? OutputTokens, decimal? Cost)
{
    /// <summary>Gets additional provider-reported usage fields.</summary>
    public IReadOnlyDictionary<string, JsonElement> AdditionalData { get; init; } = new Dictionary<string, JsonElement>().AsReadOnly();
}
