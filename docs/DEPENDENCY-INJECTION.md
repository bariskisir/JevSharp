# Dependency injection, failover, and resilience

```csharp
using JevSharp.Abstractions.Clients;
using JevSharp.Abstractions.Models;
using JevSharp.Core.Configuration;
using JevSharp.DependencyInjection;

builder.Services.AddJev(options =>
{
    options.UseOpenRouter(apiKey, OpenRouterModels.Latest);
    options.Retry.MaxAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(5);
});
```

Inject `IJevClient` into application services. `AddJev` returns `IHttpClientBuilder`, so handlers and transport may be configured. Configuration is validated and copied during registration. Duplicate names are rejected.

```csharp
builder.Services.AddJev("classification", options => options.UseTypeSafe(typeSafeKey));
builder.Services.AddJev("routing", options => options.UseOpenRouter(openRouterKey));

using (var client = factory.CreateClient("routing"))
{
    var response = await client.EvaluateAsync(request, cancellationToken);
}
```

Only the unnamed/default registration supplies `IJevClient` directly. Factory-created clients are caller-owned and should be disposed. Their handlers are pooled by `IHttpClientFactory`. Direct `JevClient` instances own their internally-created `HttpClient`; externally supplied instances remain caller-owned. Reuse clients across evaluations and do not dispose a client while requests are in flight.

## Failover

```csharp
using JevSharp.DependencyInjection.Resilience;

builder.Services.AddJev("primary", options => options.UseTypeSafe(typeSafeKey));
builder.Services.AddJev("secondary", options => options.UseOpenRouter(openRouterKey));
builder.Services.AddJevFailover("decisions", "primary", "secondary");

using (var client = factory.CreateClient("decisions"))
{
    var response = await client.EvaluateAsync(request, cancellationToken);
}
```

Each provider completes its normal Jev retry sequence before the next provider is considered. Failover continues only after transient transport failures, timeouts, HTTP 408/429/retryable server failures, or optional local-resilience rejections. Authentication, authorization, validation, invalid-response, and caller-cancellation failures stop immediately. `JevFailoverException.Failures` preserves ordered typed failures when every provider is exhausted.

## Local resilience

`ConfigureJevResilience` adds optional Microsoft `HttpClient` resilience protection to one named registration. It adds a local concurrency limiter and circuit breaker only; JevSharp remains responsible for request retries and attempt timeouts.

```csharp
builder.Services.AddJev("primary", options => options.UseTypeSafe(typeSafeKey))
    .ConfigureJevResilience(options =>
    {
        options.PermitLimit = 16;
        options.QueueLimit = 8;
        options.FailureRatio = 0.5;
        options.MinimumThroughput = 10;
        options.SamplingDuration = TimeSpan.FromSeconds(30);
        options.BreakDuration = TimeSpan.FromSeconds(30);
    });
```
