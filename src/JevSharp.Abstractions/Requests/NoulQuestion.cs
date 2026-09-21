using JevSharp.Abstractions.Models;

namespace JevSharp.Abstractions.Requests;

/// <summary>A yes/no question returning the probability of yes.</summary>
/// <param name="Instructions">The proposition to evaluate.</param>
/// <param name="Criteria">Optional descriptions of yes and no.</param>
public sealed record NoulQuestion(JevValue Instructions, NoulCriteria? Criteria = null) : JevQuestion(Instructions);
