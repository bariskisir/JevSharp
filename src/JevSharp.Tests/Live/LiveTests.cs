namespace JevSharp.Tests.Live;

/// <summary>Explicitly enabled live provider checks; never run in ordinary CI.</summary>
public sealed class LiveTests
{
    /// <summary>Exercises the TypeSafe service with mixed questions.</summary>
    [LiveFact("TYPESAFE_API_KEY")]
    [Trait("Category", "Live")]
    public async Task TypeSafe_evaluates_mixed_questions() => await EvaluateAsync(JevProtocol.TypeSafe, "TYPESAFE_API_KEY");

    /// <summary>Exercises the OpenRouter service with mixed questions.</summary>
    [LiveFact("OPENROUTER_API_KEY")]
    [Trait("Category", "Live")]
    public async Task OpenRouter_evaluates_mixed_questions() => await EvaluateAsync(JevProtocol.OpenRouter, "OPENROUTER_API_KEY");

    /// <summary>Exercises the experimental Vercel evaluation endpoint.</summary>
    [LiveFact("VERCEL_API_KEY")]
    [Trait("Category", "Live")]
    public async Task Vercel_evaluates_mixed_questions() => await EvaluateAsync(JevProtocol.Vercel, "VERCEL_API_KEY");

    /// <summary>Checks live response structure without assuming probabilistic outcomes.</summary>
    private static async Task EvaluateAsync(JevProtocol protocol, string variable)
    {
        var key = Environment.GetEnvironmentVariable(variable)!;
        var options = protocol switch
        {
            JevProtocol.TypeSafe => new JevClientOptions().UseTypeSafe(key),
            JevProtocol.OpenRouter => new JevClientOptions().UseOpenRouter(key),
            _ => new JevClientOptions().UseVercel(key)
        };
        options.Retry.MaxAttempts = 1;
        using (var client = new JevClient(options))
        {
            var response = await client.EvaluateAsync(CreateLiveRequest());
            Assert.Equal(3, response.Answers.Count);
            Assert.IsType<ChoiceAnswer>(response.Answers["department"]);
            Assert.IsType<ScoreAnswer>(response.Answers["urgency"]);
            Assert.InRange(response.GetAnswer<NoulAnswer>("refund_requested").Probability, 0, 1);
        }
    }

    /// <summary>Creates the provider-neutral plain-text request used for paid compatibility checks.</summary>
    /// <returns>A mixed primitive request accepted by every built-in provider.</returns>
    private static JevRequest CreateLiveRequest() => new(
        "The customer reports that the checkout page is blank after selecting Pay and asks for help.",
        new Dictionary<string, JevQuestion>
        {
            ["department"] = new ChoiceQuestion(
                "Which team should own this ticket?",
                new Dictionary<string, JevValue>
                {
                    ["billing"] = "Charges, refunds, and payment processing.",
                    ["technical"] = "Software defects and outages."
                }),
            ["urgency"] = new ScoreQuestion(
                "How urgent is this ticket?",
                ["Can wait", "Needs attention soon", "Blocking revenue now"]),
            ["refund_requested"] = new NoulQuestion("Does the customer request a refund?")
        });
}
