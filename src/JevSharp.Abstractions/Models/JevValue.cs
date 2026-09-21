using System.Text.Json;

namespace JevSharp.Abstractions.Models;

/// <summary>An immutable JSON value used for state, instructions, and criteria.</summary>
public sealed class JevValue
{
    private readonly JsonElement value;
    /// <summary>Copies a JSON value so it outlives its source document.</summary>
    private JevValue(JsonElement value) => this.value = value.Clone();
    /// <summary>Gets an explicit JSON null value.</summary>
    public static JevValue Null { get; } = FromObject<object?>(null);
    /// <summary>Gets the JSON representation; its backing storage is owned by this value.</summary>
    public JsonElement Json => value;

    /// <summary>Creates a value from a CLR object, retaining its JSON structure.</summary>
    /// <typeparam name="T">The value's CLR type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">Optional serialization settings for this value.</param>
    /// <returns>An immutable JSON value.</returns>
    /// <exception cref="JsonException">The object cannot be serialized.</exception>
    /// <exception cref="NotSupportedException">The type is unsupported by the serializer.</exception>
    public static JevValue FromObject<T>(T value, JsonSerializerOptions? options = null) => new(JsonSerializer.SerializeToElement(value, options));
    /// <summary>Parses JSON without encoding it as a JSON string.</summary>
    /// <param name="json">A complete JSON value.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="ArgumentNullException">The input is null.</exception>
    /// <exception cref="JsonException">The input is invalid JSON.</exception>
    public static JevValue FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using (var document = JsonDocument.Parse(json))
        {
            return new(document.RootElement);
        }
    }

    /// <summary>Converts text to a JSON string, or null to JSON null.</summary>
    /// <param name="text">The text to wrap.</param>
    /// <returns>The JSON value.</returns>
    public static implicit operator JevValue(string? text) => FromObject(text);
    /// <summary>Returns the JSON representation. It can contain sensitive application data.</summary>
    /// <returns>The serialized JSON.</returns>
    public override string ToString() => value.GetRawText();
}
