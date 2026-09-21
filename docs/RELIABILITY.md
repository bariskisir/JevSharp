# Retries, errors, and logging

Defaults are three total attempts and five seconds between attempts. The whole evaluation is replayed, so providers may charge repeated requests.

```csharp
options.Retry.MaxAttempts = 5;
options.Retry.Delay = TimeSpan.FromSeconds(2);
options.Retry.RespectRetryAfter = true;
options.AttemptTimeout = TimeSpan.FromSeconds(30);
```

Set `MaxAttempts = 1` to disable retries. `RespectRetryAfter` is false by default; when enabled, the greater of the configured delay and a valid provider delay is used. No exponential backoff is added. The default timeout is 60 seconds per attempt. Use a cancellation token for an overall deadline.

Transient connections, attempt timeouts, and HTTP 408, 429, 500, 502, 503, 504, 524, and 529 are retried. Other HTTP failures, malformed successful responses, and caller cancellation are not retried. Successful responses are limited to 8 MiB by default; optional error details are limited to 64 KiB.

| Exception | Meaning |
| --- | --- |
| `ArgumentException` / `ArgumentNullException` | Invalid local configuration or request |
| `JevAuthenticationException` | HTTP 401 |
| `JevAuthorizationException` | HTTP 403 |
| `JevValidationException` | HTTP 400 or 422 |
| `JevRateLimitException` | HTTP 429 after attempts are exhausted |
| `JevApiException` | Other unsuccessful HTTP responses, including insufficient credit |
| `JevTransportException` | Connection or response-stream failure |
| `JevTimeoutException` | Attempt timeout after attempts are exhausted |
| `JevCircuitOpenException` | Optional local circuit breaker is open |
| `JevConcurrencyLimitException` | Optional local concurrency limit rejected the attempt |
| `JevFailoverException` | Every client in an explicit failover chain rejected the evaluation |
| `JevInvalidResponseException` | Invalid JSON, oversized body, or incompatible answers |
| `OperationCanceledException` | Caller cancellation |
| `ObjectDisposedException` | Use after client disposal |

Provider exceptions expose status, attempts, and correlation details where available. Messages exclude response bodies; optional `Details` must not be logged without considering application-data sensitivity.

JevSharp uses `ILogger<JevClient>`. An application already using Serilog through Microsoft logging receives SDK diagnostics without an SDK-specific Serilog dependency. Automatic logs exclude request/response bodies and headers. The DI registration removes default HTTP-factory loggers to avoid exposing custom endpoint query strings and headers.
