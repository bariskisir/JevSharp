namespace JevSharp.DependencyInjection.Resilience;

/// <summary>Configures optional local circuit-breaking and concurrency protection for a named Jev client.</summary>
public sealed record JevResilienceOptions
{
    /// <summary>Gets or initializes the maximum concurrent outbound evaluations.</summary>
    public int PermitLimit { get; set; } = 32;

    /// <summary>Gets or initializes the number of evaluations that may wait for a permit.</summary>
    public int QueueLimit { get; set; }

    /// <summary>Gets or initializes the failure ratio that opens the circuit.</summary>
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>Gets or initializes the minimum sampled operations before the circuit can open.</summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>Gets or initializes the rolling circuit-breaker observation period.</summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or initializes how long an open circuit rejects requests.</summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Validates this immutable configuration before it is attached to an HTTP client.</summary>
    /// <exception cref="ArgumentException">One or more resilience settings are invalid.</exception>
    internal void Validate()
    {
        if (PermitLimit < 1 || QueueLimit < 0 || MinimumThroughput < 2
            || FailureRatio <= 0 || FailureRatio > 1
            || SamplingDuration <= TimeSpan.Zero || BreakDuration <= TimeSpan.Zero)
        {
            throw new ArgumentException("Jev resilience settings contain an invalid limit, ratio, or duration.");
        }
    }
}
