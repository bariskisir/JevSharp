using System.ComponentModel.DataAnnotations;

namespace JevSharp.Samples.Web.Contracts;

/// <summary>The ticket content accepted by the evaluation endpoint.</summary>
/// <param name="Message">The nonempty customer message to evaluate.</param>
public sealed record EvaluationTicket([property: Required(AllowEmptyStrings = false)] string Message);
