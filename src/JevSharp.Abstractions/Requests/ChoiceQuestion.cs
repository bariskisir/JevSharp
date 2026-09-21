using JevSharp.Abstractions.Models;

namespace JevSharp.Abstractions.Requests;

/// <summary>A question selecting one named option.</summary>
/// <param name="Instructions">The judgment to make.</param>
/// <param name="Criteria">Option names and their descriptions, including explicit JSON null.</param>
public sealed record ChoiceQuestion(JevValue Instructions, IReadOnlyDictionary<string, JevValue> Criteria) : JevQuestion(Instructions);
