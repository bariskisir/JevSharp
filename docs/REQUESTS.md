# Requests and responses

One evaluation sends one shared state and a complete map of independent questions. Choice, Score, and Noul questions may be mixed. IDs are case-sensitive and response answers are matched by ID, independently of response order. Multiple questions of the same type are supported.

If a question depends on an earlier answer, create a second request and include that answer in its new state. An array state is one shared state, not a batch of evaluations.

## Structured state and question values

Use `JevValue.FromObject(...)` for CLR objects, dictionaries, or arrays; use `JevValue.FromJson(...)` for existing JSON. Strings convert implicitly. JSON objects are preserved rather than encoded as strings.

```csharp
var state = JevValue.FromObject(new
{
    ticket = new { message = "Charged twice", order_id = "A-104" },
    charges = new[] { 49, 49 },
    policy = "Duplicate charges qualify for refunds."
});

var question = new NoulQuestion(
    JevValue.FromObject(new
    {
        question = "Does the policy support a refund?",
        compare = new[] { "charges", "policy" }
    }),
    new NoulCriteria(
        True: JevValue.FromObject(new { reason = "Duplicate charges", eligible = true }),
        False: "Only one valid charge exists."));
```

State must be a string, object, or array. Nested numeric and boolean properties are preserved. Bare numeric, boolean, and null states, plus image, audio, and video inputs, are unsupported.

Instructions, Choice descriptions, Score levels, and Noul criteria support structured values. Use `JevValue.Null` for explicit JSON null. A C# null Noul criterion omits that side; `JevValue.Null` sends a null value. Choice supports 1-255 options and Score requires 2-10 levels. Do not mutate supplied collections while an evaluation is being prepared; retry payloads are snapshotted.

## Reading answers

| Answer | Main value | Additional data |
| --- | --- | --- |
| `ChoiceAnswer` | `Choice` | `Probabilities`, `Confidence` |
| `ScoreAnswer` | `Score` | `Probabilities`, `Legend`, `Confidence` |
| `NoulAnswer` | `Probability` | A probability from 0 to 1, without an automatic threshold |

Scores are weighted level indices and can be fractional. Confidence and probability have different meanings; apply your own business thresholds. The SDK rejects malformed responses, invalid choices, mismatched IDs/types, and out-of-range values.

`JevResponse` exposes provider-reported usage, nullable USD cost, response/request IDs, provider metadata, rounding metadata, warnings, and unknown top-level properties. Typed answers preserve unknown fields. Redacted error response bodies are available through `JevApiException.Details` and can still contain application data.
