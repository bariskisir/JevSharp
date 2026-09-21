namespace JevSharp.Abstractions.Exceptions;

/// <summary>Every client in an explicit failover chain rejected the evaluation.</summary>
public sealed class JevFailoverException : JevException
{
    /// <summary>Initializes an aggregate failure with the ordered provider failures.</summary>
    /// <param name="failures">Failures returned by every attempted provider.</param>
    /// <exception cref="ArgumentException">No failures were supplied.</exception>
    public JevFailoverException(IReadOnlyList<JevException> failures) : base(
        "All Jev failover providers rejected the evaluation.",
        "Failover",
        GetAttempts(failures),
        GetLastFailure(failures))
    {
        Failures = failures.ToArray();
    }

    /// <summary>Gets the provider failures in the order they were attempted.</summary>
    public IReadOnlyList<JevException> Failures { get; }

    /// <summary>Validates failures before the base exception is initialized.</summary>
    private static int GetAttempts(IReadOnlyList<JevException>? failures)
    {
        return EnsureFailures(failures).Sum(failure => failure.Attempts);
    }

    /// <summary>Gets the final safe provider failure as the aggregate inner exception.</summary>
    private static JevException GetLastFailure(IReadOnlyList<JevException>? failures)
    {
        return EnsureFailures(failures)[^1];
    }

    /// <summary>Rejects empty or null provider failures.</summary>
    private static IReadOnlyList<JevException> EnsureFailures(IReadOnlyList<JevException>? failures)
    {
        if (failures is null || failures.Count == 0 || failures.Any(failure => failure is null))
        {
            throw new ArgumentException("At least one non-null provider failure is required.", nameof(failures));
        }

        return failures;
    }
}
