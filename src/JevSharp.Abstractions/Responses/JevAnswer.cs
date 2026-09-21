using System.Text.Json;

namespace JevSharp.Abstractions.Responses;

/// <summary>The common base of typed Jev answers.</summary>
public abstract record JevAnswer
{
    /// <summary>Gets provider fields not represented by the typed answer.</summary>
    public IReadOnlyDictionary<string, JsonElement> AdditionalData { get; init; } = new Dictionary<string, JsonElement>().AsReadOnly();
}
