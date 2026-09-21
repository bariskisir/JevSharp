namespace JevSharp.Abstractions.Clients;

/// <summary>Creates independently configured named clients backed by managed HTTP handlers.</summary>
public interface IJevClientFactory
{
    /// <summary>Creates a client using a registered name.</summary>
    /// <param name="name">The name supplied during registration.</param>
    /// <returns>A client owned by the caller, to dispose when finished.</returns>
    /// <exception cref="ArgumentException">The name is empty or unregistered.</exception>
    IJevClient CreateClient(string name);
}
