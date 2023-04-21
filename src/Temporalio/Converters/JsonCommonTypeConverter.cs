using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Temporalio.Converters
{
    /// <summary>
    /// Converter to/from JSON from/to common types.
    /// </summary>
    /// <remarks>
    /// This does default serialization, but on deserialization it is built to convert to well-known
    /// .NET types. Specifically, objects become <c>Dictionary&lt;string, object?&gt;</c>, arrays
    /// become <c>List&lt;object?&gt;</c>, numbers that can convert to long as long, other numbers
    /// as doubles, and strings, bools, and nulls as expected.
    /// </remarks>
    public class JsonCommonTypeConverter : JsonConverter<object?>
    {
        /// <summary>
        /// Singleton instance of this converter.
        /// </summary>
        public static readonly JsonCommonTypeConverter Instance = new();

        /// <inheritdoc />
        public override object? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) => reader.TokenType switch
            {
                JsonTokenType.StartObject =>
                    JsonSerializer.Deserialize<Dictionary<string, object?>>(ref reader, options),
                JsonTokenType.StartArray =>
                    JsonSerializer.Deserialize<List<object?>>(ref reader, options),
                JsonTokenType.String => reader.GetString()!,
                JsonTokenType.Number when reader.TryGetInt64(out long l) => l,
                JsonTokenType.Number => reader.GetDouble(),
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Null => null,
                _ => throw new JsonException($"Unrecognized type: {reader.TokenType}"),
            };

        /// <inheritdoc />
        public override void Write(
            Utf8JsonWriter writer, object? value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(
                writer,
                value,
                value?.GetType() ?? typeof(object),
                options);
    }
}