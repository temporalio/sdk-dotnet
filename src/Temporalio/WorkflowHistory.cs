using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Temporalio.Api.Failure.V1;
using Temporalio.Api.History.V1;

namespace Temporalio
{
    /// <summary>
    /// History for a workflow.
    /// </summary>
    /// <param name="ID">ID of the workflow.</param>
    /// <param name="Events">Collection of events.</param>
    public record WorkflowHistory(string ID, IReadOnlyCollection<HistoryEvent> Events)
    {
        /// <summary>
        /// Interface for fixes to apply.
        /// </summary>
        internal interface IFixPath
        {
            /// <summary>
            /// Gets the field path for the fix.
            /// </summary>
            string[] Fields { get; }
        }

        /// <summary>
        /// Convert this workflow history to JSON. Note, the JSON does not contain the ID.
        /// </summary>
        /// <returns>String with the workflow history.</returns>
        public string ToJson() =>
            JsonFormatter.Default.Format(new History() { Events = { Events } });

        /// <summary>
        /// Convert this workflow history to JSON and write to writer. Note, the JSON does not
        /// contain the ID.
        /// </summary>
        /// <param name="writer">Where to write the JSON.</param>
        public void ToJson(TextWriter writer) =>
            JsonFormatter.Default.Format(new History() { Events = { Events } }, writer);

        /// <summary>
        /// Create workflow history from the given JSON. While this works with "ToJson" from this
        /// class, it also works with CLI and UI exported JSON.
        /// </summary>
        /// <param name="workflowID">Workflow ID.</param>
        /// <param name="json">JSON to create history from.</param>
        /// <returns>Created history.</returns>
        public static WorkflowHistory FromJson(string workflowID, string json)
        {
            var historyRaw = JsonSerializer.Deserialize<object?>(json, JsonCommonTypeConverter.SerializerOptions);
            if (historyRaw is not Dictionary<string, object?> historyObj)
            {
                throw new ArgumentException("JSON is not expected history object");
            }
            return FromJson(workflowID, historyObj);
        }

        private static WorkflowHistory FromJson(string workflowID, Dictionary<string, object?> historyObj)
        {
            // Unfortunately due to enum incorrectness in UI/tctl/Go, we have to parse, alter,
            // serialize the altered, then deserialize again using Proto JSON
            HistoryJsonFixer.Fix(historyObj);
            var fixedJson = JsonSerializer.Serialize(historyObj);
            var history = JsonParser.Default.Parse<History>(fixedJson);
            return new(workflowID, history.Events);
        }

        /// <summary>
        /// Converter to/from JSON from/to common types.
        /// </summary>
        internal class JsonCommonTypeConverter : JsonConverter<object?>
        {
            /// <summary>
            /// Options that include the common type converter.
            /// </summary>
            public static readonly JsonSerializerOptions SerializerOptions = new()
            {
                Converters = { new JsonCommonTypeConverter() },
            };

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
                Utf8JsonWriter writer, object? objectToWrite, JsonSerializerOptions options) =>
                JsonSerializer.Serialize(
                    writer,
                    objectToWrite,
                    objectToWrite?.GetType() ?? typeof(object),
                    options);
        }

        /// <summary>
        /// Fixer for fixing history enumerates that are proto-JSON incompatible.
        /// </summary>
        internal static class HistoryJsonFixer
        {
            // TODO(cretz): Do this at compile time?
            private static readonly Lazy<IReadOnlyCollection<IFixPath>> HistoryFixes = new(
                () =>
                {
                    var paths = new List<IFixPath>();
                    CollectPathsToFix(typeof(History), new(), paths, new(), new());
                    return paths;
                },
                true);

            // TODO(cretz): Do this at compile time?
            private static readonly Lazy<IReadOnlyCollection<IFixPath>> FailureFixes = new(
                () =>
                {
                    var paths = new List<IFixPath>();
                    CollectPathsToFix(typeof(Failure), new(), paths, new(), new());
                    return paths;
                },
                true);

            /// <summary>
            /// Fix the history object.
            /// </summary>
            /// <param name="historyObj">History object to fix.</param>
            public static void Fix(Dictionary<string, object?> historyObj)
            {
                foreach (var historyFix in HistoryFixes.Value)
                {
                    Fix(historyFix, 0, historyObj);
                }
            }

            private static void Fix(
                IFixPath fix,
                int fieldIndex,
                Dictionary<string, object?> obj)
            {
                if (!obj.TryGetValue(fix.Fields[fieldIndex], out var prop) || prop == null)
                {
                    return;
                }
                switch (prop)
                {
                    case string str when fix.Fields.Length == fieldIndex + 1 && fix is FixEnumPath enumFix:
                        // Only change if in map
                        if (enumFix.BadNameToGoodName.TryGetValue(str, out var goodName))
                        {
                            obj[fix.Fields[fieldIndex]] = goodName;
                        }
                        return;
                    case Dictionary<string, object?> failDict when fix.Fields.Length == fieldIndex + 1 && fix is FixFailurePath:
                        // Apply failure fixes, which means it will recurse into cause
                        foreach (var failureFix in FailureFixes.Value)
                        {
                            Fix(failureFix, 0, failDict);
                        }
                        return;
                    case Dictionary<string, object?> dict when fix.Fields.Length > fieldIndex + 1:
                        Fix(fix, fieldIndex + 1, dict);
                        return;
                    case List<object?> list when fix.Fields.Length > fieldIndex + 1:
                        foreach (var item in list)
                        {
                            if (item is not Dictionary<string, object?> dict)
                            {
                                throw new JsonException(
                                    $"List item type {item?.GetType()} unexpected with field index {fieldIndex} for fix {fix}");
                            }
                            Fix(fix, fieldIndex + 1, dict);
                        }
                        return;
                    default:
                        throw new JsonException(
                            $"Got type {prop.GetType()} unexpectedly and field index {fieldIndex} for fix {fix}");
                }
            }

