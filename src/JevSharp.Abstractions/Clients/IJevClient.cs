using JevSharp.Abstractions.Exceptions;
using JevSharp.Abstractions.Requests;
using JevSharp.Abstractions.Responses;

namespace JevSharp.Abstractions.Clients;

/// <summary>Evaluates state using a configured Jev provider.</summary>
public interface IJevClient : IDisposable
{
    /// <summary>Evaluates every question against the same state in one request per attempt.</summary>
    /// <param name="request">The state, questions, and optional per-request settings.</param>
    /// <param name="cancellationToken">Cancels HTTP operations and retry waits.</param>
    /// <returns>Answers keyed by the original question IDs.</returns>
    /// <exception cref="ArgumentException">The request is invalid.</exception>
    /// <exception cref="JevApiException">The provider rejects the request.</exception>
    /// <exception cref="JevTransportException">A connection failure exhausts the attempts.</exception>
    /// <exception cref="JevTimeoutException">An attempt times out and no attempts remain.</exception>
    /// <exception cref="JevCircuitOpenException">Optional local resilience has an open provider circuit.</exception>
    /// <exception cref="JevConcurrencyLimitException">Optional local resilience rejects the local concurrency limit.</exception>
    /// <exception cref="JevFailoverException">Every provider in an explicit failover chain rejects the evaluation.</exception>
    /// <exception cref="JevInvalidResponseException">The response does not match the request contract.</exception>
    /// <exception cref="InvalidOperationException">Authentication or protocol headers violate their ownership contract.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels the evaluation.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<JevResponse> EvaluateAsync(JevRequest request, CancellationToken cancellationToken = default);
}
