namespace JevSharp.Abstractions.Exceptions;

/// <summary>The base exception for SDK transport and provider failures.</summary>
public abstract class JevException : Exception
{
    /// <summary>Initializes an SDK failure with safe diagnostic context.</summary>
    /// <param name="message">A message that excludes request and response bodies.</param>
    /// <param name="provider">The configured provider.</param>
    /// <param name="attempts">The number of attempts made.</param>
    /// <param name="innerException">An optional underlying exception.</param>
    protected JevException(string message, string provider, int attempts, Exception? innerException = null) : base(message, innerException)
    {
        Provider = provider;
        Attempts = attempts;
    }

    /// <summary>Gets the configured provider.</summary>
    public string Provider { get; }

    /// <summary>Gets the number of attempts made.</summary>
    public int Attempts { get; }
}
