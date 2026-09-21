namespace JevSharp.Tests.Protocols;

/// <summary>A non-seekable stream that simulates a stalled or broken response body.</summary>
internal sealed class FailingReadStream : MemoryStream
{
    private readonly bool waitForCancellation;
    internal bool ReadStarted { get; private set; }
    internal bool WasDisposed { get; private set; }
    public override bool CanSeek => false;

    /// <summary>Selects whether reads block until cancellation or fail immediately.</summary>
    internal FailingReadStream(bool waitForCancellation) => this.waitForCancellation = waitForCancellation;

    /// <summary>Simulates a failed network read while respecting cancellation.</summary>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ReadStarted = true;
        if (waitForCancellation)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        throw new IOException("sensitive network context");
    }

    /// <summary>Records disposal of the response stream.</summary>
    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
}
