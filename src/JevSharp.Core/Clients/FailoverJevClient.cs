namespace JevSharp.Core.Clients;

/// <summary>Evaluates through ordered clients and moves only across transient provider failures.</summary>
public sealed class FailoverJevClient : IJevClient
{
    private readonly IReadOnlyList<IJevClient> clients;
    private int disposed;

    /// <summary>Initializes a failover chain that owns every supplied client.</summary>
    public FailoverJevClient(IReadOnlyList<IJevClient> clients)
    {
        if (clients is null || clients.Count < 2 || clients.Any(client => client is null))
        {
            throw new ArgumentException("A failover client requires at least two non-null clients.", nameof(clients));
        }

        this.clients = clients.ToArray();
    }

    /// <inheritdoc />
    public async Task<JevResponse> EvaluateAsync(JevRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        var failures = new List<JevException>();
        foreach (var client in clients)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var response = await client.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
                return response;
            }
            catch (JevException exception) when (CanFailOver(exception))
            {
                AddFailures(failures, exception);
            }
        }

        throw new JevFailoverException(failures);
    }

    /// <summary>Disposes every owned client exactly once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }
    }

    /// <summary>Restricts failover to failures that may succeed through another provider.</summary>
    private static bool CanFailOver(JevException exception) => exception switch
    {
        JevFailoverException aggregate => aggregate.Failures.All(CanFailOver),
        JevTransportException { IsTransient: true } => true,
        JevTimeoutException => true,
        JevRateLimitException => true,
        JevCircuitOpenException => true,
        JevConcurrencyLimitException => true,
        JevApiException api => HttpFailureClassifier.IsTransient(api.StatusCode),
        _ => false
    };

    /// <summary>Preserves leaf failures in attempt order when nested chains are exhausted.</summary>
    private static void AddFailures(List<JevException> failures, JevException exception)
    {
        if (exception is JevFailoverException aggregate)
        {
            foreach (var failure in aggregate.Failures)
            {
                AddFailures(failures, failure);
            }
        }
        else
        {
            failures.Add(exception);
        }
    }
}
