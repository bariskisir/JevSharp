using JevSharp.Abstractions.Models;

namespace JevSharp.Abstractions.Protocols;

/// <summary>Per-evaluation data provided to a response parser.</summary>
/// <param name="Model">The model sent for this evaluation.</param>
/// <param name="RequestBody">The JSON payload captured before the first attempt.</param>
/// <param name="Provider">The transport provider label, or Custom for a custom endpoint.</param>
/// <param name="Attempts">The number of HTTP attempts so far.</param>
/// <param name="RequestId">The redacted correlation identifier, if supplied by the server.</param>
public sealed record JevProtocolResponseContext(string Model, JevValue RequestBody, string Provider, int Attempts, string? RequestId);
