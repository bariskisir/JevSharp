namespace JevSharp.Samples.Web.Contracts;

/// <summary>The normalized probabilities returned by the evaluation endpoint.</summary>
/// <param name="UrgencyProbability">The probability that the ticket is urgent.</param>
/// <param name="RefundProbability">The probability that the ticket requests a refund.</param>
/// <param name="Model">The model reported by the provider, when available.</param>
public sealed record EvaluationResponse(double UrgencyProbability, double RefundProbability, string? Model);
