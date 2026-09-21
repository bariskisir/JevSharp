using System.Net;

namespace JevSharp.Tests.Validation;

/// <summary>Verifies malformed requests and responses fail deterministically.</summary>
public sealed class ValidationTests
{
    /// <summary>Invalid top-level states are rejected before sending.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("true")]
    public async Task Invalid_state_never_reaches_transport(string json)
    {
        using (var handler = new StubHandler(Fixtures.Single))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Fixtures.Options(JevProtocol.TypeSafe), http))
                {
                    await Assert.ThrowsAsync<ArgumentException>(() => client.EvaluateAsync(Fixtures.One() with { State = JevValue.FromJson(json) }));
                    Assert.Empty(handler.Requests);
                }
            }
        }
    }

    /// <summary>All structural validation occurs before network activity.</summary>
    [Theory]
    [InlineData("empty")]
    [InlineData("empty-id")]
    [InlineData("empty-choice")]
    [InlineData("large-choice")]
    [InlineData("short-score")]
    [InlineData("large-score")]
    [InlineData("scalar-instructions")]
    [InlineData("model")]
    [InlineData("provider-options")]
    public async Task Invalid_request_fails_locally(string scenario)
    {
        var one = Fixtures.One();
        var request = scenario switch
        {
            "empty" => one with { Questions = new Dictionary<string, JevQuestion>() },
            "empty-id" => one with { Questions = new Dictionary<string, JevQuestion> { [""] = new NoulQuestion("Question") } },
            "empty-choice" => one with { Questions = new Dictionary<string, JevQuestion> { ["yes"] = new ChoiceQuestion("Choose", new Dictionary<string, JevValue>()) } },
            "large-choice" => one with { Questions = new Dictionary<string, JevQuestion> { ["yes"] = new ChoiceQuestion("Choose", Enumerable.Range(0, 256).ToDictionary(n => n.ToString(), _ => JevValue.Null)) } },
            "short-score" => one with { Questions = new Dictionary<string, JevQuestion> { ["yes"] = new ScoreQuestion("Rate", ["Only"]) } },
            "large-score" => one with { Questions = new Dictionary<string, JevQuestion> { ["yes"] = new ScoreQuestion("Rate", Enumerable.Repeat<JevValue>("Level", 11).ToArray()) } },
            "scalar-instructions" => one with { Questions = new Dictionary<string, JevQuestion> { ["yes"] = new NoulQuestion(JevValue.FromObject(42)) } },
            "model" => one with { Model = "  " },
            _ => one with { VercelProviderOptions = JevValue.FromJson("{}") }
        };
        using (var handler = new StubHandler(Fixtures.Single))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Fixtures.Options(JevProtocol.TypeSafe), http))
                {
                    await Assert.ThrowsAsync<ArgumentException>(() => client.EvaluateAsync(request));
                    Assert.Empty(handler.Requests);
                }
            }
        }
    }

    /// <summary>Explicit nullable instructions are retained where supported and rejected by Vercel.</summary>
    [Theory]
    [InlineData(JevProtocol.TypeSafe)]
    [InlineData(JevProtocol.OpenRouter)]
    [InlineData(JevProtocol.Vercel)]
    public async Task Null_instructions_follow_provider_contract(JevProtocol protocol)
    {
        var request = new JevRequest("Text", new Dictionary<string, JevQuestion> { ["yes"] = new NoulQuestion(JevValue.Null) });
        using (var handler = new StubHandler(Fixtures.Single))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Fixtures.Options(protocol), http))
                {
                    if (protocol == JevProtocol.Vercel)
                    {
                        await Assert.ThrowsAsync<ArgumentException>(() => client.EvaluateAsync(request));
                        Assert.Empty(handler.Requests);
                    }
                    else
                    {
                        await client.EvaluateAsync(request);
                        Assert.Contains("\"instructions\":null", Assert.Single(handler.Requests).Body);
                    }
                }
            }
        }
    }

    /// <summary>Malformed answers are rejected without retrying a successful HTTP response.</summary>
    [Theory]
    [InlineData("not-json")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("duplicate")]
    [InlineData("case")]
    [InlineData("type")]
    [InlineData("range")]
    [InlineData("string")]
    [InlineData("missing-model")]
    [InlineData("negative-usage")]
    public async Task Invalid_responses_throw_without_retries(string scenario)
    {
        var response = scenario switch
        {
            "not-json" => "<html>Error</html>",
            "missing" => Fixtures.Single.Replace("\"yes\":{\"type\":\"noul\",\"noul\":0.9}", ""),
            "extra" => Fixtures.Single.Replace("\"answers\":{", "\"answers\":{\"extra\":{\"type\":\"noul\",\"noul\":0.5},"),
            "duplicate" => Fixtures.Single.Replace("\"answers\":{", "\"answers\":{\"yes\":{\"type\":\"noul\",\"noul\":0.5},"),
            "case" => Fixtures.Single.Replace("\"yes\"", "\"Yes\""),
            "type" => Fixtures.Single.Replace("\"type\":\"noul\"", "\"type\":\"score\""),
            "range" => Fixtures.Single.Replace("0.9", "1.1"),
            "string" => Fixtures.Single.Replace("0.9", "\"0.9\""),
            "missing-model" => Fixtures.Single.Replace("\"model\":\"jev-1.13.0\",", ""),
            _ => Fixtures.Single.Replace("\"input_tokens\":1", "\"input_tokens\":-1")
        };
        using (var handler = new StubHandler(response))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Fixtures.Options(JevProtocol.TypeSafe), http))
                {
                    var error = await Assert.ThrowsAsync<JevInvalidResponseException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.Equal(1, error.Attempts);
                    Assert.Equal("request-42", error.RequestId);
                    Assert.Single(handler.Requests);
                    Assert.DoesNotContain(response, error.ToString());
                }
            }
        }
    }

    /// <summary>Choice membership, distributions, and score ranges are checked against the request.</summary>
    [Theory]
    [InlineData("\"choice\":\"billing\"", "\"choice\":\"unknown\"")]
    [InlineData("\"billing\":0.8", "\"billing\":1.2")]
    [InlineData("\"technical\":0.2", "\"unknown\":0.2")]
    [InlineData("\"score\":1.6", "\"score\":3")]
    [InlineData("\"confidence\":0.7", "\"confidence\":-0.1")]
    public async Task Invalid_numeric_or_choice_answers_are_rejected(string original, string replacement)
    {
        using (var handler = new StubHandler(Fixtures.Standard.Replace(original, replacement)))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Fixtures.Options(JevProtocol.TypeSafe), http))
                {
                    await Assert.ThrowsAsync<JevInvalidResponseException>(() => client.EvaluateAsync(Fixtures.Mixed()));
                }
            }
        }
    }

    /// <summary>Rounded probability sums are accepted without enforcing exact equality to one.</summary>
    [Fact]
    public async Task Rounded_probabilities_are_accepted()
    {
        using (var handler = new StubHandler(Fixtures.Standard.Replace("\"technical\":0.2", "\"technical\":0.19")))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Fixtures.Options(JevProtocol.TypeSafe), http))
                {
                    var result = await client.EvaluateAsync(Fixtures.Mixed());
                    Assert.Equal(0.19, result.GetAnswer<ChoiceAnswer>("department").Probabilities!["technical"]);
                    Assert.Throws<KeyNotFoundException>(() => result.GetAnswer<NoulAnswer>("absent"));
                    Assert.Throws<InvalidOperationException>(() => result.GetAnswer<NoulAnswer>("department"));
                }
            }
        }
    }

    /// <summary>Transport limits prevent excessive successful response allocation.</summary>
    [Fact]
    public async Task Oversized_response_is_rejected()
    {
        var options = Fixtures.Options(JevProtocol.TypeSafe);
        options.MaxResponseBytes = 10;
        using (var handler = new StubHandler(Fixtures.Single))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    await Assert.ThrowsAsync<JevInvalidResponseException>(() => client.EvaluateAsync(Fixtures.One()));
                }
            }
        }
    }

    /// <summary>Permanent HTTP errors expose specific exceptions and are never retried.</summary>
    [Theory]
    [InlineData(400, typeof(JevValidationException))]
    [InlineData(401, typeof(JevAuthenticationException))]
    [InlineData(402, typeof(JevApiException))]
    [InlineData(403, typeof(JevAuthorizationException))]
    [InlineData(404, typeof(JevApiException))]
    [InlineData(413, typeof(JevApiException))]
    [InlineData(422, typeof(JevValidationException))]
    public async Task Permanent_errors_are_typed(int status, Type expected)
    {
        using (var handler = new StubHandler("not json", (HttpStatusCode)status))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(Fixtures.Options(JevProtocol.TypeSafe), http))
                {
                    var error = await Assert.ThrowsAsync(expected, () => client.EvaluateAsync(Fixtures.One()));
                    var api = Assert.IsAssignableFrom<JevApiException>(error);
                    Assert.Equal(status, (int)api.StatusCode);
                    Assert.Equal("not json", api.Details);
                    Assert.Equal(1, api.Attempts);
                    Assert.Single(handler.Requests);
                }
            }
        }
    }

    /// <summary>Configuration and authentication header validation prevent malformed requests.</summary>
    [Fact]
    public void Invalid_configuration_and_headers_fail_early()
    {
        Assert.Throws<ArgumentException>(() => new JevClient(new()));
        Assert.Throws<ArgumentException>(() => new BearerAuthentication("\r\nkey"));
        Assert.Throws<ArgumentException>(() => new HeaderAuthentication(new Dictionary<string, string> { ["Host"] = "elsewhere" }));
        Assert.Throws<ArgumentException>(() => new HeaderAuthentication(new Dictionary<string, string> { ["X-Api-Key"] = "key\r\nextra" }));
        Assert.Throws<ArgumentException>(() => new HeaderAuthentication(new Dictionary<string, string>
        {
            ["X-Api-Key"] = "first",
            ["x-api-key"] = "second"
        }));
        Assert.Throws<ArgumentException>(() => new JevClient(new JevClientOptions().UseCustom(new("ftp://localhost/eval"), JevProtocol.TypeSafe, "model", new Dictionary<string, string>())));
        var options = Fixtures.Options(JevProtocol.TypeSafe);
        options.Retry.MaxAttempts = 0;
        Assert.Throws<ArgumentException>(() => new JevClient(options));
        options.Retry.MaxAttempts = 1;
        options.Model = " ";
        Assert.Throws<ArgumentException>(() => new JevClient(options));
    }
}
