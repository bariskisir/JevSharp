using JevSharp.Abstractions.Requests;
using JevSharp.Abstractions.Responses;

namespace JevSharp.Abstractions.Protocols;

/// <summary>Maps common Jev evaluations to a custom JSON request and response format.</summary>
/// <remarks>
/// Implementations must be thread-safe and must not retain mutable per-evaluation state.
/// The client owns HTTP POST, authentication, retries, cancellation, and response size limits.
/// Protocol instances are caller-owned and are not disposed by the client.
/// </remarks>
public interface IJevProtocol
{
    /// <summary>Creates the complete JSON payload once per evaluation, before any HTTP attempts.</summary>
    /// <param name="request">The evaluation to serialize. Do not mutate its collections.</param>
    /// <param name="model">The resolved model, including any per-request override.</param>
    /// <returns>The JSON body and additional protocol headers, copied before sending.</returns>
    /// <exception cref="ArgumentException">The request is unsupported by this protocol.</exception>
    /// <remarks>Preparation exceptions propagate without retrying. Do not include secrets in exception messages.</remarks>
    JevProtocolRequest PrepareRequest(JevRequest request, string model);

    /// <summary>Converts a successful HTTP response into common typed answers.</summary>
    /// <param name="body">The complete JSON response, with independently owned storage.</param>
    /// <param name="context">The immutable request snapshot and transport metadata.</param>
    /// <returns>The normalized response. Validate all protocol-specific fields and distributions.</returns>
    /// <exception cref="System.Text.Json.JsonException">The response does not match the protocol schema.</exception>
    /// <remarks>
    /// Parsing exceptions are replaced with a safe JevInvalidResponseException and are not retried.
    /// The client verifies answer IDs, types, and main value ranges, and sets RequestedModel and RequestId.
    /// Non-success HTTP responses are handled by the client without calling this method.
    /// </remarks>
    JevResponse ParseResponse(JevValue body, JevProtocolResponseContext context);
}
