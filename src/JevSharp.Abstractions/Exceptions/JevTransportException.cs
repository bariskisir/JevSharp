namespace JevSharp.Abstractions.Exceptions;
/// <summary>A connection failure after the configured attempts.</summary>
public sealed class JevTransportException(string provider, int attempts, bool isTransient = false) : JevException($"Could not reach {provider} after {attempts} attempt(s).", provider, attempts)
{
    /// <summary>Gets whether the transport classified the failure as transient.</summary>
    public bool IsTransient { get; } = isTransient;
}
