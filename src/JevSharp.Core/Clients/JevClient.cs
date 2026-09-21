using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JevSharp.Core.Clients;

/// <summary>A reusable, thread-safe HTTP client for Jev evaluation.</summary>
public sealed class JevClient : IJevClient
{
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly Uri endpoint;
    private readonly IJevProtocol protocol;
    private readonly string provider;
    private readonly string model;
    private readonly IJevAuthentication authentication;
    private readonly int maxAttempts;
    private readonly TimeSpan delay;
    private readonly bool respectRetryAfter;
    private readonly TimeSpan timeout;
    private readonly TimeProvider time;
    private readonly int maxResponseBytes;
    private readonly ILogger<JevClient> logger;
    private int disposed;
    /// <summary>Creates a client and snapshots its options.</summary>
    /// <param name="options">Provider, model, retry, and transport settings.</param>
    /// <param name="httpClient">Optional caller-owned HTTP client. Its timeout also applies.</param>
    /// <param name="logger">Optional logger; request/response bodies and headers are never logged.</param>
    /// <exception cref="ArgumentException">Configuration is invalid or no provider was selected.</exception>
    /// <remarks>Owned clients disable redirects. Configure caller-owned clients similarly when using credentials.</remarks>
    public JevClient(JevClientOptions options, HttpClient? httpClient = null, ILogger<JevClient>? logger = null) : this(options, httpClient, logger, httpClient is null)
    {
    }

    /// <summary>Creates a factory-owned client with explicit HTTP ownership.</summary>
    internal JevClient(JevClientOptions options, HttpClient? httpClient, ILogger<JevClient>? logger, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Endpoint is not { IsAbsoluteUri: true } uri || uri.Scheme is not ("https" or "http")
            || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0
            || options.ProtocolImplementation is null || options.Authentication is null)
        {
            throw new ArgumentException("Select a provider with an absolute HTTP(S) endpoint without user info or fragments.", nameof(options));
        }

        if (options.Retry is null || options.Retry.MaxAttempts < 1 || options.Retry.Delay < TimeSpan.Zero
            || options.Retry.Delay.TotalMilliseconds > uint.MaxValue - 1 || options.AttemptTimeout <= TimeSpan.Zero
            || options.AttemptTimeout.TotalMilliseconds > uint.MaxValue - 1
            || options.TimeProvider is null || options.MaxResponseBytes < 1)
        {
            throw new ArgumentException("Retry, timeout, time provider, or response size settings are invalid.", nameof(options));
        }

