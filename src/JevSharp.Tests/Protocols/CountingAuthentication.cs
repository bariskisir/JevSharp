namespace JevSharp.Tests.Protocols;

/// <summary>Demonstrates authentication independent of protocol serialization.</summary>
internal sealed class CountingAuthentication : IJevAuthentication
{
    internal int Calls;

    /// <inheritdoc />
    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("X-Api-Key", $"key-{Interlocked.Increment(ref Calls)}");
        return ValueTask.CompletedTask;
    }
}
