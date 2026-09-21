namespace JevSharp.Abstractions.Responses;

/// <summary>The probability that a proposition is true.</summary>
/// <param name="Probability">A value from zero to one, without automatic thresholding.</param>
public sealed record NoulAnswer(double Probability) : JevAnswer;
