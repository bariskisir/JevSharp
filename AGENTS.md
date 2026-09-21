# JevSharp Repository Guide

## Purpose

JevSharp is a typed .NET 10 SDK for evaluating a shared state against Choice, Score, and Noul questions. It supports the TypeSafe API, OpenRouter, Vercel AI Gateway, and compatible custom endpoints.

Keep the SDK focused on Jev evaluation. Provider-specific request and response differences belong in protocol implementations, while the public surface remains provider-neutral.

## Repository layout

- `src/JevSharp.slnx` is the solution entry point.
- `src/JevSharp.Abstractions` contains public contracts, requests, responses, protocols, authentication interfaces, model constants, and typed exceptions.
- `src/JevSharp.Core` contains HTTP execution, retry handling, protocol codecs, built-in protocols, authentication implementations, and client configuration.
- `src/JevSharp` contains the public dependency-injection integration.
- `src/JevSharp.Tests` contains unit and integration tests. Tests are grouped by integration, protocol, reliability, validation, live, and support concerns.
- `src/Samples` contains console and ASP.NET Core sample applications. Samples are not NuGet packages.
- `src/scripts` contains release-version validation, source convention checks, and package verification scripts.
- `docs` contains user documentation. Keep the root README short and link to topic documents here.
- `assets` contains repository and NuGet package visual assets.
- `artifacts` is generated output and must not be treated as source.

## Project boundaries

- Place public, reusable DTOs and interfaces in `JevSharp.Abstractions`.
- Place transport behavior and built-in implementations in `JevSharp.Core`.
- Place `IServiceCollection` and `IHttpClientBuilder` extensions in `JevSharp`.
- Keep dependencies directed from `JevSharp` to `JevSharp.Core` to `JevSharp.Abstractions`; do not reverse those project references.
- Add sample-only and test-only dependencies only to their respective projects.
- Add package versions only in `Directory.Packages.props`. Do not assign package versions in individual project files.

## SDK behavior

- An evaluation sends one state and a complete, case-sensitive question map in one HTTP request per attempt.
- Choice, Score, and Noul questions may be mixed. Preserve structured JSON values and explicit JSON nulls; do not serialize object state as a JSON string.
- Preserve provider-reported values. Do not infer missing usage, cost, model, probability distribution, confidence, or metadata fields.
- Built-in model constants are conveniences. Manual provider-supported model IDs and per-request overrides must pass through unchanged after basic local validation.
- Built-in and custom protocols must be thread-safe. Prepare a request once per evaluation and reuse the snapshotted body on retries.
- Keep authentication, retries, cancellation, response-size limits, and HTTP error mapping in the client layer. Custom protocols own only their wire format and response mapping.
- Keep custom endpoint URL and authentication explicit. Do not add credentials automatically for custom endpoints.
- Preserve the typed exception hierarchy and keep exception messages safe for logs. Optional provider error details may contain application data and require caller care.
- Default retries are three total attempts with a five-second delay. Retry only documented transient HTTP, transport, and timeout failures. Caller cancellation, permanent provider failures, and invalid successful responses must stop immediately.

## Dependency injection and resilience

- `AddJev` registers a direct or named client backed by `IHttpClientFactory`; configured options are validated and snapshotted at registration.
- The unnamed registration supplies `IJevClient`; named registrations are resolved through `IJevClientFactory` and are caller-owned disposable wrappers.
- Failover is explicit through `AddJevFailover`. Continue to a later client only after transient transport failures, timeouts, rate limits, retryable server responses, circuit-open responses, or local concurrency rejections. Authentication, authorization, validation, invalid-response, and cancellation failures must stop the chain.
- `ConfigureJevResilience` uses `Microsoft.Extensions.Http.Resilience` for optional circuit breaking and local concurrency limiting. JevClient remains the only owner of semantic retries and attempt timeouts. Do not add deprecated `Microsoft.Extensions.Http.Polly` or a second HTTP retry/timeout policy.

