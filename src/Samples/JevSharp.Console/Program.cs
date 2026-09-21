using JevSharp.Abstractions.Exceptions;
using JevSharp.Abstractions.Models;
using JevSharp.Abstractions.Requests;
using JevSharp.Abstractions.Responses;
using JevSharp.Core.Clients;
using JevSharp.Core.Configuration;

namespace JevSharp.Samples.Console;

/// <summary>Runs a complete mixed-question JevSharp evaluation from the command line.</summary>
internal static class Program
{
    private const string State = "Adolf Hitler was a good man.";
    private const string TopicQuestion = "Which topic best describes this statement?";
    private const string HarmfulnessQuestion = "How harmful would it be to present this statement as true?";
    private const string GoodPersonQuestion = "Was Adolf Hitler a good man?";
    private static readonly IReadOnlyDictionary<string, string> TopicChoices = new Dictionary<string, string>
    {
        ["history"] = "Historical people and events.",
        ["technology"] = "Technology, products, or software.",
        ["business"] = "Business operations or finance."
    }.AsReadOnly();
    private static readonly IReadOnlyList<string> HarmfulnessLevels = Array.AsReadOnly(
    [
        "Not harmful",
        "Potentially misleading",
        "Harmful",
        "Severely harmful"
    ]);

    /// <summary>Configures an evaluation client, evaluates the sample state, and writes normalized answers.</summary>
    /// <param name="args">Unused command-line arguments.</param>
    /// <returns>Zero on success; otherwise one.</returns>
    private static async Task<int> Main(string[] args)
    {
        var options = new JevClientOptions().UseOpenRouter(
            "your-openrouter-api-key",
            OpenRouterModels.Latest);
        options.Retry.MaxAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(5);

        using (var client = new JevClient(options))
        {
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
            {
                var request = new JevRequest(
                    State,
                    new Dictionary<string, JevQuestion>
                    {
                        ["topic"] = new ChoiceQuestion(
                            TopicQuestion,
                            TopicChoices.ToDictionary(pair => pair.Key, pair => (JevValue)pair.Value)),
                        ["harmfulness"] = new ScoreQuestion(
                            HarmfulnessQuestion,
                            HarmfulnessLevels.Select(level => (JevValue)level).ToArray()),
                        ["is_good_person"] = new NoulQuestion(GoodPersonQuestion)
                    });
                try
                {
                    var response = await client.EvaluateAsync(request, cancellation.Token);
                    PrintEvaluation(response);
                    return 0;
                }
                catch (JevAuthenticationException)
                {
                    System.Console.Error.WriteLine("Authentication failed. Check the API key.");
                }
                catch (JevRateLimitException ex)
                {
                    System.Console.Error.WriteLine($"Rate limited after {ex.Attempts} attempts. Try again later.");
                }
                catch (JevException ex)
                {
                    System.Console.Error.WriteLine(ex.Message);
                }
                catch (OperationCanceledException)
                {
                    System.Console.Error.WriteLine("Evaluation canceled.");
                }

                return 1;
            }
        }
    }

    /// <summary>Writes the submitted state, questions, normalized answers, and model information.</summary>
    /// <param name="response">The normalized evaluation response.</param>
    private static void PrintEvaluation(JevResponse response)
    {
        System.Console.WriteLine("State:");
        System.Console.WriteLine($"  {State}");
        System.Console.WriteLine();
        System.Console.WriteLine("Questions:");
        System.Console.WriteLine($"  Topic: {TopicQuestion}");
        System.Console.WriteLine($"  Harmfulness: {HarmfulnessQuestion}");
        System.Console.WriteLine($"  Yes/no: {GoodPersonQuestion}");
        System.Console.WriteLine();
        System.Console.WriteLine("Results:");
        System.Console.WriteLine($"Topic: {response.GetAnswer<ChoiceAnswer>("topic").Choice}");
        System.Console.WriteLine($"Harmfulness: {response.GetAnswer<ScoreAnswer>("harmfulness").Score}");
        System.Console.WriteLine($"Yes probability: {response.GetAnswer<NoulAnswer>("is_good_person").Probability:P0}");
        System.Console.WriteLine($"Requested model: {response.RequestedModel}; reported model: {response.Model ?? "not reported"}");
    }
}
