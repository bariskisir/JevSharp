using JevSharp.Abstractions.Authentication;

namespace JevSharp.Tests.Support;

/// <summary>Applies deterministic caller authentication behavior without external credentials.</summary>
internal sealed class CallbackAuthentication(Action<HttpRequestMessage> apply) : IJevAuthentication
{
    /// <inheritdoc />
    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        apply(request);
        return ValueTask.CompletedTask;
    }
}
