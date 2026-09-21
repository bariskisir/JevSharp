using System.Text;

namespace JevSharp.Core.Protocols;

/// <summary>Adapts one built-in wire format to the public protocol extension contract.</summary>
internal sealed class BuiltInProtocol(JevProtocol protocol) : IJevProtocol
{
    /// <inheritdoc />
    public JevProtocolRequest PrepareRequest(JevRequest request, string model)
    {
        var prepared = ProtocolCodec.Prepare(request, protocol, model);
        var headers = new Dictionary<string, string>();
        if (protocol == JevProtocol.Vercel)
        {
            headers.Add("ai-model-id", prepared.Model);
            headers.Add("ai-evaluation-model-specification-version", "4");
            headers.Add("ai-gateway-protocol-version", "0.0.1");
            headers.Add("ai-gateway-auth-method", "api-key");
        }

        return new(JevValue.FromJson(Encoding.UTF8.GetString(prepared.Body))) { Headers = headers.AsReadOnly() };
    }

    /// <inheritdoc />
    public JevResponse ParseResponse(JevValue body, JevProtocolResponseContext context)
    {
        var prepared = new PreparedRequest(context.Model, [], context.RequestBody.Json.GetProperty("questions"));
        return ProtocolCodec.Parse(Encoding.UTF8.GetBytes(body.ToString()), prepared,
            protocol, context.Provider, context.Attempts, context.RequestId);
    }
}
