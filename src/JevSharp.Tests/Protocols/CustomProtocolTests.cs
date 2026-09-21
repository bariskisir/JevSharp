using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace JevSharp.Tests.Protocols;

/// <summary>Exercises caller-defined formats through the real HTTP and DI pipelines.</summary>
public sealed class CustomProtocolTests
{
    private const string MixedResponse = """{"results":{"department":"billing","urgency":1.6,"refund_requested":0.95,"policy_supports_refund":0.8}}""";

    /// <summary>Invalid UTF-8 must not be silently replaced before a custom parser sees the response.</summary>
    [Fact]
    public async Task Invalid_response_encoding_is_rejected_before_parsing()
    {
        var implementation = new TestProtocol();
        var protocol = new DelegateProtocol(implementation.PrepareRequest, implementation.ParseResponse);
        using (var handler = new StubHandler((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0x22, 0xFF, 0x22])
        })))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Options(protocol), http))
                {
                    await Assert.ThrowsAsync<JevInvalidResponseException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.Equal(0, protocol.Parses);
                    Assert.Single(handler.Requests);
                }
            }
        }
    }

    /// <summary>Custom normalization must still respect choice membership and score bounds.</summary>
    [Theory]
    [InlineData("billing", "unknown")]
    [InlineData("1.6", "3.0")]
    public async Task Custom_values_are_checked_against_original_questions(string original, string replacement)
    {
        using (var handler = new StubHandler(MixedResponse.Replace(original, replacement)))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Options(new TestProtocol()), http))
                {
                    await Assert.ThrowsAsync<JevInvalidResponseException>(() => client.EvaluateAsync(Fixtures.Mixed()));
                    Assert.Single(handler.Requests);
                }
            }
        }
    }

    /// <summary>Extension validation errors propagate locally without making an HTTP request.</summary>
    [Fact]
    public async Task Preparation_failure_never_reaches_transport()
    {
        var expected = new ArgumentException("Unsupported custom option.");
        var protocol = new DelegateProtocol((_, _) => throw expected, new TestProtocol().ParseResponse);
        using (var handler = new StubHandler("{}"))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Options(protocol), http))
                {
                    var error = await Assert.ThrowsAsync<ArgumentException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.Same(expected, error);
                    Assert.Empty(handler.Requests);
                    Assert.Equal(1, protocol.Preparations);
                }
            }
        }
    }

    /// <summary>Caller cancellation remains cancellation even when an extension fails while parsing.</summary>
    [Fact]
    public async Task Cancellation_during_parsing_takes_precedence()
    {
        using (var cancellation = new CancellationTokenSource())
        {
            var protocol = new DelegateProtocol(new TestProtocol().PrepareRequest, (_, _) =>
            {
                cancellation.Cancel();
                throw new JsonException();
            });
            using (var handler = new StubHandler("{}"))
            {
                using (var http = new HttpClient(handler))
                {
                    using (var client = new JevClient(Options(protocol), http))
                    {
                        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                            client.EvaluateAsync(Fixtures.One(), cancellation.Token));
                        Assert.Equal(cancellation.Token, error.CancellationToken);
                        Assert.Single(handler.Requests);
                    }
                }
            }
        }
    }

    /// <summary>Protocol headers cannot silently replace a custom credential.</summary>
    [Fact]
    public async Task Protocol_authentication_header_collision_fails_before_sending()
    {
        var protocol = new DelegateProtocol((_, _) => new(JevValue.FromJson("{}"))
        {
            Headers = new Dictionary<string, string> { ["X-Api-Key"] = "replacement" }
        }, new TestProtocol().ParseResponse);
        using (var handler = new StubHandler("{}"))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Options(protocol), http))
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.Empty(handler.Requests);
                }
            }
        }
    }

    /// <summary>The compiled sample maps structured state and mixed questions without built-in response metadata.</summary>
    [Fact]
    public async Task Custom_format_round_trips_mixed_questions()
    {
        using (var handler = new StubHandler(MixedResponse))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Options(new TestProtocol()), http))
                {
                    var result = await client.EvaluateAsync(Fixtures.Mixed() with { Model = "manual-model" });
                    var sent = Assert.Single(handler.Requests);
                    Assert.Equal("https://custom.example/evaluate", sent.Uri!.AbsoluteUri);
                    Assert.Equal("private-key", sent.Headers["X-Api-Key"]);
                    Assert.Equal("1", sent.Headers["X-Evaluation-Version"]);
                    using (var json = JsonDocument.Parse(sent.Body))
                    {
                        Assert.Equal("manual-model", json.RootElement.GetProperty("deployment").GetString());
                        Assert.Equal(4, json.RootElement.GetProperty("checks").EnumerateObject().Count());
                        Assert.True(json.RootElement.GetProperty("input").GetProperty("eligible").GetBoolean());
                        Assert.False(json.RootElement.TryGetProperty("questions", out _));
                    }
                    Assert.Equal("billing", result.GetAnswer<ChoiceAnswer>("department").Choice);
                    Assert.Equal(1.6, result.GetAnswer<ScoreAnswer>("urgency").Score);
                    Assert.Equal(0.95, result.GetAnswer<NoulAnswer>("refund_requested").Probability);
                    Assert.Equal("manual-model", result.RequestedModel);
                    Assert.Equal("request-42", result.RequestId);
                    Assert.Null(result.Usage);
                }
            }
        }
    }

    /// <summary>Preparation runs once and both body and headers survive later mutation across retries.</summary>
    [Fact]
    public async Task Retries_snapshot_protocol_output_and_refresh_authentication()
    {
        var headers = new Dictionary<string, string> { ["X-Format"] = "original" };
        var implementation = new TestProtocol();
        var protocol = new DelegateProtocol(
            (request, model) => implementation.PrepareRequest(request, model) with { Headers = headers },
            implementation.ParseResponse);
        var auth = new CountingAuthentication();
        var options = new JevClientOptions().UseCustom(new("https://custom.example/evaluate"), protocol, "model", auth);
        options.Retry.Delay = TimeSpan.Zero;
        var request = Fixtures.Mixed();
        using (var handler = new StubHandler((_, attempt, _) =>
        {
            headers["X-Format"] = "changed";
            ((Dictionary<string, JevQuestion>)request.Questions).Clear();
            return Task.FromResult(StubHandler.Json(attempt == 1 ? "busy" : MixedResponse,
                attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        }))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    var result = await client.EvaluateAsync(request);
                    Assert.Equal(4, result.Answers.Count);
                    Assert.Equal(1, protocol.Preparations);
                    Assert.Equal(1, protocol.Parses);
                    Assert.Equal(2, auth.Calls);
                    Assert.Equal(2, handler.Requests.Count);
                    Assert.Single(handler.Requests.Select(value => value.Body).Distinct());
                    Assert.All(handler.Requests, value => Assert.Equal("original", value.Headers["X-Format"]));
                }
            }
        }
    }

    /// <summary>Malformed or out-of-contract custom answers never trigger another paid request.</summary>
    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"results\":{}}")]
    [InlineData("{\"results\":{\"yes\":1.2}}")]
    [InlineData("{\"results\":{\"YES\":0.9}}")]
    [InlineData("{\"results\":{\"yes\":0.9,\"yes\":0.5}}")]
    public async Task Invalid_custom_responses_are_safe_and_not_retried(string body)
    {
        using (var handler = new StubHandler(body))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Options(new TestProtocol()), http))
                {
                    var error = await Assert.ThrowsAsync<JevInvalidResponseException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.Equal(1, error.Attempts);
                    Assert.Single(handler.Requests);
                    Assert.DoesNotContain(body, error.ToString());
                }
            }
        }
    }

    /// <summary>Exceptions thrown by parsers are not mistaken for retryable transport errors.</summary>
    [Fact]
    public async Task Parser_exceptions_are_redacted_and_not_retried()
    {
        var protocol = new DelegateProtocol(new TestProtocol().PrepareRequest,
            (_, _) => throw new HttpRequestException("private-response-data"));
        using (var handler = new StubHandler("{}"))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Options(protocol), http))
                {
                    var error = await Assert.ThrowsAsync<JevInvalidResponseException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.DoesNotContain("private-response-data", error.ToString());
                    Assert.Null(error.InnerException);
                    Assert.Single(handler.Requests);
                }
            }
        }
    }

    /// <summary>Protocols cannot take ownership of authentication or HTTP framing.</summary>
    [Theory]
    [InlineData("Authorization", "secret")]
    [InlineData("Content-Type", "text/plain")]
    [InlineData("Host", "different.example")]
    [InlineData("X-Format", "bad\r\nvalue")]
    public async Task Invalid_protocol_headers_fail_before_sending(string name, string value)
    {
        var protocol = new DelegateProtocol((_, _) => new(JevValue.FromJson("{}"))
        {
            Headers = new Dictionary<string, string> { [name] = value }
        }, new TestProtocol().ParseResponse);
        using (var handler = new StubHandler("{}"))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Options(protocol), http))
                {
                    await Assert.ThrowsAsync<ArgumentException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.Empty(handler.Requests);
                }
            }
        }
    }

    /// <summary>HTTP errors, cancellation, and size limits remain client responsibilities.</summary>
    [Theory]
    [InlineData("http")]
    [InlineData("size")]
    [InlineData("cancel")]
    public async Task Transport_guards_do_not_call_custom_parser(string scenario)
    {
        var implementation = new TestProtocol();
        var protocol = new DelegateProtocol(implementation.PrepareRequest, implementation.ParseResponse);
        var options = Options(protocol);
        options.MaxResponseBytes = scenario == "size" ? 1 : 1024;
        using (var cancellation = new CancellationTokenSource())
        {
            using (var handler = new StubHandler((_, _, token) =>
            {
                if (scenario == "cancel")
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                }
                return Task.FromResult(StubHandler.Json("{}", scenario == "http" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK));
            }))
            {
                using (var http = new HttpClient(handler))
                {
                    using (var client = new JevClient(options, http))
                    {
                        var error = await Record.ExceptionAsync(() => client.EvaluateAsync(Fixtures.One(), cancellation.Token));
                        switch (scenario)
                        {
                            case "http":
                                Assert.IsType<JevAuthenticationException>(error);
                                break;
                            case "size":
                                Assert.IsType<JevInvalidResponseException>(error);
                                break;
                            default:
                                Assert.IsAssignableFrom<OperationCanceledException>(error);
                                break;
                        }
                        Assert.Equal(0, protocol.Parses);
                        Assert.Single(handler.Requests);
                    }
                }
            }
        }
    }

    /// <summary>One protocol instance works concurrently through DI with isolated evaluation contexts.</summary>
    [Fact]
    public async Task Shared_protocol_supports_concurrent_DI_evaluations()
    {
        var services = new ServiceCollection();
        var handler = new StubHandler("""{"results":{"yes":0.9}}""");
        services.AddJev(options => options.UseCustom(new("https://custom.example/evaluate"),
            new TestProtocol(), "default-model", new Dictionary<string, string>()))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using (var provider = services.BuildServiceProvider())
        {
            var client = provider.GetRequiredService<IJevClient>();
            var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(index =>
                client.EvaluateAsync(Fixtures.One() with { Model = $"model-{index}" })));
            Assert.Equal(12, results.Select(value => value.RequestedModel).Distinct().Count());
            Assert.Equal(12, handler.Requests.Count);
            Assert.All(results, result => Assert.Equal(0.9, result.GetAnswer<NoulAnswer>("yes").Probability));
        }
    }

    /// <summary>Creates a custom configuration with a stable credential.</summary>
    private static JevClientOptions Options(IJevProtocol protocol) => new JevClientOptions().UseCustom(
        new("https://custom.example/evaluate"), protocol, "model",
        new Dictionary<string, string> { ["X-Api-Key"] = "private-key" });
}
