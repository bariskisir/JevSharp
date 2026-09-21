using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace JevSharp.Tests.Integration;

/// <summary>Checks nested failover across provider boundaries.</summary>
public sealed class NestedFailoverTests
{
    /// <summary>An exhausted inner chain continues to the outer fallback for every provider.</summary>
    [Theory]
    [InlineData("TypeSafe")]
    [InlineData("OpenRouter")]
    [InlineData("Vercel")]
    [InlineData("Custom")]
    public async Task Exhausted_inner_chain_reaches_outer_fallback(string fallback)
    {
        var services = new ServiceCollection();
        var first = new StubHandler("busy", HttpStatusCode.ServiceUnavailable);
        var second = new StubHandler("busy", HttpStatusCode.TooManyRequests);
        var third = new StubHandler(ProviderFixture.Response(fallback));
        Register(services, "a", "TypeSafe", first);
        Register(services, "b", "OpenRouter", second);
        Register(services, "c", fallback, third);
        services.AddJevFailover("inner", "a", "b");
        services.AddJevFailover("outer", "inner", "c");
        using (var provider = services.BuildServiceProvider())
        {
            using (var client = provider.GetRequiredService<IJevClientFactory>().CreateClient("outer"))
            {
                var response = await client.EvaluateAsync(Fixtures.One());
                Assert.Equal(0.9, response.GetAnswer<NoulAnswer>("yes").Probability);
            }
        }
        Assert.Single(first.Requests);
        Assert.Single(second.Requests);
        Assert.Single(third.Requests);
    }

    /// <summary>Permanent inner failures stop all outer fallbacks.</summary>
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(422)]
    [InlineData(501)]
    public async Task Permanent_inner_failure_stops_outer_chain(int status)
    {
        var services = new ServiceCollection();
        var first = new StubHandler("failure", (HttpStatusCode)status);
        var second = new StubHandler(Fixtures.Single);
        var third = new StubHandler(ProviderFixture.Response("Vercel"));
        Register(services, "a", "Custom", first);
        Register(services, "b", "OpenRouter", second);
        Register(services, "c", "Vercel", third);
        services.AddJevFailover("inner", "a", "b");
        services.AddJevFailover("outer", "inner", "c");
        using (var provider = services.BuildServiceProvider())
        {
            using (var client = provider.GetRequiredService<IJevClientFactory>().CreateClient("outer"))
            {
                await Assert.ThrowsAnyAsync<JevApiException>(() => client.EvaluateAsync(Fixtures.One()));
            }
        }
        Assert.Empty(second.Requests);
        Assert.Empty(third.Requests);
    }

    /// <summary>Nested exhaustion preserves ordered leaf failures and total attempts.</summary>
    [Fact]
    public async Task Nested_exhaustion_flattens_failures()
    {
        var services = new ServiceCollection();
        Register(services, "a", "TypeSafe", new StubHandler("busy", HttpStatusCode.ServiceUnavailable));
        Register(services, "b", "OpenRouter", new StubHandler("busy", HttpStatusCode.TooManyRequests));
        Register(services, "c", "Vercel", new StubHandler("busy", HttpStatusCode.GatewayTimeout));
        services.AddJevFailover("inner", "a", "b");
        services.AddJevFailover("outer", "inner", "c");
        using (var provider = services.BuildServiceProvider())
        {
            using (var client = provider.GetRequiredService<IJevClientFactory>().CreateClient("outer"))
            {
                var error = await Assert.ThrowsAsync<JevFailoverException>(() => client.EvaluateAsync(Fixtures.One()));
                Assert.Equal(new[] { "TypeSafe", "OpenRouter", "Vercel" }, error.Failures.Select(failure => failure.Provider));
                Assert.Equal(3, error.Attempts);
            }
        }
    }

    /// <summary>Caller list changes do not remove the chain's owned clients.</summary>
    [Fact]
    public async Task Constructor_snapshots_client_list()
    {
        var services = new ServiceCollection();
        Register(services, "a", "TypeSafe", new StubHandler("busy", HttpStatusCode.ServiceUnavailable));
        Register(services, "b", "OpenRouter", new StubHandler(Fixtures.Single));
        using (var provider = services.BuildServiceProvider())
        {
            var factory = provider.GetRequiredService<IJevClientFactory>();
            var clients = new List<IJevClient> { factory.CreateClient("a"), factory.CreateClient("b") };
            using (var client = new FailoverJevClient(clients))
            {
                clients.Clear();
                var response = await client.EvaluateAsync(Fixtures.One());
                Assert.Equal(0.9, response.GetAnswer<NoulAnswer>("yes").Probability);
            }
        }
    }

    /// <summary>Registers an isolated named client.</summary>
    private static void Register(ServiceCollection services, string name, string provider, StubHandler handler)
    {
        services.AddJev(name, options => ProviderFixture.Configure(options, provider))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
    }
}
