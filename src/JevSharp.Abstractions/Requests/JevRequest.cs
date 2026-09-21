namespace JevSharp.Abstractions.Requests;

/// <summary>One shared state and one or more independent questions, sent in one HTTP request.</summary>
/// <param name="State">A string, JSON object, or JSON array.</param>
/// <param name="Questions">Questions keyed by unique, case-sensitive IDs.</param>
/// <remarks>Do not mutate supplied collections while an evaluation is being prepared.</remarks>
public sealed record JevRequest(JevValue State, IReadOnlyDictionary<string, JevQuestion> Questions)
{
    /// <summary>Gets the optional model override for this evaluation.</summary>
    public string? Model { get; init; }

    /// <summary>Gets Vercel-only provider options, represented as a JSON object.</summary>
    public JevValue? VercelProviderOptions { get; init; }
}
