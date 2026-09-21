using JevSharp.Abstractions.Models;

namespace JevSharp.Abstractions.Protocols;

/// <summary>A complete JSON payload and optional protocol headers.</summary>
/// <param name="Body">The immutable JSON payload.</param>
public sealed record JevProtocolRequest(JevValue Body)
{
    /// <summary>Gets additional request headers. Authentication and transport headers are not permitted.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>().AsReadOnly();
}
