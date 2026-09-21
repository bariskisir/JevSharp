using JevSharp.Abstractions.Authentication;

namespace JevSharp.Core.Authentication;

/// <summary>Authenticates each request with a Bearer API key.</summary>
public sealed class BearerAuthentication : IJevAuthentication
{
    private readonly string apiKey;

    /// <summary>Creates Bearer authentication.</summary>
    /// <param name="apiKey">A nonempty API key without control characters.</param>
    /// <exception cref="ArgumentException">The key is invalid.</exception>
    public BearerAuthentication(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (apiKey.Any(char.IsControl))
        {
            throw new ArgumentException("The API key contains control characters.", nameof(apiKey));
        }

        this.apiKey = apiKey;
    }

    /// <inheritdoc />
    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        request.Headers.Authorization = new("Bearer", apiKey);
        return ValueTask.CompletedTask;
    }
}
