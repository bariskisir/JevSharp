# JevSharp

![JevSharp logo](https://raw.githubusercontent.com/bariskisir/JevSharp/master/assets/jevsharp-logo.png)

.NET 10 SDK for Jev decisions through TypeSafe, OpenRouter, Vercel AI Gateway, and compatible endpoints.

[![CI status](https://github.com/bariskisir/JevSharp/actions/workflows/ci.yml/badge.svg)](https://github.com/bariskisir/JevSharp/actions/workflows/ci.yml)
[![NuGet version](https://img.shields.io/nuget/v/JevSharp.svg)](https://www.nuget.org/packages/JevSharp)
[![MIT license](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/bariskisir/JevSharp/blob/master/LICENSE)

## Install

```shell
dotnet add package JevSharp
```

## Quick start

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

- [Providers and models](https://github.com/bariskisir/JevSharp/blob/master/docs/PROVIDERS.md)
- [Requests and responses](https://github.com/bariskisir/JevSharp/blob/master/docs/REQUESTS.md)
- [Dependency injection, failover, and resilience](https://github.com/bariskisir/JevSharp/blob/master/docs/DEPENDENCY-INJECTION.md)
- [Custom endpoints, authentication, and protocols](https://github.com/bariskisir/JevSharp/blob/master/docs/CUSTOM-PROTOCOLS.md)
- [Retries, errors, and logging](https://github.com/bariskisir/JevSharp/blob/master/docs/RELIABILITY.md)
- [Development, samples, and releases](https://github.com/bariskisir/JevSharp/blob/master/docs/DEVELOPMENT.md)
