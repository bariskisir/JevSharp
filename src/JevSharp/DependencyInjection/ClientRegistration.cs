using JevSharp.Core.Configuration;

namespace JevSharp.DependencyInjection;

/// <summary>A named options snapshot owned by the service collection.</summary>
internal sealed record ClientRegistration(string Name, JevClientOptions Options);
