using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Temporalio.Extensions.ToolRegistry
{
    /// <summary>
    /// Recursively converts <see cref="JsonElement"/> values to CLR types.
    /// </summary>
    /// <remarks>
    /// <c>System.Text.Json</c> deserializes <c>Dictionary&lt;string, object?&gt;</c> with nested
    /// objects as <see cref="JsonElement"/> rather than <c>Dictionary&lt;string, object?&gt;</c>.
    /// This helper materializes those elements into usable CLR types.
    /// </remarks>
    internal static class JsonElementConverter
    {
        /// <summary>
        /// Converts a <see cref="JsonElement"/> to its corresponding CLR type:
        /// Object → Dictionary&lt;string, object?&gt;, Array → List&lt;object?&gt;,
        /// String → string, Number → long or double, Bool → bool, Null → null.
        /// </summary>
        /// <param name="element">The JSON element to convert.</param>
        /// <returns>The converted CLR value.</returns>
        public static object? ConvertElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in element.EnumerateObject())
                    {
                        dict[prop.Name] = ConvertElement(prop.Value);
                    }
                    return dict;

                case JsonValueKind.Array:
                    // If all items are JSON objects return List<Dictionary<string,object?>> so the
                    // type is consistent with in-memory construction (e.g. tool_calls built by the
                    // provider) and pattern-matching in BuildAssistantMessage works after a
                    // checkpoint round-trip. Mixed/primitive/empty arrays fall back to List<object?>.
                    bool anyItems = false;
                    bool allJsonObjects = true;
                    foreach (var arrayItem in element.EnumerateArray())
                    {
                        anyItems = true;
                        if (arrayItem.ValueKind != JsonValueKind.Object)
                        {
                            allJsonObjects = false;
                            break;
                        }
                    }
                    if (anyItems && allJsonObjects)
                    {
                        var dictList = new List<Dictionary<string, object?>>();
                        foreach (var arrayItem in element.EnumerateArray())
                        {
                            var d = new Dictionary<string, object?>();
                            foreach (var prop in arrayItem.EnumerateObject())
                            {
                                d[prop.Name] = ConvertElement(prop.Value);
                            }
                            dictList.Add(d);
                        }
                        return dictList;
                    }
                    var mixedList = new List<object?>();
                    foreach (var arrayItem in element.EnumerateArray())
                    {
                        mixedList.Add(ConvertElement(arrayItem));
                    }
                    return mixedList;

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var longVal))
                    {
                        return longVal;
                    }
                    return element.GetDouble();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    throw new InvalidOperationException($"Unexpected JsonValueKind: {element.ValueKind}");
            }
        }

        /// <summary>
        /// Materializes all <see cref="JsonElement"/> values within a dictionary.
        /// </summary>
        /// <param name="dict">Dictionary whose values may be <see cref="JsonElement"/> instances.</param>
        /// <returns>New dictionary with all <see cref="JsonElement"/> values converted to CLR types.</returns>
        public static Dictionary<string, object?> Materialize(Dictionary<string, object?> dict)
        {
            var result = new Dictionary<string, object?>(dict.Count);
            foreach (var kvp in dict)
            {
                result[kvp.Key] = kvp.Value is JsonElement elem ? ConvertElement(elem) : kvp.Value;
            }
            return result;
        }

        /// <summary>
        /// Materializes all <see cref="JsonElement"/> values within a list of dictionaries.
        /// </summary>
        /// <param name="list">List of dictionaries whose values may be <see cref="JsonElement"/> instances.</param>
        /// <returns>New list with all <see cref="JsonElement"/> values converted to CLR types.</returns>
        public static List<Dictionary<string, object?>> MaterializeList(
            List<Dictionary<string, object?>> list)
        {
            var result = new List<Dictionary<string, object?>>(list.Count);
            foreach (var item in list)
            {
                result.Add(Materialize(item));
            }
            return result;
        }
    }
}
