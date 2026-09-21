using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;

namespace JevSharp.DependencyInjection.Resilience;

/// <summary>Adds optional Microsoft-supported resilience strategies to a Jev HTTP registration.</summary>
public static class JevHttpClientBuilderExtensions
{
    /// <summary>Adds a circuit breaker and a local concurrency limiter without adding HTTP retries or timeouts.</summary>
    /// <param name="builder">The Jev HTTP registration returned by AddJev.</param>
    /// <param name="configure">Configures the local resilience strategies.</param>
    /// <returns>The same HTTP builder.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">A configured ratio, duration, or limit is invalid.</exception>
    /// <remarks>JevClient remains the sole owner of semantic retry and attempt-timeout behavior.</remarks>
    public static IHttpClientBuilder ConfigureJevResilience(this IHttpClientBuilder builder, Action<JevResilienceOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new JevResilienceOptions();
        configure(options);
        options.Validate();

        builder.AddHttpMessageHandler(() => new JevResilienceExceptionHandler());
        builder.AddResilienceHandler("JevSharp", pipeline =>
        {
            pipeline.AddConcurrencyLimiter(options.PermitLimit, options.QueueLimit);
            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = options.FailureRatio,
                MinimumThroughput = options.MinimumThroughput,
                SamplingDuration = options.SamplingDuration,
                BreakDuration = options.BreakDuration
            });
        });
        return builder;
    }
}
