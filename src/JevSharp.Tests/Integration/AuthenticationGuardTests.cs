namespace JevSharp.Tests.Integration;

/// <summary>Verifies authentication ownership independently of the selected provider protocol.</summary>
public sealed class AuthenticationGuardTests
{
    /// <summary>Combines provider formats and forbidden request changes.</summary>
    public static IEnumerable<object[]> Mutations()
    {
        foreach (var provider in ProviderFixture.Names)
        {
            foreach (var mutation in new[] { "Host", "Content-Type", "Content-Length", "Accept", "Connection", "Protocol", "Uri", "Method", "Content", "Version", "VersionPolicy", "Options" })
            {
                yield return [provider, mutation];
            }
        }
    }

    /// <summary>Authentication cannot alter transport or protocol settings before the request is sent.</summary>
    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task Transport_mutations_are_rejected_without_sending(string provider, string mutation)
    {
        var options = new JevClientOptions();
        ProviderFixture.Configure(options, provider);
        var authentication = new CallbackAuthentication(request =>
        {
            switch (mutation)
            {
                case "Host": request.Headers.Host = "changed.example"; break;
                case "Content-Type": request.Content!.Headers.ContentType = new("text/plain"); break;
                case "Content-Length": request.Content!.Headers.ContentLength = 1; break;
                case "Accept": request.Headers.Accept.Clear(); break;
                case "Connection": request.Headers.ConnectionClose = true; break;
                case "Protocol": request.Headers.Add("ai-model-id", "changed-model"); break;
                case "Uri": request.RequestUri = new("https://changed.example"); break;
                case "Method": request.Method = HttpMethod.Get; break;
                case "Content":
                    request.Content!.Dispose();
                    request.Content = new StringContent("changed");
                    break;
                case "Version": request.Version = new(2, 0); break;
                case "VersionPolicy": request.VersionPolicy = HttpVersionPolicy.RequestVersionExact; break;
                case "Options": request.Options.Set(new HttpRequestOptionsKey<int>("JevSharp.Attempt"), 99); break;
            }
        });
        options.UseCustom(options.Endpoint!, options.ProtocolImplementation!, options.Model ?? "test-model", authentication);
        using (var handler = new StubHandler(ProviderFixture.Response(provider)))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.EvaluateAsync(Fixtures.One()));
                    Assert.DoesNotContain("changed", error.Message);
                    Assert.Empty(handler.Requests);
                }
            }
        }
    }

    /// <summary>Dynamic credential headers continue to work with every wire protocol.</summary>
    [Theory]
    [InlineData("TypeSafe")]
    [InlineData("OpenRouter")]
    [InlineData("Vercel")]
    [InlineData("Custom")]
    public async Task Authentication_headers_are_allowed(string provider)
    {
        var options = new JevClientOptions();
        ProviderFixture.Configure(options, provider);
        options.UseCustom(options.Endpoint!, options.ProtocolImplementation!, options.Model ?? "test-model",
            new CallbackAuthentication(request => request.Headers.Add("X-Rotating-Key", "test-key")));
        using (var handler = new StubHandler(ProviderFixture.Response(provider)))
        {
            using (var http = new HttpClient(handler))
            {
                using (var client = new JevClient(options, http))
                {
                    var response = await client.EvaluateAsync(Fixtures.One());
                    Assert.Equal(0.9, response.GetAnswer<NoulAnswer>("yes").Probability);
                    Assert.Equal("test-key", Assert.Single(handler.Requests).Headers["X-Rotating-Key"]);
                }
            }
        }
    }
}
