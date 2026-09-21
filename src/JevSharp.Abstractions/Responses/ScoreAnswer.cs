using JevSharp.Abstractions.Models;

namespace JevSharp.Abstractions.Responses;

/// <summary>A probability-weighted, potentially fractional score.</summary>
/// <param name="Score">The weighted level index.</param>
/// <param name="Probabilities">Reported probabilities, keyed by level index.</param>
/// <param name="Legend">Reported descriptions, keyed by level index.</param>
/// <param name="Confidence">Reported confidence; null if unavailable.</param>
public sealed record ScoreAnswer(double Score, IReadOnlyDictionary<string, double>? Probabilities, IReadOnlyDictionary<string, JevValue>? Legend, double? Confidence) : JevAnswer;
