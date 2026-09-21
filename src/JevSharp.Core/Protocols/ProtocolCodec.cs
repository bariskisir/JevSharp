using System.Globalization;
using System.Text.Json;

namespace JevSharp.Core.Protocols;

/// <summary>Maps public evaluation contracts to the three provider wire protocols.</summary>
internal static class ProtocolCodec
{
    /// <summary>Validates and serializes a complete evaluation once for all attempts.</summary>
    internal static PreparedRequest Prepare(JevRequest request, JevProtocol protocol, string defaultModel)
    {
        ArgumentNullException.ThrowIfNull(request);
        var model = request.Model ?? defaultModel;
        ValidateModel(model);
        ValidateEntry(request.State, false);
        if (request.Questions is null || request.Questions.Count == 0)
        {
            throw new ArgumentException("At least one question is required.", nameof(request));
        }

        if (request.VercelProviderOptions is not null && protocol != JevProtocol.Vercel)
        {
            throw new ArgumentException("Request options do not match the configured protocol.", nameof(request));
        }

        using (var stream = new MemoryStream())
        {
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                if (protocol != JevProtocol.Vercel)
                {
                    writer.WriteString("model", model);
                }

                WriteValue(writer, "state", request.State);
                writer.WriteStartObject("questions");
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (id, question) in request.Questions)
                {
                    if (string.IsNullOrWhiteSpace(id) || !names.Add(id) || question is null)
                    {
                        throw new ArgumentException("Question IDs must be unique and nonempty and questions cannot be null.", nameof(request));
                    }

                    ValidateEntry(question.Instructions, protocol != JevProtocol.Vercel);
                    writer.WriteStartObject(id);
                    WriteValue(writer, "instructions", question.Instructions);
                    switch (question)
                    {
                        case ChoiceQuestion choice:
                            if (choice.Criteria is null || choice.Criteria.Count is < 1 or > 255)
                            {
                                throw new ArgumentException("Choice requires between one and 255 options.", nameof(request));
                            }

                            writer.WriteString("type", "choice");
                            writer.WriteStartObject("criteria");
                            var options = new HashSet<string>(StringComparer.Ordinal);
                            foreach (var (key, value) in choice.Criteria)
                            {
                                if (string.IsNullOrWhiteSpace(key) || !options.Add(key))
                                {
                                    throw new ArgumentException("Choice option names must be unique and nonempty.", nameof(request));
                                }

                                ValidateEntry(value, true);
                                WriteValue(writer, key, value);
                            }

                            writer.WriteEndObject();
                            break;
                        case ScoreQuestion score:
                            if (score.Criteria is null || score.Criteria.Count is < 2 or > 10)
                            {
                                throw new ArgumentException("Score requires between two and ten ordered levels.", nameof(request));
                            }

                            writer.WriteString("type", "score");
                            writer.WriteStartArray("criteria");
                            foreach (var value in score.Criteria)
                            {
                                ValidateEntry(value, true);
                                value.Json.WriteTo(writer);
                            }

                            writer.WriteEndArray();
                            break;
                        case NoulQuestion noul:
                            writer.WriteString("type", protocol == JevProtocol.Vercel ? "boolean" : "noul");
                            if (noul.Criteria is not null)
                            {
                                writer.WriteStartObject("criteria");
                                if (noul.Criteria.True is { } yes)
                                {
                                    ValidateEntry(yes, true);
                                    WriteValue(writer, "true", yes);
                                }

                                if (noul.Criteria.False is { } no)
                                {
                                    ValidateEntry(no, true);
                                    WriteValue(writer, "false", no);
                                }

                                writer.WriteEndObject();
                            }

                            break;
                        default:
                            throw new ArgumentException("Unsupported question type.", nameof(request));
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                WriteObject(writer, "providerOptions", request.VercelProviderOptions);
                writer.WriteEndObject();
            }

            var body = stream.ToArray();
            using (var snapshot = JsonDocument.Parse(body))
            {
                return new(model, body, snapshot.RootElement.GetProperty("questions").Clone());
            }
        }
    }

