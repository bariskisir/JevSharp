namespace JevSharp.Tests.Integration;

/// <summary>A thread-safe credential strategy that rotates a header for every attempt.</summary>
internal sealed class RotatingAuthentication : IJevAuthentication
{
    private int calls;
    internal int Calls => Volatile.Read(ref calls);

    /// <summary>Applies the next deterministic test credential.</summary>
    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        request.Headers.Add("X-Api-Key", $"key-{Interlocked.Increment(ref calls)}");
        return ValueTask.CompletedTask;
    }
}
