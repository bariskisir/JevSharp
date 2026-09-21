using System.Text;
using System.Text.Json;

namespace JevSharp.Core.Transport;

/// <summary>Owns stable per-evaluation data independently of a shared protocol instance.</summary>
internal sealed record ProtocolEvaluation(
    string Model, JevValue RequestBody, byte[] Body, JsonElement Questions,
    IReadOnlyDictionary<string, string> Headers)
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    /// <summary>Validates common inputs and snapshots protocol output before any HTTP activity.</summary>
    internal static ProtocolEvaluation Prepare(IJevProtocol protocol, JevRequest request, string defaultModel)
    {
        ArgumentNullException.ThrowIfNull(request);
        var canonical = ProtocolCodec.Prepare(request with { VercelProviderOptions = null }, JevProtocol.TypeSafe, defaultModel);
        var prepared = protocol.PrepareRequest(request, canonical.Model)
            ?? throw new ArgumentException("The protocol returned no request.", nameof(protocol));
        ArgumentNullException.ThrowIfNull(prepared.Body);
        ArgumentNullException.ThrowIfNull(prepared.Headers);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var validation = new HttpRequestMessage())
        {
            foreach (var (name, value) in prepared.Headers)
            {
                if (string.IsNullOrWhiteSpace(name) || value is null || value.Any(char.IsControl)
                    || IsReservedHeader(name) || !headers.TryAdd(name, value))
                {
                    throw new ArgumentException("Protocol headers must be unique, valid, and cannot control authentication or transport.");
                }

                try
                {
                    validation.Headers.Add(name, value);
                }
                catch (Exception ex) when (ex is FormatException or InvalidOperationException)
                {
                    throw new ArgumentException("The protocol supplied an invalid request header.");
                }
            }
        }

        return new(canonical.Model, prepared.Body, Encoding.UTF8.GetBytes(prepared.Body.ToString()),
            canonical.Questions, headers.AsReadOnly());
    }

    /// <summary>Rejects headers whose ownership belongs to HTTP or authentication.</summary>
    private static bool IsReservedHeader(string name) => name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)
        || new[] { "Authorization", "Proxy-Authorization", "Cookie", "Host", "Connection", "Transfer-Encoding",
            "Trailer", "TE", "Upgrade", "Expect", "Accept", "User-Agent" }.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Normalizes protocol failures and verifies the common answer contract without retrying.</summary>
    internal JevResponse Parse(IJevProtocol protocol, byte[] bytes, string provider, int attempt, string? requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = protocol.ParseResponse(JevValue.FromJson(StrictUtf8.GetString(bytes)),
                new(Model, RequestBody, provider, attempt, requestId));
            if (result is null || result.Answers is null || result.Answers.Count != Questions.EnumerateObject().Count())
            {
                throw new JsonException();
            }

            var answers = result.Answers.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            foreach (var question in Questions.EnumerateObject())
            {
                if (!answers.TryGetValue(question.Name, out var answer))
                {
                    throw new JsonException();
                }

                var valid = (question.Value.GetProperty("type").GetString(), answer) switch
                {
                    ("noul", NoulAnswer noul) => double.IsFinite(noul.Probability) && noul.Probability is >= 0 and <= 1,
                    ("choice", ChoiceAnswer choice) => choice.Choice is not null
                        && question.Value.GetProperty("criteria").TryGetProperty(choice.Choice, out _),
                    ("score", ScoreAnswer score) => double.IsFinite(score.Score) && score.Score >= 0
                        && score.Score <= question.Value.GetProperty("criteria").GetArrayLength() - 1,
                    _ => false
                };
                if (!valid)
                {
                    throw new JsonException();
                }
            }

            return result with { Answers = answers.AsReadOnly(), RequestedModel = Model, RequestId = requestId };
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Extension failures must not become transport retries or expose response data in exceptions.
            throw new JevInvalidResponseException(provider, attempt, requestId);
        }
    }
}
