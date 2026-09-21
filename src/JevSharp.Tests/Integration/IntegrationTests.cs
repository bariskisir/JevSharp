using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace JevSharp.Tests.Integration;

/// <summary>Verifies integration, resource ownership, concurrency, and diagnostic boundaries.</summary>
public sealed class IntegrationTests
{
    /// <summary>Custom auth headers with an ai prefix remain supported and are redacted like other credentials.</summary>
    [Fact]
    public async Task Custom_ai_prefixed_credentials_are_supported_and_redacted()
    {
        var options = new JevClientOptions().UseCustom(new("https://proxy.example/evaluate"), JevProtocol.TypeSafe,
            "custom-model", new Dictionary<string, string> { ["ai-api-key"] = "private-custom-key" });
        using (var handler = new StubHandler("private-custom-key", HttpStatusCode.Unauthorized))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    var error = await Assert.ThrowsAsync<JevAuthenticationException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.Equal("private-custom-key", Assert.Single(handler.Requests).Headers["ai-api-key"]);
                    Assert.Equal("[REDACTED]", error.Details);
                }
            }
        }
    }

    /// <summary>The main package registers an injectable client and snapshots options.</summary>
    [Fact]
    public async Task Default_registration_is_resolvable_and_snapshotted()
    {
        var services = new ServiceCollection();
        JevClientOptions? captured = null;
        var handler = new StubHandler(Fixtures.Single);
        services.AddJev(options =>
        {
            captured = options.UseTypeSafe("first-key", "pinned-model");
            options.Retry.MaxAttempts = 1;
        }).ConfigurePrimaryHttpMessageHandler(() => handler);
        captured!.UseOpenRouter("changed-key", "changed-model");
        captured.Retry.MaxAttempts = 100;
        using (var provider = services.BuildServiceProvider())
        {
            var client = provider.GetRequiredService<IJevClient>();
            var result = await client.EvaluateAsync(Fixtures.One());
            var sent = Assert.Single(handler.Requests);
            Assert.Equal("api.typesafe.ai", sent.Uri!.Host);
            Assert.Equal("Bearer first-key", sent.Headers["Authorization"]);
            Assert.Equal("pinned-model", result.RequestedModel);
        }
    }

    /// <summary>Named clients use separate provider/authentication settings and reject unknown names.</summary>
    [Fact]
    public async Task Named_clients_are_isolated()
    {
        var services = new ServiceCollection();
        var first = new StubHandler(Fixtures.Single);
        var second = new StubHandler(Fixtures.Single);
        services.AddJev("first", o => o.UseTypeSafe("first-key"))
            .ConfigurePrimaryHttpMessageHandler(() => first);
        services.AddJev("second", o => o.UseOpenRouter("second-key"))
            .ConfigurePrimaryHttpMessageHandler(() => second);
        Assert.Throws<ArgumentException>(() => services.AddJev("first", o => o.UseTypeSafe("duplicate")));
        using (var provider = services.BuildServiceProvider())
        {
            var factory = provider.GetRequiredService<IJevClientFactory>();
            Assert.Throws<ArgumentException>(() => factory.CreateClient("First"));
            using (var a = factory.CreateClient("first"))
            {
                using (var b = factory.CreateClient("second"))
                {
                    await Task.WhenAll(a.EvaluateAsync(Fixtures.One()), b.EvaluateAsync(Fixtures.One()));
                }
            }
            Assert.Equal("Bearer first-key", Assert.Single(first.Requests).Headers["Authorization"]);
            Assert.Equal("Bearer second-key", Assert.Single(second.Requests).Headers["Authorization"]);
        }
    }

    /// <summary>Per-request models and authentication remain isolated under concurrent use.</summary>
    [Fact]
    public async Task Concurrent_evaluations_do_not_mutate_default_headers()
    {
        using (var handler = new StubHandler(Fixtures.Single))
        {
            using (var http = new HttpClient(handler))
            {
                using (var first = new JevClient(new JevClientOptions().UseTypeSafe("first-key"), http))
                {
                    using (var second = new JevClient(new JevClientOptions().UseOpenRouter("second-key"), http))
                    {
                        var requests = Enumerable.Range(0, 40).Select(async i =>
                        {
                            var client = i % 2 == 0 ? first : second;
                            var result = await client.EvaluateAsync(Fixtures.One() with { Model = $"model-{i}" });
                            Assert.Equal($"model-{i}", result.RequestedModel);
                        });
                        await Task.WhenAll(requests);
                        Assert.Equal(40, handler.Requests.Count);
                        Assert.Empty(http.DefaultRequestHeaders);
                        foreach (var capture in handler.Requests)
                        {
                            Assert.Equal(capture.Uri!.Host == "api.typesafe.ai" ? "Bearer first-key" : "Bearer second-key", capture.Headers["Authorization"]);
                        }
                    }
                }
            }
        }
    }

    /// <summary>Disposing a caller-owned client does not dispose its HTTP transport.</summary>
    [Fact]
    public async Task Injected_http_client_remains_open()
    {
        using (var handler = new StubHandler(Fixtures.Single))
        {
            using (var http = new HttpClient(handler))
            {
                var client = new JevClient(Fixtures.Options(JevProtocol.TypeSafe), http);
                client.Dispose();
                Assert.False(handler.Disposed);
                await Assert.ThrowsAsync<ObjectDisposedException>(() => client.EvaluateAsync(Fixtures.One()));
                using (var another = new JevClient(Fixtures.Options(JevProtocol.TypeSafe), http))
                {
                    await another.EvaluateAsync(Fixtures.One());
                }
            }
        }
    }

    /// <summary>Explicit HTTP ownership releases the transport exactly when the SDK client is disposed.</summary>
    [Fact]
    public void Owned_http_client_is_disposed()
    {
        var handler = new StubHandler(Fixtures.Single);
        var http = new HttpClient(handler);
        var client = JevClient.CreateOwned(Fixtures.Options(JevProtocol.TypeSafe), http);
        client.Dispose();
        client.Dispose();
        Assert.True(handler.Disposed);
    }

    /// <summary>Custom auth executes again on every attempt and credentials stay out of diagnostics.</summary>
    [Fact]
    public async Task Dynamic_auth_refreshes_and_error_details_are_redacted()
    {
        var auth = new RotatingAuthentication();
        var options = new JevClientOptions().UseCustom(new("https://proxy.example/evaluate"), JevProtocol.TypeSafe, "manual", auth);
        options.Retry.Delay = TimeSpan.Zero;
        var logger = new CaptureLogger();
        using (var handler = new StubHandler((_, attempt, _) => Task.FromResult(StubHandler.Json(
            $"{{\"error\":\"key-{attempt} PRIVATE-CONTENT\"}}", HttpStatusCode.ServiceUnavailable))))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http, logger))
                {
                    var error = await Assert.ThrowsAsync<JevApiException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.Equal(3, auth.Calls);
                    Assert.Equal(new[] { "key-1", "key-2", "key-3" }, handler.Requests.Select(r => r.Headers["X-Api-Key"]));
                    Assert.Contains("[REDACTED]", error.Details!);
                    Assert.DoesNotContain("key-3", error.Details!);
                    Assert.DoesNotContain("PRIVATE-CONTENT", error.ToString());
                    Assert.All(logger.Messages, message =>
                    {
                        Assert.DoesNotContain("key-", message);
                        Assert.DoesNotContain("PRIVATE-CONTENT", message);
                    });
                }
            }
        }
    }

    /// <summary>Bearer tokens echoed in error bodies and request IDs are scrubbed.</summary>
    [Fact]
    public async Task Echoed_bearer_tokens_are_redacted()
    {
        using (var handler = new StubHandler((_, _, _) =>
        {
            var response = StubHandler.Json("Bearer test-secret test-secret", HttpStatusCode.Unauthorized);
            response.Headers.Remove("x-request-id");
            response.Headers.Add("x-request-id", "echo-test-secret");
            return Task.FromResult(response);
        }))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Fixtures.Options(JevProtocol.TypeSafe), http))
                {
                    var error = await Assert.ThrowsAsync<JevAuthenticationException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.DoesNotContain("test-secret", error.Details!);
                    Assert.DoesNotContain("test-secret", error.RequestId!);
                    Assert.DoesNotContain("test-secret", error.ToString());
                }
            }
        }
    }

    /// <summary>A cancellation requested before evaluation makes no HTTP request.</summary>
    [Fact]
    public async Task Pre_canceled_token_makes_no_request()
    {
        using (var cancellation = new CancellationTokenSource())
        {
            using (var handler = new StubHandler(Fixtures.Single))
            {
                using (var http = new HttpClient(handler))
                {
                    using (var client = new JevClient(Fixtures.Options(JevProtocol.TypeSafe), http))
                    {
                        cancellation.Cancel();
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.EvaluateAsync(Fixtures.One(), cancellation.Token));
                        Assert.Empty(handler.Requests);
                    }
                }
            }
        }
    }
}
