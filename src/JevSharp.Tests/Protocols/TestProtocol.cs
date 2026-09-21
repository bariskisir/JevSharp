using System.Text.Json;

namespace JevSharp.Tests.Protocols;

/// <summary>Provides an isolated JSON mapping for custom protocol behavior tests.</summary>
internal sealed class TestProtocol : IJevProtocol
{
    /// <inheritdoc />
    public JevProtocolRequest PrepareRequest(JevRequest request, string model)
    {
        var standard = JevProtocols.TypeSafe.PrepareRequest(request, model).Body.Json;
        return new(JevValue.FromObject(new
        {
            deployment = model,
            input = standard.GetProperty("state"),
            checks = standard.GetProperty("questions")
        }))
        {
            Headers = new Dictionary<string, string> { ["X-Evaluation-Version"] = "1" }
        };
    }

    /// <inheritdoc />
    public JevResponse ParseResponse(JevValue body, JevProtocolResponseContext context)
    {
        var checks = context.RequestBody.Json.GetProperty("checks");
        var answers = new Dictionary<string, JevAnswer>(StringComparer.Ordinal);
        foreach (var result in body.Json.GetProperty("results").EnumerateObject())
        {
            JevAnswer answer = checks.GetProperty(result.Name).GetProperty("type").GetString() switch
            {
                "choice" => new ChoiceAnswer(result.Value.GetString() ?? throw new JsonException(), null, null),
                "score" => new ScoreAnswer(result.Value.GetDouble(), null, null, null),
                "noul" => new NoulAnswer(result.Value.GetDouble()),
                _ => throw new JsonException("Unsupported question type.")
            };
            answers.Add(result.Name, answer);
        }

        return new()
        {
            RequestedModel = context.Model,
            RequestId = context.RequestId,
            Answers = answers.AsReadOnly()
        };
    }
}
