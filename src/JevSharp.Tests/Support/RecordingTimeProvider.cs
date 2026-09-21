using System.Collections.Concurrent;
using Microsoft.Extensions.Time.Testing;

namespace JevSharp.Tests.Support;

/// <summary>A fake clock that also records when retry and timeout timers have been scheduled.</summary>
internal sealed class RecordingTimeProvider : TimeProvider
{
    private readonly FakeTimeProvider inner = new();
    internal ConcurrentQueue<TimeSpan> Scheduled { get; } = new();

    /// <summary>Gets fake wall-clock time.</summary>
    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();
    /// <summary>Gets the fake monotonic timestamp.</summary>
    public override long GetTimestamp() => inner.GetTimestamp();
    /// <summary>Gets the timestamp frequency.</summary>
    public override long TimestampFrequency => inner.TimestampFrequency;
    /// <summary>Schedules a fake timer and records its initial delay.</summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = inner.CreateTimer(callback, state, dueTime, period);
        Scheduled.Enqueue(dueTime);
        return timer;
    }

    /// <summary>Advances the clock and fires due timers.</summary>
    internal void Advance(TimeSpan duration)
    {
        inner.Advance(duration);
    }
}
