namespace JevSharp.Abstractions.Exceptions;
/// <summary>An HTTP attempt timed out and no attempts remain.</summary>
public sealed class JevTimeoutException(string provider, int attempts) : JevException($"{provider} timed out after {attempts} attempt(s).", provider, attempts);
