using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace JevSharp.DependencyInjection;

/// <summary>Registers Jev clients with Microsoft dependency injection.</summary>
public static class JevServiceCollectionExtensions
{
    private const string DefaultName = "default";

    /// <summary>Registers the default Jev client and the named-client factory.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures provider, model, and reliability options.</param>
    /// <returns>The HTTP builder for custom handlers and transport settings.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">The default name was already registered.</exception>
    public static IHttpClientBuilder AddJev(this IServiceCollection services, Action<JevClientOptions> configure) =>
        services.AddJev(DefaultName, configure);

    /// <summary>Registers a named client; the default registration is also injectable as IJevClient.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">A unique, case-sensitive client name.</param>
    /// <param name="configure">Runs once during registration; options are validated immediately.</param>
    /// <returns>The HTTP builder for the registration.</returns>
    /// <exception cref="ArgumentException">The name is empty, duplicated, or configuration is invalid.</exception>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static IHttpClientBuilder AddJev(this IServiceCollection services, string name, Action<JevClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);
        if (IsRegisteredName(services, name))
        {
            throw new ArgumentException("The Jev client name is already registered.", nameof(name));
        }
        var options = new JevClientOptions();
        configure(options);
        using (var validation = new JevClient(options))
        {
            // Construction validates without making a network request.
        }
        var registration = new ClientRegistration(name, Snapshot(options));
        services.AddSingleton(registration);
        services.TryAddSingleton<IJevClientFactory, JevClientFactory>();
        if (name == DefaultName)
        {
            services.AddTransient<IJevClient>(sp => sp.GetRequiredService<IJevClientFactory>().CreateClient(DefaultName));
        }
        return services.AddHttpClient(HttpName(name), client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
            .RemoveAllLoggers();
    }

    /// <summary>Registers an ordered named-client failover chain.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">A unique name for the failover client.</param>
    /// <param name="clientNames">Previously registered clients or failover chains, in attempt order.</param>
    /// <returns>The original service collection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">The chain is invalid, ambiguous, cyclic, or references an unknown client.</exception>
    /// <remarks>Only transient transport, timeout, rate-limit, local-resilience, and retryable server failures move to the next client.</remarks>
    public static IServiceCollection AddJevFailover(this IServiceCollection services, string name, params string[] clientNames)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(clientNames);
        if (name == DefaultName || IsRegisteredName(services, name))
        {
            throw new ArgumentException("The Jev client name is already registered.", nameof(name));
        }

        if (clientNames.Length < 2 || clientNames.Any(string.IsNullOrWhiteSpace)
            || clientNames.Distinct(StringComparer.Ordinal).Count() != clientNames.Length)
        {
            throw new ArgumentException("A failover chain requires at least two unique, non-empty client names.", nameof(clientNames));
        }

        var registration = new FailoverRegistration(name, clientNames.ToArray());
        ValidateFailoverGraph(services, registration);
        services.AddSingleton(registration);
        services.TryAddSingleton<IJevClientFactory, JevClientFactory>();
        return services;
    }

    /// <summary>Copies configuration so captured options cannot change future factory clients.</summary>
    private static JevClientOptions Snapshot(JevClientOptions source)
    {
        return source with { Retry = source.Retry with { } };
    }

    /// <summary>Builds an isolated HTTP-factory registration name.</summary>
    internal static string HttpName(string name) => $"JevSharp:{name}";

    /// <summary>Checks names across direct clients and named failover chains.</summary>
    private static bool IsRegisteredName(IServiceCollection services, string name) => services.Any(descriptor =>
        descriptor.ImplementationInstance is ClientRegistration { Name: var clientName } && clientName == name
        || descriptor.ImplementationInstance is FailoverRegistration { Name: var failoverName } && failoverName == name);

    /// <summary>Validates every known edge so registrations fail before a container is built.</summary>
    private static void ValidateFailoverGraph(IServiceCollection services, FailoverRegistration candidate)
    {
        var directNames = services
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<ClientRegistration>()
            .Select(registration => registration.Name)
            .ToHashSet(StringComparer.Ordinal);
        var failovers = services
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<FailoverRegistration>()
            .Append(candidate)
            .ToDictionary(registration => registration.Name, registration => registration.ClientNames, StringComparer.Ordinal);
        var knownNames = directNames.Concat(failovers.Keys).ToHashSet(StringComparer.Ordinal);
        if (candidate.ClientNames.Any(clientName => !knownNames.Contains(clientName)))
        {
            throw new ArgumentException("Every failover client must be registered before it is referenced.", nameof(candidate));
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var failoverName in failovers.Keys)
        {
            Visit(failoverName, failovers, visiting, visited);
        }
    }

    /// <summary>Detects cycles in a failover graph.</summary>
    private static void Visit(
        string name,
        IReadOnlyDictionary<string, IReadOnlyList<string>> failovers,
        ISet<string> visiting,
        ISet<string> visited)
    {
        if (visited.Contains(name))
        {
            return;
        }

        if (!visiting.Add(name))
        {
            throw new ArgumentException("A Jev failover chain cannot contain a cycle.");
        }

        foreach (var child in failovers[name])
        {
            if (failovers.ContainsKey(child))
            {
                Visit(child, failovers, visiting, visited);
            }
        }

        visiting.Remove(name);
        visited.Add(name);
    }
}
