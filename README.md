<p align="center">
  <img src="https://raw.githubusercontent.com/bariskisir/JevSharp/master/assets/jevsharp-logo.svg" alt="JevSharp logo" width="88">
</p>

<h1 align="center">JevSharp</h1>

<p align="center">
  .NET 10 SDK for Jev decisions through TypeSafe, OpenRouter, Vercel AI Gateway, and compatible endpoints.
</p>

<p align="center">
  <a href="https://github.com/bariskisir/JevSharp/actions/workflows/ci.yml"><img src="https://github.com/bariskisir/JevSharp/actions/workflows/ci.yml/badge.svg" alt="CI status"></a>
  <a href="https://www.nuget.org/packages/JevSharp"><img src="https://img.shields.io/nuget/v/JevSharp.svg" alt="NuGet version"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT license"></a>
</p>

---

Send one shared state and multiple independent Choice, Score, and Noul questions in one request. JevSharp provides typed answers and supports TypeSafe, OpenRouter, Vercel AI Gateway, and compatible custom endpoints.

## Install

```shell
dotnet add package JevSharp
```

Requires .NET 10.

## Quick start

Pass your API key directly. Your application decides where its values come from:

```csharp
using JevSharp.Abstractions.Models;
using JevSharp.Abstractions.Requests;
using JevSharp.Abstractions.Responses;
using JevSharp.Core.Clients;
using JevSharp.Core.Configuration;

var apiKey = "your-openrouter-api-key";
var options = new JevClientOptions().UseOpenRouter(apiKey);

using (var client = new JevClient(options))
{
    var request = new JevRequest(
        "My card was charged twice. Please refund the duplicate.",
        new Dictionary<string, JevQuestion>
        {
            ["department"] = new ChoiceQuestion(
                "Which team should handle this?",
                new Dictionary<string, JevValue>
                {
                    ["billing"] = "Charges, payments, and refunds",
                    ["technical"] = "Software defects and outages"
                }),
            ["urgency"] = new ScoreQuestion(
                "How urgently should support respond?",
                ["Routine", "Soon", "Immediately"]),
            ["refund_requested"] = new NoulQuestion("Does the customer request a refund?")
        });

    var response = await client.EvaluateAsync(request);
    Console.WriteLine(response.GetAnswer<ChoiceAnswer>("department").Choice);
    Console.WriteLine(response.GetAnswer<ScoreAnswer>("urgency").Score);
    Console.WriteLine(response.GetAnswer<NoulAnswer>("refund_requested").Probability);
}
```

## Documentation

- [Providers and models](docs/PROVIDERS.md)
- [Requests and responses](docs/REQUESTS.md)
- [Dependency injection, failover, and resilience](docs/DEPENDENCY-INJECTION.md)
- [Custom endpoints, authentication, and protocols](docs/CUSTOM-PROTOCOLS.md)
- [Retries, errors, and logging](docs/RELIABILITY.md)
- [Development, samples, and releases](docs/DEVELOPMENT.md)
