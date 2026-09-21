namespace JevSharp.Abstractions.Responses;

/// <summary>A selected option and its reported distribution.</summary>
/// <param name="Choice">The selected option name.</param>
/// <param name="Probabilities">Reported probabilities; null if omitted by the provider.</param>
/// <param name="Confidence">Reported confidence; null if unavailable.</param>
public sealed record ChoiceAnswer(string Choice, IReadOnlyDictionary<string, double>? Probabilities, double? Confidence) : JevAnswer;