        model = options.Model ?? (options.IsCustom ? throw new ArgumentException("Custom endpoints require a model.", nameof(options)) : options.Protocol switch
        {
            JevProtocol.TypeSafe => TypeSafeModels.Latest,
            JevProtocol.OpenRouter => OpenRouterModels.Latest,
            _ => VercelModels.Jev
        });
        ProtocolCodec.ValidateModel(model);
        endpoint = uri;
        protocol = options.ProtocolImplementation;
        provider = options.IsCustom ? "Custom" : options.Protocol.ToString()!;
        authentication = options.Authentication;
        maxAttempts = options.Retry.MaxAttempts;
        delay = options.Retry.Delay;
        respectRetryAfter = options.Retry.RespectRetryAfter;
        timeout = options.AttemptTimeout;
        time = options.TimeProvider;
        maxResponseBytes = options.MaxResponseBytes;
        this.logger = logger ?? NullLogger<JevClient>.Instance;
        this.ownsHttpClient = ownsHttpClient;
        this.httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    /// <inheritdoc />
    public async Task<JevResponse> EvaluateAsync(JevRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = ProtocolEvaluation.Prepare(protocol, request, model);
        var started = time.GetTimestamp();
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var retryDelay = delay;
            try
            {
                var result = await SendAttemptAsync(prepared, attempt, cancellationToken).ConfigureAwait(false);
                logger.LogInformation(
                    "Jev {Provider} evaluation completed in {DurationMs} ms after {Attempts} attempt(s).",
                    provider, time.GetElapsedTime(started).TotalMilliseconds, attempt);
                return result;
            }
            catch (JevApiException ex) when (HttpFailureClassifier.IsTransient(ex.StatusCode) && attempt < maxAttempts)
            {
                if (respectRetryAfter && ex.RetryAfter > retryDelay)
                {
                    retryDelay = ex.RetryAfter.Value;
                }
            }
            catch (JevTimeoutException) when (attempt < maxAttempts)
            {
                // Attempt timeout is independent of caller cancellation.
            }
            catch (JevTransportException ex) when (ex.IsTransient && attempt < maxAttempts)
            {
                // Only classified connection failures are retried.
            }
            logger.LogWarning(
                "Retrying Jev {Provider} evaluation after attempt {Attempt}; delay {DelayMs} ms.",
                provider, attempt, retryDelay.TotalMilliseconds);
            await DelayAsync(retryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Runs one bounded HTTP attempt and releases all resources before a retry wait.</summary>
    private async Task<JevResponse> SendAttemptAsync(
        ProtocolEvaluation prepared, int attempt, CancellationToken cancellationToken)
    {
        using (var expiration = new CancellationTokenSource(timeout, time))
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, expiration.Token))
            {
                try
                {
                    using (var message = CreateRequest(prepared))
                    {
                        message.Options.Set(new HttpRequestOptionsKey<int>("JevSharp.Attempt"), attempt);
                        var authenticationGuard = new AuthenticationRequestGuard(message);
                        await authentication.ApplyAsync(message, linked.Token).ConfigureAwait(false);
                        authenticationGuard.Validate(message);
                        ApplyProtocolHeaders(message, prepared);
                        using (var response = await httpClient.SendAsync(
                            message, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false))
                        {
                            var secrets = GetHeaderValues(message);
                            var requestId = Redact(GetRequestId(response), secrets);
                            if (!response.IsSuccessStatusCode)
                            {
                                var details = await ReadErrorDetailsAsync(
                                    response, secrets, linked.Token, cancellationToken).ConfigureAwait(false);
                                cancellationToken.ThrowIfCancellationRequested();
                                throw CreateApiException(response.StatusCode, attempt, requestId, details, GetRetryAfter(response));
                            }
                            var bytes = await ReadBodyAsync(response, maxResponseBytes, linked.Token).ConfigureAwait(false);
                            cancellationToken.ThrowIfCancellationRequested();
                            if (bytes is null)
                            {
                                throw new JevInvalidResponseException(provider, attempt, requestId);
                            }
                            var result = prepared.Parse(protocol, bytes, provider, attempt, requestId, cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();
                            return result;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw new JevTimeoutException(provider, attempt);
                }
                catch (HttpRequestException ex)
                {
                    throw new JevTransportException(provider, attempt, HttpFailureClassifier.IsTransient(ex));
                }
                catch (IOException)
                {
                    throw new JevTransportException(provider, attempt, true);
                }
            }
        }
    }

    /// <summary>Creates an isolated message whose body is identical across attempts.</summary>
    private HttpRequestMessage CreateRequest(ProtocolEvaluation prepared)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(prepared.Body)
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        message.Headers.Accept.Add(new("application/json"));
        message.Headers.UserAgent.ParseAdd("JevSharp/" + typeof(JevClient).Assembly.GetName().Version);
        message.Options.Set(new HttpRequestOptionsKey<string>("JevSharp.Provider"), provider);
        return message;
    }

    /// <summary>Applies protocol-owned headers after the authentication strategy has completed.</summary>
    private static void ApplyProtocolHeaders(HttpRequestMessage message, ProtocolEvaluation prepared)
    {
        foreach (var (name, value) in prepared.Headers)
        {
            if (message.Headers.Contains(name) && !HeaderAuthentication.IsProtocolHeader(name))
            {
                throw new InvalidOperationException("Protocol headers must not replace authentication headers.");
            }

            SetHeader(message, name, value);
        }
    }

    /// <summary>Reads optional error details without losing a known HTTP failure when its body cannot be read.</summary>
    private static async Task<string?> ReadErrorDetailsAsync(
        HttpResponseMessage response, string[] secrets, CancellationToken attemptToken, CancellationToken callerToken)
    {
        try
        {
            var bytes = await ReadBodyAsync(response, 64 * 1024, attemptToken).ConfigureAwait(false);
            return bytes is null ? null : Redact(Encoding.UTF8.GetString(bytes), secrets);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return null;
        }
    }

    /// <summary>Disposes HTTP resources owned by this client; injected HTTP clients are left open.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0 && ownsHttpClient)
        {
            httpClient.Dispose();
        }
    }

