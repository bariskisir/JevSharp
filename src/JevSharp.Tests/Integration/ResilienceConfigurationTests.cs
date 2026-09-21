using System.Net;
using JevSharp.DependencyInjection.Resilience;
using Microsoft.Extensions.DependencyInjection;

namespace JevSharp.Tests.Integration;

/// <summary>Verifies opt-in Microsoft HTTP resilience configuration validation.</summary>
public sealed class ResilienceConfigurationTests
{
    /// <summary>Invalid local resilience values are rejected at service-registration time.</summary>
    [Theory]
    [InlineData(0, 0, 0.5, 10)]
    [InlineData(1, -1, 0.5, 10)]
    [InlineData(1, 0, 0, 10)]
    [InlineData(1, 0, 1.1, 10)]
    [InlineData(1, 0, 0.5, 1)]
    public void Invalid_values_are_rejected(int permits, int queue, double ratio, int throughput)
    {
        var services = new ServiceCollection();
        var builder = services.AddJev("resilient", options => options.UseTypeSafe("test-key"));
        Assert.Throws<ArgumentException>(() => builder.ConfigureJevResilience(options =>
        {
            options.PermitLimit = permits;
            options.QueueLimit = queue;
            options.FailureRatio = ratio;
            options.MinimumThroughput = throughput;
        }));
    }

    /// <summary>Valid configuration can be attached without adding a second Jev retry policy.</summary>
    [Fact]
    public void Valid_configuration_is_registered()
    {
        var services = new ServiceCollection();
        services.AddJev("resilient", options => options.UseTypeSafe("test-key"))
            .ConfigureJevResilience(options =>
            {
                options.PermitLimit = 2;
                options.QueueLimit = 1;
                options.FailureRatio = 0.5;
                options.MinimumThroughput = 2;
                options.SamplingDuration = TimeSpan.FromSeconds(5);
                options.BreakDuration = TimeSpan.FromSeconds(5);
            });
        using (var provider = services.BuildServiceProvider())
        {
            Assert.NotNull(provider.GetRequiredService<IJevClientFactory>());
        }
    }

    /// <summary>An open Microsoft resilience circuit is normalized to a safe Jev exception.</summary>
    [Fact]
    public async Task Open_circuit_is_normalized_to_a_jev_exception()
    {
        var services = new ServiceCollection();
        var handler = new StubHandler("busy", HttpStatusCode.ServiceUnavailable);
        services.AddJev("resilient", options =>
        {
            options.UseTypeSafe("test-key");
            options.Retry.MaxAttempts = 1;
        }).ConfigurePrimaryHttpMessageHandler(() => handler)
            .ConfigureJevResilience(options =>
            {
                options.PermitLimit = 2;
                options.MinimumThroughput = 2;
                options.SamplingDuration = TimeSpan.FromMinutes(1);
                options.BreakDuration = TimeSpan.FromMinutes(1);
            });

        using (var provider = services.BuildServiceProvider())
        {
            var factory = provider.GetRequiredService<IJevClientFactory>();
            using (var client = factory.CreateClient("resilient"))
            {
                await Assert.ThrowsAsync<JevApiException>(() => client.EvaluateAsync(Fixtures.One()));
                await Assert.ThrowsAsync<JevApiException>(() => client.EvaluateAsync(Fixtures.One()));
                await Assert.ThrowsAsync<JevCircuitOpenException>(() => client.EvaluateAsync(Fixtures.One()));
            }
        }

        Assert.Equal(2, handler.Requests.Count);
    }
}
