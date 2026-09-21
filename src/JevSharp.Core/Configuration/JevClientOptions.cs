namespace JevSharp.Core.Configuration;

/// <summary>Configures a client. Values are snapshotted when the client is constructed.</summary>
public sealed record JevClientOptions
{
    /// <summary>Gets or sets the model override. Null selects the built-in provider default.</summary>
    public string? Model { get; set; }
    /// <summary>Gets or sets the timeout for each attempt, including reading the response.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(60);
    /// <summary>Gets retry configuration.</summary>
    public JevRetryOptions Retry { get; init; } = new();
    /// <summary>Gets or sets the time source used for timeouts and delays.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    /// <summary>Gets or sets the maximum success-response size in bytes.</summary>
    public int MaxResponseBytes { get; set; } = 8 * 1024 * 1024;
    /// <summary>Gets the built-in format, or null when a protocol instance was supplied.</summary>
    public JevProtocol? Protocol { get; private set; }
    /// <summary>Gets the selected protocol implementation. Instances must be thread-safe.</summary>
    public IJevProtocol? ProtocolImplementation { get; private set; }
    /// <summary>Gets the configured endpoint.</summary>
    public Uri? Endpoint { get; private set; }
    /// <summary>Gets the authentication strategy.</summary>
    public IJevAuthentication? Authentication { get; private set; }
    /// <summary>Gets whether the endpoint is custom.</summary>
    public bool IsCustom { get; private set; }

    /// <summary>Selects TypeSafe with Bearer authentication.</summary>
    /// <param name="apiKey">The TypeSafe key.</param>
    /// <param name="model">A model ID, or null for the latest stable alias.</param>
    /// <returns>This options instance.</returns>
    /// <exception cref="ArgumentException">The API key is invalid.</exception>
    public JevClientOptions UseTypeSafe(string apiKey, string? model = null) =>
        Configure(new("https://api.typesafe.ai/v1/systemone"), JevProtocol.TypeSafe, new BearerAuthentication(apiKey), model, false);

    /// <summary>Selects OpenRouter with Bearer authentication.</summary>
    /// <param name="apiKey">The OpenRouter key.</param>
    /// <param name="model">A model ID, or null for the latest alias.</param>
    /// <returns>This options instance.</returns>
    /// <exception cref="ArgumentException">The API key is invalid.</exception>
    public JevClientOptions UseOpenRouter(string apiKey, string? model = null) =>
        Configure(new("https://openrouter.ai/api/alpha/decisions"), JevProtocol.OpenRouter, new BearerAuthentication(apiKey), model, false);

    /// <summary>Selects Vercel AI Gateway with API-key authentication.</summary>
    /// <param name="apiKey">The AI Gateway key.</param>
    /// <param name="model">A model ID, or null for typesafe-ai/jev.</param>
    /// <returns>This options instance.</returns>
    /// <exception cref="ArgumentException">The API key is invalid.</exception>
    public JevClientOptions UseVercel(string apiKey, string? model = null) =>
        Configure(new("https://ai-gateway.vercel.sh/v4/ai/evaluation-model"), JevProtocol.Vercel, new BearerAuthentication(apiKey), model, false);

    /// <summary>Selects a complete custom endpoint and explicit model with static authentication headers.</summary>
    /// <param name="endpoint">The exact HTTP or HTTPS endpoint.</param>
    /// <param name="protocol">The endpoint's supported JSON protocol.</param>
    /// <param name="model">The model identifier.</param>
    /// <param name="headers">Authentication headers; an empty map supports an unauthenticated local proxy.</param>
    /// <returns>This options instance.</returns>
    /// <exception cref="ArgumentException">A header is invalid.</exception>
    public JevClientOptions UseCustom(Uri endpoint, JevProtocol protocol, string model,
        IReadOnlyDictionary<string, string> headers) => UseCustom(endpoint, protocol, model, new HeaderAuthentication(headers));

    /// <summary>Selects a complete custom endpoint with a dynamic authentication strategy.</summary>
    /// <param name="endpoint">The exact HTTP or HTTPS endpoint.</param>
    /// <param name="protocol">The endpoint's supported JSON protocol.</param>
    /// <param name="model">The explicit model identifier.</param>
    /// <param name="authentication">A thread-safe strategy applied on each attempt.</param>
    /// <returns>This options instance.</returns>
    /// <exception cref="ArgumentNullException">The endpoint or authentication is null.</exception>
    public JevClientOptions UseCustom(Uri endpoint, JevProtocol protocol, string model, IJevAuthentication authentication) =>
        Configure(endpoint, protocol, authentication, model, true);

    /// <summary>Selects a custom endpoint with a caller-defined JSON protocol and static authentication.</summary>
    /// <param name="endpoint">The exact HTTP or HTTPS endpoint.</param>
    /// <param name="protocol">A thread-safe, caller-owned protocol implementation.</param>
    /// <param name="model">The explicit model identifier.</param>
    /// <param name="headers">Authentication headers, or an empty map for an unauthenticated endpoint.</param>
    /// <returns>This options instance.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">A header is invalid.</exception>
    public JevClientOptions UseCustom(Uri endpoint, IJevProtocol protocol, string model,
        IReadOnlyDictionary<string, string> headers) => UseCustom(endpoint, protocol, model, new HeaderAuthentication(headers));

    /// <summary>Selects a custom endpoint with a caller-defined JSON protocol and dynamic authentication.</summary>
    /// <param name="endpoint">The exact HTTP or HTTPS endpoint.</param>
    /// <param name="protocol">A thread-safe, caller-owned protocol implementation.</param>
    /// <param name="model">The explicit model identifier.</param>
    /// <param name="authentication">A thread-safe strategy applied on each attempt.</param>
    /// <returns>This options instance.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public JevClientOptions UseCustom(Uri endpoint, IJevProtocol protocol, string model, IJevAuthentication authentication)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(authentication);
        Endpoint = endpoint;
        Protocol = null;
        ProtocolImplementation = protocol;
        Authentication = authentication;
        Model = model;
        IsCustom = true;
        return this;
    }

    /// <summary>Stores provider selection for validation at client construction.</summary>
    private JevClientOptions Configure(Uri endpoint, JevProtocol protocol, IJevAuthentication authentication, string? model, bool custom)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(authentication);
        Endpoint = endpoint;
        Protocol = protocol;
        ProtocolImplementation = JevProtocols.ResolveBuiltIn(protocol);
        Authentication = authentication;
        Model = model;
        IsCustom = custom;
        return this;
    }
}
