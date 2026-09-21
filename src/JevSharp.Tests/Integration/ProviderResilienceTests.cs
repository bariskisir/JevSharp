using System.Net;
using JevSharp.DependencyInjection.Resilience;
using Microsoft.Extensions.DependencyInjection;

namespace JevSharp.Tests.Integration;

/// <summary>Checks provider-neutral circuit classification and registration snapshots.</summary>
public sealed class ProviderResilienceTests
{
    /// <summary>Combines supported providers with transient and permanent HTTP statuses.</summary>
    public static IEnumerable<object[]> Statuses()
    {
        foreach (var provider in ProviderFixture.Names)
        {
            foreach (var status in new[] { 400, 401, 403, 422, 501, 505 })
            {
                yield return [provider, status, false];
            }
            foreach (var status in new[] { 408, 429, 500, 502, 503, 504, 524, 529 })
            {
                yield return [provider, status, true];
            }
        }
    }

    /// <summary>Only documented transient responses contribute to opening a circuit.</summary>
    [Theory]
    [MemberData(nameof(Statuses))]
    public async Task Circuit_uses_shared_http_classification(string providerName, int status, bool transient)
    {
        var services = new ServiceCollection();
        var handler = new StubHandler("unavailable", (HttpStatusCode)status);
        Register(services, providerName, handler);
        using (var provider = services.BuildServiceProvider())
        {
            using (var client = provider.GetRequiredService<IJevClientFactory>().CreateClient("test"))
            {
                await Assert.ThrowsAnyAsync<JevApiException>(() => client.EvaluateAsync(Fixtures.One()));
                await Assert.ThrowsAnyAsync<JevApiException>(() => client.EvaluateAsync(Fixtures.One()));
                if (transient)
                {
                    await Assert.ThrowsAsync<JevCircuitOpenException>(() => client.EvaluateAsync(Fixtures.One()));
                }
                else
                {
                    await Assert.ThrowsAnyAsync<JevApiException>(() => client.EvaluateAsync(Fixtures.One()));
                }
            }
        }
        Assert.Equal(transient ? 2 : 3, handler.Requests.Count);
    }

    /// <summary>Connection failures count, while permanent TLS and protocol failures do not.</summary>
    [Theory]
    [InlineData(HttpRequestError.ConnectionError, true)]
    [InlineData(HttpRequestError.NameResolutionError, true)]
    [InlineData(HttpRequestError.ResponseEnded, true)]
    [InlineData(HttpRequestError.SecureConnectionError, false)]
    [InlineData(HttpRequestError.HttpProtocolError, false)]
    public async Task Circuit_uses_shared_transport_classification(HttpRequestError error, bool transient)
    {
        var services = new ServiceCollection();
        var handler = new StubHandler((_, _, _) => throw new HttpRequestException(error));
        Register(services, "Custom", handler);
        using (var provider = services.BuildServiceProvider())
        {
            using (var client = provider.GetRequiredService<IJevClientFactory>().CreateClient("test"))
            {
                await Assert.ThrowsAsync<JevTransportException>(() => client.EvaluateAsync(Fixtures.One()));
                await Assert.ThrowsAsync<JevTransportException>(() => client.EvaluateAsync(Fixtures.One()));
                if (transient)
                {
                    await Assert.ThrowsAsync<JevCircuitOpenException>(() => client.EvaluateAsync(Fixtures.One()));
                }
                else
                {
                    await Assert.ThrowsAsync<JevTransportException>(() => client.EvaluateAsync(Fixtures.One()));
                }
            }
        }
        Assert.Equal(transient ? 2 : 3, handler.Requests.Count);
    }

    /// <summary>Mutating caller-held options cannot affect later pipeline construction.</summary>
    [Theory]
    [InlineData("TypeSafe")]
    [InlineData("OpenRouter")]
    [InlineData("Vercel")]
    [InlineData("Custom")]
    public async Task Registration_snapshots_resilience_options(string providerName)
    {
        var services = new ServiceCollection();
        var handler = new StubHandler(ProviderFixture.Response(providerName));
        JevResilienceOptions? captured = null;
        services.AddJev("test", options => ProviderFixture.Configure(options, providerName))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .ConfigureJevResilience(options => captured = options);
        captured!.PermitLimit = 0;
        captured.FailureRatio = 0;
        captured.BreakDuration = TimeSpan.Zero;
        using (var provider = services.BuildServiceProvider())
        {
            using (var client = provider.GetRequiredService<IJevClientFactory>().CreateClient("test"))
            {
                var response = await client.EvaluateAsync(Fixtures.One());
                Assert.Equal(0.9, response.GetAnswer<NoulAnswer>("yes").Probability);
            }
        }
    }

    /// <summary>Creates a circuit whose failure threshold is reached after two failed sends.</summary>
    private static void Register(ServiceCollection services, string provider, StubHandler handler)
    {
        services.AddJev("test", options => ProviderFixture.Configure(options, provider))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .ConfigureJevResilience(options =>
            {
                options.MinimumThroughput = 2;
                options.SamplingDuration = TimeSpan.FromMinutes(1);
                options.BreakDuration = TimeSpan.FromMinutes(1);
            });
    }
}
