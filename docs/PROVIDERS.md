# Providers and models

```csharp
var typeSafe = new JevClientOptions().UseTypeSafe("your-typesafe-api-key");
var openRouter = new JevClientOptions().UseOpenRouter("your-openrouter-api-key");
var vercel = new JevClientOptions().UseVercel("your-vercel-api-key");
```

| Provider | Default | Additional constants |
| --- | --- | --- |
| TypeSafe | `TypeSafeModels.Latest` -> `jev-latest` | `Preview` -> `jev-preview`; `Jev1_13_0` -> `jev-1.13.0` |
| OpenRouter | `OpenRouterModels.Latest` -> `~typesafe/jev-latest` | `Jev1_13` -> `typesafe/jev-1.13` |
| Vercel | `VercelModels.Jev` -> `typesafe-ai/jev` | No separately verified latest or version-pinned ID |

Select a constant or pass a provider-supported string:

```csharp
var options = new JevClientOptions().UseTypeSafe(apiKey, model: TypeSafeModels.Jev1_13_0);
options.Model = configuredModelId;
var specificRequest = request with { Model = configuredModelId };
```

Model selection is request override, then client configuration, then provider default. Custom endpoints require an explicit model. Empty IDs are rejected; other IDs pass through unchanged. Constants are a maintained catalog and upstream latest/preview aliases can change. `RequestedModel` is sent to the provider; `Model` is reported by it and may be absent.

## Provider differences

| Provider | Endpoint | Model selection | Yes/no wire format |
| --- | --- | --- | --- |
| TypeSafe | `https://api.typesafe.ai/v1/systemone` | Request body | `noul` |
| OpenRouter | `https://openrouter.ai/api/alpha/decisions` | Request body | `noul` |
| Vercel | `https://ai-gateway.vercel.sh/v4/ai/evaluation-model` | `ai-model-id` header | `boolean` / `probability` |

Vercel uses its experimental evaluation specification v4 and gateway protocol headers. JevSharp normalizes every provider to `NoulAnswer.Probability`. Vercel can omit confidence, legends, distributions, model IDs, and cost; absent values remain null. OpenRouter uses the common `model`, `state`, and `questions` fields. Vercel-specific JSON options use `JevRequest.VercelProviderOptions`; using them with another protocol fails locally.
