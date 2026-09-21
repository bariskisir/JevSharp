using System.Text.Json;

namespace JevSharp.Tests.Protocols;

/// <summary>Verifies public evaluation behavior at the HTTP boundary.</summary>
public sealed class ProtocolTests
{
    /// <summary>All providers send the four questions once and return normalized typed answers.</summary>
    [Theory]
    [InlineData(JevProtocol.TypeSafe, "https://api.typesafe.ai/v1/systemone", "jev-latest")]
    [InlineData(JevProtocol.OpenRouter, "https://openrouter.ai/api/alpha/decisions", "~typesafe/jev-latest")]
    [InlineData(JevProtocol.Vercel, "https://ai-gateway.vercel.sh/v4/ai/evaluation-model", "typesafe-ai/jev")]
    public async Task Mixed_questions_are_one_request(JevProtocol protocol, string endpoint, string model)
    {
        using (var handler = new StubHandler(protocol == JevProtocol.Vercel ? Fixtures.Vercel : Fixtures.Standard))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Fixtures.Options(protocol), http))
                {
                    var result = await client.EvaluateAsync(Fixtures.Mixed());
                    var captured = Assert.Single(handler.Requests);
                    Assert.Equal(endpoint, captured.Uri!.AbsoluteUri);
                    Assert.Equal("POST", captured.Method);
                    Assert.Equal("Bearer test-secret", captured.Headers["Authorization"]);
                    Assert.Equal("application/json", captured.Headers["Content-Type"]);
                    using (var body = JsonDocument.Parse(captured.Body))
                    {
                        var root = body.RootElement;
                        var questions = root.GetProperty("questions");
                        Assert.Equal(4, questions.EnumerateObject().Count());
                        Assert.Equal(49, root.GetProperty("state").GetProperty("amounts")[0].GetInt32());
                        Assert.True(root.GetProperty("state").GetProperty("eligible").GetBoolean());
                        Assert.Equal(JsonValueKind.Object, questions.GetProperty("department").GetProperty("criteria").GetProperty("billing").ValueKind);
                        Assert.Equal(JsonValueKind.Null, questions.GetProperty("department").GetProperty("criteria").GetProperty("technical").ValueKind);
                        Assert.Equal(JsonValueKind.Array, questions.GetProperty("urgency").GetProperty("instructions").ValueKind);
                        Assert.Equal(JsonValueKind.Object, questions.GetProperty("urgency").GetProperty("criteria")[1].ValueKind);
                        Assert.Equal(JsonValueKind.Null, questions.GetProperty("policy_supports_refund").GetProperty("criteria").GetProperty("false").ValueKind);
                        if (protocol == JevProtocol.Vercel)
                        {
                            Assert.False(root.TryGetProperty("model", out _));
                            Assert.Equal(model, captured.Headers["ai-model-id"]);
                            Assert.Equal("4", captured.Headers["ai-evaluation-model-specification-version"]);
                            Assert.Equal("0.0.1", captured.Headers["ai-gateway-protocol-version"]);
                            Assert.Equal("api-key", captured.Headers["ai-gateway-auth-method"]);
                            Assert.Equal("boolean", questions.GetProperty("refund_requested").GetProperty("type").GetString());
                            Assert.Null(result.Model);
                            Assert.Null(result.GetAnswer<ChoiceAnswer>("department").Confidence);
                            Assert.Null(result.GetAnswer<ScoreAnswer>("urgency").Legend);
                            Assert.Single(result.Warnings);
                            Assert.NotNull(result.ProviderMetadata);
                            Assert.NotNull(result.Rounding);
                            Assert.Null(result.Usage!.Cost);
                        }
                        else
                        {
                            Assert.Equal(new[] { "model", "questions", "state" }, root.EnumerateObject().Select(property => property.Name).Order());
                            Assert.Equal(model, root.GetProperty("model").GetString());
                            Assert.Equal("noul", questions.GetProperty("refund_requested").GetProperty("type").GetString());
                            Assert.Equal("jev-1.13.0", result.Model);
                            Assert.Equal(0.7, result.GetAnswer<ChoiceAnswer>("department").Confidence);
                            Assert.Equal(17, result.GetAnswer<ChoiceAnswer>("department").AdditionalData["futureAnswer"].GetInt32());
                            Assert.True(result.AdditionalData["future"].GetProperty("enabled").GetBoolean());
                            Assert.Equal(0.00001m, result.Usage!.Cost);
                            Assert.Equal(3, result.Usage.AdditionalData["cache_read_tokens"].GetInt32());
                        }
                    }
                    Assert.Equal(4, result.Answers.Count);
                    Assert.Equal("billing", result.GetAnswer<ChoiceAnswer>("department").Choice);
                    Assert.Equal(1.6, result.GetAnswer<ScoreAnswer>("urgency").Score);
                    Assert.Equal(0.95, result.GetAnswer<NoulAnswer>("refund_requested").Probability);
                    Assert.Equal(0.8, result.GetAnswer<NoulAnswer>("policy_supports_refund").Probability);
                    Assert.Equal(model, result.RequestedModel);
                    Assert.Equal(120, result.Usage!.InputTokens);
                    Assert.Equal(20, result.Usage.OutputTokens);
                    Assert.Equal("request-42", result.RequestId);
                }
            }
        }
    }

    /// <summary>Manual IDs and per-request overrides are sent without rewriting or allowlists.</summary>
    [Theory]
    [InlineData(JevProtocol.TypeSafe)]
    [InlineData(JevProtocol.OpenRouter)]
    [InlineData(JevProtocol.Vercel)]
    public async Task Model_precedence_and_manual_strings_are_preserved(JevProtocol protocol)
    {
        var options = Fixtures.Options(protocol);
        options.Model = "future/provider-model";
        using (var handler = new StubHandler(protocol == JevProtocol.Vercel ? Fixtures.Vercel : Fixtures.Standard))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    options.Model = "mutated-after-construction";
                    var configured = await client.EvaluateAsync(Fixtures.Mixed());
                    var overridden = await client.EvaluateAsync(Fixtures.Mixed() with { Model = "manual/request-model" });
                    Assert.Equal("future/provider-model", configured.RequestedModel);
                    Assert.Equal("manual/request-model", overridden.RequestedModel);
                    var captures = handler.Requests.ToArray();
                    for (var i = 0; i < captures.Length; i++)
                    {
                        var expected = i == 0 ? "future/provider-model" : "manual/request-model";
                        using (var body = JsonDocument.Parse(captures[i].Body))
                        {
                            Assert.Equal(expected, protocol == JevProtocol.Vercel
                                ? captures[i].Headers["ai-model-id"] : body.RootElement.GetProperty("model").GetString());
                        }
                    }
                }
            }
        }
    }

    /// <summary>String and array states retain their shape and are not split into multiple requests.</summary>
    [Theory]
    [InlineData("\"hello\"", JsonValueKind.String)]
    [InlineData("[\"message one\",{\"text\":\"message two\"}]", JsonValueKind.Array)]
    public async Task State_shape_is_preserved(string json, JsonValueKind kind)
    {
        using (var handler = new StubHandler(Fixtures.Single))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Fixtures.Options(JevProtocol.TypeSafe), http))
                {
                    await client.EvaluateAsync(Fixtures.One() with { State = JevValue.FromJson(json) });
                    using (var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body))
                    {
                        Assert.Equal(kind, body.RootElement.GetProperty("state").ValueKind);
                    }
                }
            }
        }
    }

    /// <summary>Custom URLs and authentication are used exactly as configured for every protocol.</summary>
    [Theory]
    [InlineData(JevProtocol.TypeSafe)]
    [InlineData(JevProtocol.OpenRouter)]
    [InlineData(JevProtocol.Vercel)]
    public async Task Custom_endpoints_preserve_full_url_and_headers(JevProtocol protocol)
    {
        var headers = new Dictionary<string, string> { ["X-Api-Key"] = "custom-secret", ["X-Tenant"] = "tenant-1" };
        var options = new JevClientOptions().UseCustom(new("http://localhost:8080/special/evaluate?revision=1"), protocol, "local-model", headers);
        headers["X-Api-Key"] = "mutated";
        using (var handler = new StubHandler(protocol == JevProtocol.Vercel ? Fixtures.Vercel : Fixtures.Standard))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    await client.EvaluateAsync(Fixtures.Mixed());
                    var captured = Assert.Single(handler.Requests);
                    Assert.Equal("http://localhost:8080/special/evaluate?revision=1", captured.Uri!.AbsoluteUri);
                    Assert.Equal("custom-secret", captured.Headers["X-Api-Key"]);
                    Assert.Equal("tenant-1", captured.Headers["X-Tenant"]);
                    Assert.False(captured.Headers.ContainsKey("Authorization"));
                }
            }
        }
    }

    /// <summary>Vercel provider options reach the gateway request body.</summary>
    [Fact]
    public async Task Vercel_provider_options_are_serialized()
    {
        using (var handler = new StubHandler(Fixtures.Vercel))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Fixtures.Options(JevProtocol.Vercel), http))
                {
                    await client.EvaluateAsync(Fixtures.Mixed() with { VercelProviderOptions = JevValue.FromJson("{\"gateway\":{\"user\":\"test\"}}") });
                    using (var json = JsonDocument.Parse(Assert.Single(handler.Requests).Body))
                    {
                        Assert.Equal("test", json.RootElement.GetProperty("providerOptions").GetProperty("gateway").GetProperty("user").GetString());
                    }
                }
            }
        }
    }

    /// <summary>Public constants identify the documented aliases and versions.</summary>
    [Fact]
    public void Model_constants_are_stable()
    {
        Assert.Equal("jev-latest", TypeSafeModels.Latest);
        Assert.Equal("jev-preview", TypeSafeModels.Preview);
        Assert.Equal("jev-1.13.0", TypeSafeModels.Jev1_13_0);
        Assert.Equal("~typesafe/jev-latest", OpenRouterModels.Latest);
        Assert.Equal("typesafe/jev-1.13", OpenRouterModels.Jev1_13);
        Assert.Equal("typesafe-ai/jev", VercelModels.Jev);
    }
}
