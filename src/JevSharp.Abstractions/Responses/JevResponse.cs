using System.Text.Json;
using JevSharp.Abstractions.Models;

namespace JevSharp.Abstractions.Responses;

/// <summary>A successful evaluation with one answer per question.</summary>
public sealed record JevResponse
{
    /// <summary>Gets the typed answers, keyed by exact question IDs.</summary>
    public required IReadOnlyDictionary<string, JevAnswer> Answers { get; init; }

    /// <summary>Gets the model ID sent in the request.</summary>
    public required string RequestedModel { get; init; }

    /// <summary>Gets the model reported by the provider, or null if not reported.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the provider's response identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the provider's request correlation identifier.</summary>
    public string? RequestId { get; init; }

    /// <summary>Gets the provider name, when reported.</summary>
    public string? Provider { get; init; }

    /// <summary>Gets token usage and cost, when reported.</summary>
    public JevUsage? Usage { get; init; }

    /// <summary>Gets Vercel provider metadata, when reported.</summary>
    public JevValue? ProviderMetadata { get; init; }

    /// <summary>Gets Vercel rounding metadata, when reported.</summary>
    public JevValue? Rounding { get; init; }

    /// <summary>Gets provider warnings, preserving their complete JSON structure.</summary>
    public IReadOnlyList<JevValue> Warnings { get; init; } = [];

    /// <summary>Gets unmodeled top-level fields.</summary>
    public IReadOnlyDictionary<string, JsonElement> AdditionalData { get; init; } = new Dictionary<string, JsonElement>().AsReadOnly();

    /// <summary>Retrieves an answer by ID and verifies its type.</summary>
    /// <typeparam name="TAnswer">The expected answer type.</typeparam>
    /// <param name="questionId">The exact question ID.</param>
    /// <returns>The matching answer.</returns>
    /// <exception cref="ArgumentException">The ID is empty.</exception>
    /// <exception cref="KeyNotFoundException">The ID is absent.</exception>
    /// <exception cref="InvalidOperationException">The answer has a different type.</exception>
    public TAnswer GetAnswer<TAnswer>(string questionId) where TAnswer : JevAnswer
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionId);
        return Answers[questionId] as TAnswer ?? throw new InvalidOperationException($"The answer is not a {typeof(TAnswer).Name}.");
    }
}
