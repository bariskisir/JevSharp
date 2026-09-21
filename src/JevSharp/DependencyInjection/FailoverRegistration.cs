namespace JevSharp.DependencyInjection;

/// <summary>An ordered named-client chain owned by the service collection.</summary>
internal sealed record FailoverRegistration(string Name, IReadOnlyList<string> ClientNames);
