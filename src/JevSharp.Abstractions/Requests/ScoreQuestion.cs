using JevSharp.Abstractions.Models;

namespace JevSharp.Abstractions.Requests;

/// <summary>A question rating state against ordered levels indexed from zero.</summary>
/// <param name="Instructions">The judgment to make.</param>
/// <param name="Criteria">Ordered level descriptions.</param>
public sealed record ScoreQuestion(JevValue Instructions, IReadOnlyList<JevValue> Criteria) : JevQuestion(Instructions);
