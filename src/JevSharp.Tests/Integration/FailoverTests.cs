using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace JevSharp.Tests.Integration;

/// <summary>Verifies explicit named-provider failover behavior and registration validation.</summary>
public sealed class FailoverTests
{
    /// <summary>A retryable server failure moves to the next configured client after its own attempts are exhausted.</summary>
    [Fact]
    public async Task Retryable_failure_moves_to_next_client()
    {
        var services = new ServiceCollection();
        var first = new StubHandler("busy", HttpStatusCode.ServiceUnavailable);
        var second = new StubHandler(Fixtures.Single);
        services.AddJev("primary", options =>
        {
            options.UseTypeSafe("primary-key");
            options.Retry.MaxAttempts = 1;
        }).ConfigurePrimaryHttpMessageHandler(() => first);
        services.AddJev("secondary", options =>
        {
            options.UseOpenRouter("secondary-key");
            options.Retry.MaxAttempts = 1;
        }).ConfigurePrimaryHttpMessageHandler(() => second);
        services.AddJevFailover("decisions", "primary", "secondary");

        using (var provider = services.BuildServiceProvider())
        {
            var factory = provider.GetRequiredService<IJevClientFactory>();
            using (var client = factory.CreateClient("decisions"))
            {
                var response = await client.EvaluateAsync(Fixtures.One());
                Assert.Equal("jev-1.13.0", response.Model);
            }
        }

        Assert.Single(first.Requests);
        Assert.Single(second.Requests);
    }

    /// <summary>An authentication failure stays with the provider and never sends an evaluation to a fallback.</summary>
    [Fact]
    public async Task Authentication_failure_does_not_move_to_next_client()
    {
        var services = new ServiceCollection();
        var first = new StubHandler("invalid key", HttpStatusCode.Unauthorized);
        var second = new StubHandler(Fixtures.Single);
        services.AddJev("primary", options => options.UseTypeSafe("primary-key"))
            .ConfigurePrimaryHttpMessageHandler(() => first);
        services.AddJev("secondary", options => options.UseOpenRouter("secondary-key"))
            .ConfigurePrimaryHttpMessageHandler(() => second);
        services.AddJevFailover("decisions", "primary", "secondary");

        using (var provider = services.BuildServiceProvider())
        {
            var factory = provider.GetRequiredService<IJevClientFactory>();
            using (var client = factory.CreateClient("decisions"))
            {
                await Assert.ThrowsAsync<JevAuthenticationException>(() => client.EvaluateAsync(Fixtures.One()));
            }
        }

        Assert.Single(first.Requests);
        Assert.Empty(second.Requests);
    }

    /// <summary>Exhausting every transient provider returns ordered, typed provider failures.</summary>
    [Fact]
    public async Task Exhausted_chain_returns_ordered_failures()
    {
        var services = new ServiceCollection();
        var first = new StubHandler("busy", HttpStatusCode.ServiceUnavailable);
        var second = new StubHandler("busy", HttpStatusCode.GatewayTimeout);
        services.AddJev("primary", options =>
        {
            options.UseTypeSafe("primary-key");
            options.Retry.MaxAttempts = 1;
        }).ConfigurePrimaryHttpMessageHandler(() => first);
        services.AddJev("secondary", options =>
        {
            options.UseOpenRouter("secondary-key");
            options.Retry.MaxAttempts = 1;
        }).ConfigurePrimaryHttpMessageHandler(() => second);
        services.AddJevFailover("decisions", "primary", "secondary");

        using (var provider = services.BuildServiceProvider())
        {
            var factory = provider.GetRequiredService<IJevClientFactory>();
            using (var client = factory.CreateClient("decisions"))
            {
                var exception = await Assert.ThrowsAsync<JevFailoverException>(() => client.EvaluateAsync(Fixtures.One()));
                Assert.Collection(exception.Failures,
                    firstFailure => Assert.Equal("TypeSafe", firstFailure.Provider),
                    secondFailure => Assert.Equal("OpenRouter", secondFailure.Provider));
            }
        }
    }

    /// <summary>Registration rejects insufficient, duplicated, or unresolved named-client chains.</summary>
    [Fact]
    public void Invalid_chains_are_rejected_before_the_service_provider_is_built()
    {
        var services = new ServiceCollection();
        services.AddJev("primary", options => options.UseTypeSafe("primary-key"));
        Assert.Throws<ArgumentException>(() => services.AddJevFailover("single", "primary"));
        Assert.Throws<ArgumentException>(() => services.AddJevFailover("duplicate", "primary", "primary"));
        Assert.Throws<ArgumentException>(() => services.AddJevFailover("unknown", "primary", "missing"));
    }
}
