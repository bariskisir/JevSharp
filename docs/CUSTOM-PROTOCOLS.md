# Custom endpoints, authentication, and protocols

```csharp
var options = new JevClientOptions().UseCustom(
    new Uri("https://gateway.example.com/custom/evaluate"),
    JevProtocol.OpenRouter,
    model: "my-jev-model",
    headers: new Dictionary<string, string>
    {
        ["X-Api-Key"] = customKey,
        ["X-Tenant"] = tenantId
    });
```

The complete URL is used as supplied. Choose the matching TypeSafe, OpenRouter, or Vercel JSON protocol. Empty headers support local unauthenticated proxies. No Bearer header is added automatically for custom endpoints.

For example, a custom server that accepts OpenRouter-compatible requests and emits OpenRouter-compatible responses should use `JevProtocol.OpenRouter`. The enum selects a JSON format; it does not route requests to OpenRouter or add its credentials.

For a different JSON contract, implement `IJevProtocol` and pass its instance to `UseCustom`. `PrepareRequest(JevRequest, string)` creates the JSON body and optional protocol headers once per evaluation. `ParseResponse(JevValue, JevProtocolResponseContext)` converts a successful JSON response into `JevResponse`; its context includes sent JSON, selected model, attempt count, and request ID.

Built-in implementations are available as `JevProtocols.TypeSafe`, `JevProtocols.OpenRouter`, and `JevProtocols.Vercel`. They use the same interface and can be composed inside custom protocols. Custom protocols use HTTP POST with JSON. Authentication, retries, cancellation, HTTP error mapping, and response limits remain managed by the client.

Protocol headers must not set authentication/transport headers or overwrite custom authentication. For rotating credentials, implement `IJevAuthentication.ApplyAsync`. It runs for every attempt, must be thread-safe, honor cancellation, and only modify authentication headers. Do not put secrets in exception messages.

Authentication may add or refresh credentials such as `Authorization`, `X-Api-Key`, or custom tenant headers. The client rejects changes to the URI, method, content reference, content headers, HTTP version/policy, request options, and reserved HTTP/protocol headers before sending. Reserved headers include `Host`, `Accept`, `User-Agent`, connection/framing headers, and built-in gateway protocol headers. These guards apply to every provider and custom endpoint; they do not sandbox caller code or remove its thread-safety obligations.

The client validates response IDs, answer types, choice membership, and main numeric ranges. A custom parser must validate additional fields, distributions, and protocol-specific rules. Non-success HTTP responses never enter the parser. Parser exceptions become safe `JevInvalidResponseException` values without retries or exposed inner exceptions. Protocol implementations must be thread-safe and cannot retain mutable per-evaluation state.