            private static void CollectPathsToFix(
                Type current,
                List<string> currentFieldPath,
                List<IFixPath> paths,
                List<Type> seen,
                Dictionary<Type, IReadOnlyDictionary<string, string>?> badNameToGoodNameCache)
            {
                // No current case where we need to recurse back into same type except failure cause
                // which we handle explicitly
                if (seen.Contains(current))
                {
                    return;
                }
                seen = ListAppend(seen, current);
                foreach (var prop in current.GetProperties())
                {
                    if (typeof(Failure).IsAssignableFrom(prop.PropertyType))
                    {
                        paths.Add(new FixFailurePath(AppendPropertyName(currentFieldPath, prop.Name).ToArray()));
                    }
                    else if (typeof(ICollection).IsAssignableFrom(prop.PropertyType) &&
                        typeof(IMessage).IsAssignableFrom(prop.PropertyType.GenericTypeArguments[0]))
                    {
                        CollectPathsToFix(
                            prop.PropertyType.GenericTypeArguments[0],
                            AppendPropertyName(currentFieldPath, prop.Name),
                            paths,
                            seen,
                            badNameToGoodNameCache);
                    }
                    else if (typeof(IDictionary).IsAssignableFrom(prop.PropertyType) &&
                        prop.PropertyType.GenericTypeArguments[0] == typeof(string) &&
                        typeof(IMessage).IsAssignableFrom(prop.PropertyType.GenericTypeArguments[1]))
                    {
                        // We are not currently built to handle map fields so make sure none
                        // apply
                        var preCount = paths.Count;
                        CollectPathsToFix(
                            prop.PropertyType.GenericTypeArguments[1],
                            AppendPropertyName(currentFieldPath, prop.Name),
                            paths,
                            seen,
                            badNameToGoodNameCache);
                        if (paths.Count > preCount)
                        {
                            var pathStr = string.Join("->", AppendPropertyName(currentFieldPath, prop.Name));
                            throw new InvalidOperationException($"Map value at path {pathStr} had some child enum");
                        }
                    }
                    else if (typeof(IMessage).IsAssignableFrom(prop.PropertyType))
                    {
                        CollectPathsToFix(
                            prop.PropertyType,
                            AppendPropertyName(currentFieldPath, prop.Name),
                            paths,
                            seen,
                            badNameToGoodNameCache);
                    }
                    else if (prop.PropertyType.IsEnum)
                    {
                        var badNameToGoodName = GetOrCreateBadNameToGoodName(
                            prop.PropertyType, badNameToGoodNameCache);
                        if (badNameToGoodName != null)
                        {
                            var fields = AppendPropertyName(currentFieldPath, prop.Name).ToArray();
                            paths.Add(new FixEnumPath(fields, badNameToGoodName));
                        }
                    }
                }
            }

            private static IReadOnlyDictionary<string, string>? GetOrCreateBadNameToGoodName(
                Type enumType, Dictionary<Type, IReadOnlyDictionary<string, string>?> cache)
            {
                if (cache.TryGetValue(enumType, out var res))
                {
                    return res;
                }
                Dictionary<string, string> badNameToGoodName = new();
                foreach (var member in enumType.GetMembers())
                {
                    var origName = member.GetCustomAttribute<OriginalNameAttribute>();
                    if (origName != null)
                    {
                        badNameToGoodName[member.Name] = origName.Name;
                    }
                }
                var maybe = badNameToGoodName.Count > 0 ? badNameToGoodName : null;
                cache[enumType] = maybe;
                return maybe;
            }

            private static List<string> AppendPropertyName(List<string> path, string propName)
            {
                // Just confirm it starts with a capital letter, but not two
                if (!char.IsUpper(propName[0]) || char.IsUpper(propName[1]))
                {
                    throw new InvalidOperationException($"Invalid property name {propName}");
                }
                // Just lowercase the first character
                return ListAppend(path, char.ToLower(propName[0]) + propName.Substring(1));
            }

            // Enumerable.Append is not in all versions we support
            private static List<T> ListAppend<T>(List<T> orig, T newItem)
            {
                var plusOne = new List<T>(orig.Count + 1);
                plusOne.AddRange(orig);
                plusOne.Add(newItem);
                return plusOne;
            }
        }

        /// <summary>
        /// Fix for enum from field path.
        /// </summary>
        /// <param name="Fields">Field path.</param>
        /// <param name="BadNameToGoodName">Dictionary with name mapping.</param>
        internal record FixEnumPath(
            string[] Fields, IReadOnlyDictionary<string, string> BadNameToGoodName) : IFixPath;

        /// <summary>
        /// Fix for failure from field path.
        /// </summary>
        /// <param name="Fields">Field path.</param>
        internal record FixFailurePath(string[] Fields) : IFixPath;
    }
}