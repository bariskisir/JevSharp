namespace JevSharp.Core.Configuration;

/// <summary>Retry behavior for a complete evaluation request.</summary>
public sealed record JevRetryOptions
{
    /// <summary>Gets or sets total attempts including the first. One disables retries.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Gets or sets the fixed delay between attempts; defaults to five seconds.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets whether server Retry-After can increase the fixed delay.</summary>
    public bool RespectRetryAfter { get; set; }
}
