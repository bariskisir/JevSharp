using JevSharp.Abstractions.Exceptions;
using Polly.CircuitBreaker;
using Polly.RateLimiting;

namespace JevSharp.DependencyInjection.Resilience;

/// <summary>Normalizes Microsoft resilience rejections into Jev exception types.</summary>
internal sealed class JevResilienceExceptionHandler : DelegatingHandler
{
    private const string ProviderKey = "JevSharp.Provider";
    private const string AttemptKey = "JevSharp.Attempt";

    /// <summary>Runs the inner handler and preserves local resilience classifications.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (BrokenCircuitException)
        {
            throw new JevCircuitOpenException(GetProvider(request), GetAttempt(request));
        }
        catch (RateLimiterRejectedException)
        {
            throw new JevConcurrencyLimitException(GetProvider(request), GetAttempt(request));
        }
    }

    /// <summary>Gets the provider set by the SDK request creator.</summary>
    private static string GetProvider(HttpRequestMessage request) => request.Options.TryGetValue(new HttpRequestOptionsKey<string>(ProviderKey), out var provider)
        ? provider
        : "Unknown";

    /// <summary>Gets the current SDK attempt set by the SDK request creator.</summary>
    private static int GetAttempt(HttpRequestMessage request) => request.Options.TryGetValue(new HttpRequestOptionsKey<int>(AttemptKey), out var attempt)
        ? attempt
        : 1;
}