## Code conventions

- Target `net10.0`, enable nullable reference types, and treat warnings as errors.
- Write code, comments, API documentation, samples, scripts, and user documentation in English.
- Use file-scoped namespaces whose names mirror the containing project and folder structure.
- Use four spaces in C# and two spaces in project, solution, JSON, and YAML files. Use LF line endings, UTF-8, final newlines, and no trailing whitespace.
- Prefer immutable `record` types for value contracts. Use classes for resource ownership, mutable behavior, or identity-bearing services.
- Keep one declared type per C# file. Do not nest declared types. Name each file after its type.
- Use explicit `Main` methods; do not use top-level statements in source, samples, or generated package-consumer code.
- Use braces for all control-flow blocks and scoped `using (...) { }` blocks rather than using declarations.
- Add XML documentation to public API declarations. Document meaningful parameters, return values, side effects, and exceptions.
- Use `ArgumentNullException.ThrowIfNull` and `ArgumentException.ThrowIfNullOrWhiteSpace` where appropriate. Validate public input before network activity.
- Do not change public contracts, provider wire formats, package metadata, or model constants without matching tests and documentation updates.

## Security and privacy

- Never commit, print, log, or document real API keys, tokens, credentials, or sensitive request/response payloads.
- Product code and samples accept credentials directly from the caller. Only opt-in live tests may read environment variables.
- Do not log request bodies, response bodies, authentication headers, endpoint query values, or secrets.
- Keep owned HTTP clients configured without redirects. Preserve redaction of known credentials from error details and request identifiers.
- Treat custom authentication implementations as untrusted caller code: they may modify authentication headers only and must remain thread-safe.

## Tests and validation

- Use deterministic fake HTTP handlers and controllable clocks for ordinary tests. Cover request formatting, response parsing, validation, retries, cancellation, ownership, and exception mapping when changing relevant behavior.
- Live tests require explicit opt-in and provider credentials. They may incur provider charges and must not assert exact model probabilities.
- Keep tests under the namespace that matches their folder. Share test infrastructure through `Tests.Support` rather than production code.
- Run the relevant checks before completing changes:

```shell
dotnet restore src/JevSharp.slnx
dotnet format src/JevSharp.slnx --verify-no-changes --no-restore
powershell -ExecutionPolicy Bypass -File src/scripts/Check-Style.ps1
dotnet build src/JevSharp.slnx -c Release --no-restore -p:JevSharpPackageVersion=0.1.0
dotnet test src/JevSharp.slnx -c Release --no-build
powershell -ExecutionPolicy Bypass -File src/scripts/Verify-Packages.ps1 -Version 0.1.0
```

## Packaging and releases

- The only published package is `JevSharp`. It contains the Abstractions and Core assemblies, so consumers install and search for one package.
- `Directory.Build.props` supplies shared package metadata, README, license, icon, XML documentation, symbols, Source Link metadata, and deterministic build settings. Keep package content limited to intended package assets.
- A packable project requires `JevSharpPackageVersion`; ordinary builds do not. Use the same version for all packages.
- Release tags use `vMAJOR.MINOR.PATCH`, optionally followed by a SemVer prerelease suffix. `Get-ReleaseVersion.ps1` removes the leading `v` after validation.
- `ci.yml` restores, formats, checks conventions, builds, tests, verifies packages, and uploads artifacts on Ubuntu and Windows.
- `publish.yml` runs when a `v*` tag is pushed. It validates the tag, reuses verified Ubuntu artifacts, and publishes through NuGet Trusted Publishing with GitHub OIDC.
- Do not add a long-lived NuGet API key. Preserve `id-token: write` only on the publishing job and keep package IDs, repository metadata, and trusted-publisher settings aligned with `docs/PUBLISHING.md`.
- Do not replace a published package version or move a published release tag. Publish a corrected release under a new version.
