namespace JevSharp.Abstractions.Exceptions;

/// <summary>Local resilience rejected an attempt because its concurrency limit was reached.</summary>
public sealed class JevConcurrencyLimitException(string provider, int attempts) : JevException($"{provider} rejected the evaluation because its local concurrency limit was reached.", provider, attempts);
