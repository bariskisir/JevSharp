using JevSharp.Abstractions.Authentication;

namespace JevSharp.Core.Authentication;

/// <summary>Authenticates requests using a copied collection of custom headers.</summary>
public sealed class HeaderAuthentication : IJevAuthentication
{
    private readonly KeyValuePair<string, string>[] headers;

    /// <summary>Copies and validates authentication headers.</summary>
    /// <param name="headers">Request headers, such as X-Api-Key or Authorization.</param>
    /// <exception cref="ArgumentException">A header is invalid or controls transport/protocol behavior.</exception>
    public HeaderAuthentication(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        using (var validation = new HttpRequestMessage())
        {
            foreach (var (name, value) in headers)
            {
                if (string.IsNullOrWhiteSpace(name) || value is null || value.Any(char.IsControl)
                    || name.Equals("Host", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)
                    || IsProtocolHeader(name)
                    || name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("An authentication header is invalid or reserved.", nameof(headers));
                }

                try
                {
                    if (validation.Headers.Contains(name))
                    {
                        throw new ArgumentException("Authentication header names must be unique ignoring case.", nameof(headers));
                    }

                    validation.Headers.Add(name, value);
                }
                catch (Exception ex) when (ex is FormatException or InvalidOperationException)
                {
                    throw new ArgumentException("An authentication header is invalid.", nameof(headers));
                }
            }

            this.headers = headers.ToArray();
        }
    }

    /// <inheritdoc />
    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var (name, value) in headers)
        {
            request.Headers.Remove(name);
            request.Headers.Add(name, value);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Identifies exact headers owned by the Vercel protocol.</summary>
    /// <param name="name">The header name to inspect.</param>
    /// <returns>True when the header is reserved for the Vercel protocol.</returns>
    internal static bool IsProtocolHeader(string name) =>
        name.Equals("ai-model-id", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ai-evaluation-model-specification-version", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ai-gateway-protocol-version", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ai-gateway-auth-method", StringComparison.OrdinalIgnoreCase);
}
