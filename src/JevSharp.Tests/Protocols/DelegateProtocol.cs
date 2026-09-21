namespace JevSharp.Tests.Protocols;

/// <summary>Counts extension calls while delegating format behavior.</summary>
internal sealed class DelegateProtocol(Func<JevRequest, string, JevProtocolRequest> prepare, Func<JevValue, JevProtocolResponseContext, JevResponse> parse) : IJevProtocol
{
    internal int Preparations;
    internal int Parses;

    /// <inheritdoc />
    public JevProtocolRequest PrepareRequest(JevRequest request, string model)
    {
        Interlocked.Increment(ref Preparations);
        return prepare(request, model);
    }

    /// <inheritdoc />
    public JevResponse ParseResponse(JevValue body, JevProtocolResponseContext context)
    {
        Interlocked.Increment(ref Parses);
        return parse(body, context);
    }
}