    /// <summary>Rejects empty model IDs or characters that cannot be safely used in headers.</summary>
    internal static void ValidateModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model) || model.Any(char.IsControl))
        {
            throw new ArgumentException("A model ID must be nonempty and contain no control characters.", nameof(model));
        }
    }

    /// <summary>Checks a structured entry without restricting nested JSON field types.</summary>
    private static void ValidateEntry(JevValue? value, bool nullable)
    {
        if (value is null || !(value.Json.ValueKind is JsonValueKind.String or JsonValueKind.Object or JsonValueKind.Array
            || nullable && value.Json.ValueKind == JsonValueKind.Null))
        {
            throw new ArgumentException("An entry must be a string, object, or array; use JevValue.Null where null is supported.");
        }
    }

    /// <summary>Writes structured JSON without double encoding.</summary>
    private static void WriteValue(Utf8JsonWriter writer, string name, JevValue value)
    {
        writer.WritePropertyName(name);
        value.Json.WriteTo(writer);
    }

    /// <summary>Writes an optional provider-specific JSON object.</summary>
    private static void WriteObject(Utf8JsonWriter writer, string name, JevValue? value)
    {
        if (value is null)
        {
            return;
        }

        if (value.Json.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Provider options must be JSON objects.");
        }

        WriteValue(writer, name, value);
    }

    /// <summary>Parses and validates answers against the serialized request snapshot.</summary>
    internal static JevResponse Parse(byte[] body, PreparedRequest request, JevProtocol protocol, string provider, int attempts, string? requestId)
    {
        try
        {
            using (var document = JsonDocument.Parse(body))
            {
                var root = document.RootElement;
                RejectDuplicates(root);
                var answers = new Dictionary<string, JevAnswer>(StringComparer.Ordinal);
                var source = root.GetProperty("answers");
                foreach (var property in source.EnumerateObject())
                {
                    if (!request.Questions.TryGetProperty(property.Name, out var question))
                    {
                        throw new JsonException();
                    }

                    var answer = property.Value;
                    var type = answer.GetProperty("type").GetString();
                    if (type != question.GetProperty("type").GetString())
                    {
                        throw new JsonException();
                    }

                    var confidence = OptionalNumber(answer, "confidence", 0, 1);
                    JevAnswer parsed;
                    switch (type)
                    {
                        case "choice":
                            var selected = answer.GetProperty("choice").GetString() ?? throw new JsonException();
                            var choices = question.GetProperty("criteria").EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                            if (!choices.Contains(selected))
                            {
                                throw new JsonException();
                            }

                            var distribution = Distribution(answer, choices, protocol != JevProtocol.Vercel);
                            if (protocol != JevProtocol.Vercel && confidence is null)
                            {
                                throw new JsonException();
                            }

                            parsed = new ChoiceAnswer(selected, distribution, confidence);
                            break;
                        case "score":
                            var levelCount = question.GetProperty("criteria").GetArrayLength();
                            var levels = Enumerable.Range(0, levelCount).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToHashSet(StringComparer.Ordinal);
                            var score = Number(answer.GetProperty("score"), 0, levelCount - 1);
                            var probabilities = Distribution(answer, levels, protocol != JevProtocol.Vercel);
                            Dictionary<string, JevValue>? legend = null;
                            if (answer.TryGetProperty("legend", out var legendElement))
                            {
                                legend = legendElement.EnumerateObject().ToDictionary(p => p.Name, p => JevValue.FromJson(p.Value.GetRawText()), StringComparer.Ordinal);
                                if (!levels.SetEquals(legend.Keys))
                                {
                                    throw new JsonException();
                                }
                            }

                            if (protocol != JevProtocol.Vercel && (confidence is null || legend is null))
                            {
                                throw new JsonException();
                            }

                            parsed = new ScoreAnswer(score, probabilities, legend?.AsReadOnly(), confidence);
                            break;
                        case "noul":
                        case "boolean":
                            parsed = new NoulAnswer(Number(answer.GetProperty(type == "noul" ? "noul" : "probability"), 0, 1));
                            break;
                        default:
                            throw new JsonException();
                    }

                    parsed = parsed with
                    {
                        AdditionalData = Extra(answer, "type", "choice", "score", "noul", "probability", "confidence", "probabilities", "legend")
                    };
                    answers.Add(property.Name, parsed);
                }

                if (answers.Count != request.Questions.EnumerateObject().Count())
                {
                    throw new JsonException();
                }

                var model = OptionalString(root, "model");
                JevUsage? usage = null;
                if (root.TryGetProperty("usage", out var u))
                {
                    var camel = protocol == JevProtocol.Vercel;
                    var input = OptionalCount(u, camel ? "inputTokens" : "input_tokens");
                    var output = OptionalCount(u, camel ? "outputTokens" : "output_tokens");
                    decimal? cost = u.TryGetProperty("cost", out var c) ? c.GetDecimal() : null;
                    if (cost < 0)
                    {
                        throw new JsonException();
                    }

                    usage = new(input, output, cost)
                    {
                        AdditionalData = Extra(u, "input_tokens", "output_tokens", "inputTokens", "outputTokens", "cost")
                    };
                }

                if (protocol != JevProtocol.Vercel && (model is null || usage is null))
                {
                    throw new JsonException();
                }

                return new()
                {
                    Answers = answers.AsReadOnly(),
                    RequestedModel = request.Model,
                    Model = model,
                    Id = OptionalString(root, "id"),
                    Provider = OptionalString(root, "provider"),
                    RequestId = requestId,
                    Usage = usage,
                    ProviderMetadata = OptionalValue(root, "providerMetadata"),
                    Rounding = OptionalValue(root, "rounding"),
                    Warnings = ReadWarnings(root),
                    AdditionalData = Extra(root, "answers", "model", "id", "provider", "usage", "providerMetadata", "rounding", "warnings")
                };
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
        {
            throw new JevInvalidResponseException(provider, attempts, requestId);
        }
    }

    /// <summary>Rejects ambiguous duplicate JSON properties, including nested answers.</summary>
    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException();
                }

                RejectDuplicates(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in value.EnumerateArray())
            {
                RejectDuplicates(child);
            }
        }
    }

    /// <summary>Reads an optional probability distribution and validates its keys and values.</summary>
    private static IReadOnlyDictionary<string, double>? Distribution(JsonElement answer, HashSet<string> allowed, bool required)
    {
        if (!answer.TryGetProperty("probabilities", out var element))
        {
            return required ? throw new JsonException() : null;
        }

        var values = element.EnumerateObject().ToDictionary(p => p.Name, p => Number(p.Value, 0, 1), StringComparer.Ordinal);
        if (!allowed.SetEquals(values.Keys))
        {
            throw new JsonException();
        }

        return values.AsReadOnly();
    }

    /// <summary>Reads a finite number within the permitted range.</summary>
    private static double Number(JsonElement value, double min, double max)
    {
        var number = value.GetDouble();
        return double.IsFinite(number) && number >= min && number <= max ? number : throw new JsonException();
    }

    /// <summary>Reads an optional bounded number.</summary>
    private static double? OptionalNumber(JsonElement value, string name, double min, double max) => value.TryGetProperty(name, out var number) ? Number(number, min, max) : null;
    /// <summary>Reads an optional nonnegative token count.</summary>
    private static long? OptionalCount(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var count))
        {
            return null;
        }

        var number = count.GetInt64();
        return number >= 0 ? number : throw new JsonException();
    }

    /// <summary>Reads an optional nullable string.</summary>
    private static string? OptionalString(JsonElement value, string name) => value.TryGetProperty(name, out var text) ? text.GetString() : null;
    /// <summary>Copies an optional JSON value.</summary>
    private static JevValue? OptionalValue(JsonElement value, string name) => value.TryGetProperty(name, out var found) ? JevValue.FromJson(found.GetRawText()) : null;
    /// <summary>Preserves unknown JSON properties independently of the source document.</summary>
    private static IReadOnlyDictionary<string, JsonElement> Extra(JsonElement value, params string[] known) => value.EnumerateObject()
        .Where(p => !known.Contains(p.Name, StringComparer.Ordinal))
        .ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal)
        .AsReadOnly();

    /// <summary>Copies optional provider warnings without flattening their JSON structure.</summary>
    private static IReadOnlyList<JevValue> ReadWarnings(JsonElement root)
    {
        if (!root.TryGetProperty("warnings", out var warnings))
        {
            return [];
        }
        return Array.AsReadOnly(warnings.EnumerateArray()
            .Select(value => JevValue.FromJson(value.GetRawText())).ToArray());
    }
}
