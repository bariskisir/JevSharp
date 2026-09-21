namespace JevSharp.Core.Authentication;

/// <summary>Protects transport settings and content headers from caller authentication changes.</summary>
internal sealed class AuthenticationRequestGuard
{
    private readonly Uri? uri;
    private readonly HttpMethod method;
    private readonly HttpContent? content;
    private readonly Version version;
    private readonly HttpVersionPolicy versionPolicy;
    private readonly string[] headers;
    private readonly KeyValuePair<string, object?>[] options;

    /// <summary>Snapshots fields owned by the client before invoking authentication.</summary>
    internal AuthenticationRequestGuard(HttpRequestMessage request)
    {
        uri = request.RequestUri;
        method = request.Method;
        content = request.Content;
        version = request.Version;
        versionPolicy = request.VersionPolicy;
        headers = ProtectedHeaders(request);
        options = request.Options.ToArray();
    }

    /// <summary>Rejects mutations before an HTTP request can be sent.</summary>
    internal void Validate(HttpRequestMessage request)
    {
        if (request.RequestUri != uri || request.Method != method || !ReferenceEquals(request.Content, content)
            || request.Version != version || request.VersionPolicy != versionPolicy
            || !headers.SequenceEqual(ProtectedHeaders(request), StringComparer.Ordinal)
            || !options.SequenceEqual(request.Options))
        {
            throw new InvalidOperationException("Authentication must only modify authentication headers.");
        }
    }

    /// <summary>Copies protected header names and values, including every content header.</summary>
    private static string[] ProtectedHeaders(HttpRequestMessage request) => request.Headers
        .Where(header => HeaderAuthentication.IsReservedHeader(header.Key))
        .Concat(request.Content?.Headers.AsEnumerable() ?? [])
        .OrderBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
        .SelectMany(header => new[] { header.Key.ToUpperInvariant() }.Concat(header.Value))
        .ToArray();
}