    /// <summary>Creates a client that owns the HttpClient supplied by an HTTP factory.</summary>
    /// <param name="options">Client settings.</param>
    /// <param name="httpClient">An HTTP factory client whose disposal does not destroy pooled handlers.</param>
    /// <param name="logger">An optional logger.</param>
    /// <returns>The owning client.</returns>
    /// <exception cref="ArgumentException">The settings are invalid.</exception>
    public static JevClient CreateOwned(JevClientOptions options, HttpClient httpClient, ILogger<JevClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return new(options, httpClient, logger, true);
    }

    /// <summary>Replaces a protocol header after custom authentication.</summary>
    private static void SetHeader(HttpRequestMessage message, string name, string value)
    {
        message.Headers.Remove(name);
        message.Headers.Add(name, value);
    }

    /// <summary>Reads a bounded body; returns null when the limit is exceeded.</summary>
    private static async Task<byte[]?> ReadBodyAsync(HttpResponseMessage response, int limit, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > limit)
        {
            return null;
        }

        using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    if (output.Length + read > limit)
                    {
                        return null;
                    }

                    output.Write(buffer, 0, read);
                }

                return output.ToArray();
            }
        }
    }

    /// <summary>Reads a valid Retry-After duration or HTTP date.</summary>
    private TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        var after = header?.Delta ?? (header?.Date - time.GetUtcNow());
        return after >= TimeSpan.Zero ? after : null;
    }

    /// <summary>Waits in bounded chunks to support long server delays and immediate cancellation.</summary>
    private async Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        var maximum = TimeSpan.FromDays(1);
        while (duration > maximum)
        {
            await Task.Delay(maximum, time, cancellationToken).ConfigureAwait(false);
            duration -= maximum;
        }

        await Task.Delay(duration, time, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Obtains a correlation identifier without copying all response headers.</summary>
    private static string? GetRequestId(HttpResponseMessage response)
    {
        foreach (var name in new[]
        {
            "x-request-id",
            "request-id",
            "x-vercel-id"
        }

        )
        {
            if (response.Headers.TryGetValues(name, out var values))
            {
                return values.FirstOrDefault();
            }
        }

        return null;
    }

    /// <summary>Collects possible credential values for redaction, including dynamically applied headers.</summary>
    private static string[] GetHeaderValues(HttpRequestMessage message) => message.Headers
        .Where(h => h.Key is not ("Accept" or "User-Agent") && !HeaderAuthentication.IsProtocolHeader(h.Key))
        .SelectMany(h => h.Value)
        .SelectMany(v => new[] { v, v.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? v[7..] : v })
        .Where(v => v.Length > 0)
        .Distinct()
        .OrderByDescending(v => v.Length)
        .ToArray();
    /// <summary>Removes known header secrets from optional error details and identifiers.</summary>
    private static string? Redact(string? value, string[] secrets)
    {
        if (value is null)
        {
            return null;
        }

        foreach (var secret in secrets)
        {
            value = value.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
            value = value.Replace(JsonSerializer.Serialize(secret)[1..^1], "[REDACTED]", StringComparison.Ordinal);
        }

        return value;
    }

    /// <summary>Maps HTTP errors to documented exception types without exposing details in messages.</summary>
    private JevApiException CreateApiException(HttpStatusCode status, int attempt, string? requestId, string? details, TimeSpan? after) => status switch
    {
        HttpStatusCode.Unauthorized => new JevAuthenticationException(provider, attempt, requestId, details),
        HttpStatusCode.Forbidden => new JevAuthorizationException(provider, attempt, requestId, details),
        HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => new JevValidationException(provider, attempt, status, requestId, details),
        HttpStatusCode.TooManyRequests => new JevRateLimitException(provider, attempt, requestId, details, after),
        _ => new JevApiException(provider, attempt, status, requestId, details, after)
    };
}
