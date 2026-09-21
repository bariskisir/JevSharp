namespace JevSharp.Abstractions.Exceptions;

/// <summary>Local resilience rejected an attempt because the provider circuit is open.</summary>
public sealed class JevCircuitOpenException(string provider, int attempts) : JevException($"{provider} is temporarily unavailable because its circuit is open.", provider, attempts);
