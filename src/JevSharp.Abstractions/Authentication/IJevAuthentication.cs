namespace JevSharp.Abstractions.Authentication;

/// <summary>Adds authentication headers to a freshly created request on every attempt.</summary>
/// <remarks>Implementations must be thread-safe and must not change the URI, method, or body.</remarks>
public interface IJevAuthentication
{
    /// <summary>Adds or refreshes authentication headers.</summary>
    /// <param name="request">The outgoing request.</param>
    /// <param name="cancellationToken">Cancels credential acquisition.</param>
    /// <returns>A task completing after authentication is applied.</returns>
    /// <exception cref="OperationCanceledException">Credential acquisition was canceled.</exception>
    ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}
