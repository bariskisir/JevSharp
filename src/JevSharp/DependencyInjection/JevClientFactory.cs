using JevSharp.Abstractions.Clients;
using JevSharp.Core.Clients;
using JevSharp.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace JevSharp.DependencyInjection;

/// <summary>Creates disposable client wrappers backed by pooled HTTP handlers.</summary>
internal sealed class JevClientFactory : IJevClientFactory
{
    private readonly IHttpClientFactory httpFactory;
    private readonly IReadOnlyDictionary<string, JevClientOptions> registrations;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> failovers;
    private readonly ILoggerFactory loggerFactory;

    /// <summary>Captures the registered configurations and infrastructure dependencies.</summary>
    public JevClientFactory(
        IHttpClientFactory httpFactory,
        IEnumerable<ClientRegistration> registrations,
        IEnumerable<FailoverRegistration> failovers,
        ILoggerFactory loggerFactory)
    {
        this.httpFactory = httpFactory;
        this.registrations = registrations.ToDictionary(r => r.Name, r => r.Options, StringComparer.Ordinal);
        this.failovers = failovers.ToDictionary(r => r.Name, r => r.ClientNames, StringComparer.Ordinal);
        this.loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public IJevClient CreateClient(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return CreateClient(name, new HashSet<string>(StringComparer.Ordinal));
    }

    /// <summary>Creates direct or failover clients while rejecting malformed registration graphs defensively.</summary>
    private IJevClient CreateClient(string name, ISet<string> visiting)
    {
        if (!visiting.Add(name))
        {
            throw new InvalidOperationException("A Jev failover registration contains a cycle.");
        }

        try
        {
            if (!registrations.TryGetValue(name, out var options))
            {
                if (!failovers.TryGetValue(name, out var failover))
                {
                    throw new ArgumentException("No Jev client is registered under that name.", nameof(name));
                }

                var clients = new List<IJevClient>();
                try
                {
                    foreach (var clientName in failover)
                    {
                        clients.Add(CreateClient(clientName, visiting));
                    }

                    return new FailoverJevClient(clients);
                }
                catch
                {
                    foreach (var client in clients)
                    {
                        client.Dispose();
                    }

                    throw;
                }
            }

            var http = httpFactory.CreateClient(JevServiceCollectionExtensions.HttpName(name));
            try
            {
                return JevClient.CreateOwned(options, http, loggerFactory.CreateLogger<JevClient>());
            }
            catch
            {
                http.Dispose();
                throw;
            }
        }
        finally
        {
            visiting.Remove(name);
        }
    }
}
