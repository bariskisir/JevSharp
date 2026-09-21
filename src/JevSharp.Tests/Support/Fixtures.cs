using JevSharp.Abstractions.Models;
using JevSharp.Abstractions.Protocols;
using JevSharp.Abstractions.Requests;
using JevSharp.Core.Configuration;

namespace JevSharp.Tests.Support;

/// <summary>Independent protocol fixtures covering three primitives and multiple Nouls.</summary>
internal static class Fixtures
{
    internal const string Standard = """
        {
          "model":"jev-1.13.0", "id":"evaluation-1", "provider":"TypeSafe",
          "usage":{"input_tokens":120,"output_tokens":20,"cost":0.00001,"cache_read_tokens":3},
          "future":{"enabled":true},
          "answers":{
            "policy_supports_refund":{"type":"noul","noul":0.8},
            "refund_requested":{"type":"noul","noul":0.95},
            "urgency":{"type":"score","score":1.6,"probabilities":{"0":0.1,"1":0.2,"2":0.7},"legend":{"0":"Routine","1":"Soon","2":"Immediate"},"confidence":0.6},
            "department":{"type":"choice","choice":"billing","probabilities":{"billing":0.8,"technical":0.2},"confidence":0.7,"futureAnswer":17}
          }
        }
        """;
    internal const string Vercel = """
        {
          "answers":{
            "department":{"type":"choice","choice":"billing","probabilities":{"billing":0.8,"technical":0.2}},
            "urgency":{"type":"score","score":1.6},
            "refund_requested":{"type":"boolean","probability":0.95},
            "policy_supports_refund":{"type":"boolean","probability":0.8}
          },
          "usage":{"inputTokens":120,"outputTokens":20},
          "rounding":{"probabilityDecimals":2},
          "warnings":[{"type":"other","message":"Example warning"}],
          "providerMetadata":{"typesafe":{"version":"1.13.0"}}
        }
        """;
    internal const string Single = """{"model":"jev-1.13.0","usage":{"input_tokens":1,"output_tokens":1},"answers":{"yes":{"type":"noul","noul":0.9}}}""";

    /// <summary>Builds a mixed request with structured state and question entries.</summary>
    internal static JevRequest Mixed() => new(
        JevValue.FromObject(new { ticket = "Charged twice", amounts = new[] { 49, 49 }, eligible = true }),
        new Dictionary<string, JevQuestion>
        {
            ["department"] = new ChoiceQuestion("Select a team.", new Dictionary<string, JevValue>
            {
                ["billing"] = JevValue.FromObject(new { handles = new[] { "refunds", "charges" } }),
                ["technical"] = JevValue.Null
            }),
            ["urgency"] = new ScoreQuestion(JevValue.FromObject(new[] { "Rate urgency", "Use the rubric" }),
                ["Routine", JevValue.FromObject(new { label = "Soon" }), "Immediate"]),
            ["refund_requested"] = new NoulQuestion("Was a refund requested?"),
            ["policy_supports_refund"] = new NoulQuestion("Does the policy support a refund?",
                new NoulCriteria(JevValue.FromObject(new { explanation = "Duplicate charges" }), JevValue.Null))
        });

    /// <summary>Builds the smallest valid evaluation.</summary>
    internal static JevRequest One() => new("A refund request", new Dictionary<string, JevQuestion>
    {
        ["yes"] = new NoulQuestion("Does the customer want a refund?")
    });

    /// <summary>Configures a built-in provider without real credentials.</summary>
    internal static JevClientOptions Options(JevProtocol protocol) => protocol switch
    {
        JevProtocol.TypeSafe => new JevClientOptions().UseTypeSafe("test-secret"),
        JevProtocol.OpenRouter => new JevClientOptions().UseOpenRouter("test-secret"),
        _ => new JevClientOptions().UseVercel("test-secret")
    };

    /// <summary>Waits for asynchronous continuations with a bounded real-time failure deadline.</summary>
    internal static async Task EventuallyAsync(Func<bool> condition)
    {
        for (var index = 0; index < 1000; index++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(1);
        }

        Assert.True(condition(), "The asynchronous operation did not reach the expected checkpoint.");
    }
}
