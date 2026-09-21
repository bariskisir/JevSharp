using JevSharp.Abstractions.Models;

namespace JevSharp.Abstractions.Requests;

/// <summary>Optional descriptions of the two outcomes of a Noul.</summary>
/// <param name="True">A yes description; null omits it, whereas JevValue.Null sends JSON null.</param>
/// <param name="False">A no description; null omits it, whereas JevValue.Null sends JSON null.</param>
public sealed record NoulCriteria(JevValue? True = null, JevValue? False = null);
