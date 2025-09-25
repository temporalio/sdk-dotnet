using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Converters;

namespace Temporalio.Common
{
    /// <summary>
    /// Read-only collection of typed search attributes. Use a <see cref="Builder" /> to create this
    /// collection manually or <see cref="Workflows.Workflow.UpsertTypedSearchAttributes" /> to
    /// update from inside a workflow.
    /// </summary>
    /// <remarks>
    /// When used inside a workflow, this collection is read-only from a usage perspective but can
    /// be mutated during workflow run so it is not strictly immutable. From a client perspective,
    /// this collection is immutable.
    /// </remarks>
    public class SearchAttributeCollection : IReadOnlyCollection<SearchAttributeKey>
    {
        /// <summary>
        /// An empty search attribute collection.
        /// </summary>
        public static readonly SearchAttributeCollection Empty = new();

        private static readonly Dictionary<ByteString, IndexedValueType> NameToIndexType =
            Enum.GetValues(typeof(IndexedValueType)).Cast<IndexedValueType>().ToDictionary(
                t => ByteString.CopyFromUtf8(Enum.GetName(typeof(IndexedValueType), t)),
                t => t);

        private static readonly Dictionary<IndexedValueType, ByteString> IndexedTypeToName =
            NameToIndexType.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        // Mostly immutable except for ApplyUpdates which intentionally mutates
        private readonly SortedDictionary<SearchAttributeKey, object> values;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchAttributeCollection"/> class.
        /// </summary>
        internal SearchAttributeCollection()
            : this(new())
        {
        }

        private SearchAttributeCollection(SortedDictionary<SearchAttributeKey, object> values) =>
            this.values = values;

        /// <summary>
        /// Gets the number of search attributes.
        /// </summary>
        public int Count => values.Count;

        /// <summary>
        /// Gets a view of the untyped search attribute values.
        /// </summary>
        public IReadOnlyDictionary<SearchAttributeKey, object> UntypedValues => values;

        /// <summary>
        /// Convert a protobuf search attribute collection to this collection.
        /// </summary>
        /// <param name="searchAttributes">Proto search attributes.</param>
        /// <returns>Search attribute collection.</returns>
        public static SearchAttributeCollection FromProto(SearchAttributes searchAttributes)
        {
            var dict = new SortedDictionary<SearchAttributeKey, object>();
            foreach (var kvp in searchAttributes.IndexedFields)
            {
                // Old servers may have null elements, which we ignore
                if (PayloadToObject(kvp.Value) is { } value)
                {
                    dict[new(kvp.Key, value.Type)] = value.Value;
                }
            }
            return new(dict);
        }

        /// <summary>
        /// Get a search attribute if the key name exists and is the right type.
        /// </summary>
        /// <typeparam name="T">Key type.</typeparam>
        /// <param name="key">Key to find value for.</param>
        /// <returns>Value.</returns>
        /// <exception cref="KeyNotFoundException">If the key name + key type not found.</exception>
        /// <exception cref="InvalidCastException">On older servers and/or older workflows from
        /// other SDKs that used things like lists for types, the cast can fail and
        /// <see cref="UntypedValues" /> may have to be used directly.</exception>
        public T Get<T>(SearchAttributeKey<T> key)
            where T : notnull =>
            // Intentionally let cast fail here
            values.TryGetValue(key, out var value) ? (T)value :
                throw new KeyNotFoundException($"{key.Name} not found or was present with different type");

        /// <summary>
        /// Try to get the search attribute if the key exists and is the right type.
        /// </summary>
        /// <typeparam name="T">Key type.</typeparam>
        /// <param name="key">Key to find value for.</param>
        /// <param name="value">Value to set.</param>
        /// <returns>False if the key name + key type is not found, true if found and set.</returns>
        /// <remarks>
        /// On older servers and/or older workflows from
        /// other SDKs that used things like lists for types, the cast can fail and
        /// <see cref="UntypedValues" /> may have to be used directly.
        /// </remarks>
        public bool TryGetValue<T>(SearchAttributeKey<T> key, out T value)
            where T : notnull
        {
            if (values.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }
            value = default!;
            return false;
        }

        /// <summary>
        /// Check whether the key name + key type is present.
        /// </summary>
        /// <typeparam name="T">Key type.</typeparam>
        /// <param name="key">Key to check for.</param>
        /// <returns>False if the key name + key type are not present, true otherwise.</returns>
        /// <remarks>
        /// It is possible for this to be false but <see cref="ContainsKey(string)" /> to be true
        /// if the key type is different than what is passed here.
        /// </remarks>
        public bool ContainsKey<T>(SearchAttributeKey<T> key)
            where T : notnull => values.ContainsKey(key);

        /// <summary>
        /// Check whether the key name exists.
        /// </summary>
        /// <param name="key">Name to check for.</param>
        /// <returns>Whether key name exists.</returns>
        public bool ContainsKey(string key) => values.Keys.Any(k => k.Name == key);

        /// <inheritdoc />
        public IEnumerator<SearchAttributeKey> GetEnumerator() =>
            UntypedValues.Keys.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => UntypedValues.Keys.GetEnumerator();

        /// <summary>
        /// Convert this collection to protobuf search attributes.
        /// </summary>
        /// <returns>Protobuf search attributes.</returns>
        public SearchAttributes ToProto() => new()
        {
            IndexedFields =
            {
                values.ToDictionary(
                    kvp => kvp.Key.Name,
                    kvp =>
                    {
                        try
                        {
                            var value = kvp.Value;
                            if (value == null)
                            {
                                throw new ArgumentException("Search attribute cannot be null");
                            }
                            return ToAttributePayload(kvp.Key, kvp.Value);
                        }
                        catch (ArgumentException e)
                        {
                            throw new ArgumentException($"Failed converting search attribute {kvp.Key}", e);
                        }
                    }),
            },
        };

        /// <summary>
        /// Convert a search attribute payload to an object.
        /// </summary>
        /// <param name="payload">Payload to convert.</param>
        /// <returns>
        /// Object or null if the payload came from an older server which supported null search
        /// attribute payloads.
        /// </returns>
        internal static (object Value, IndexedValueType Type)? PayloadToObject(Payload payload)
        {
            if (!payload.Metadata.TryGetValue("type", out var typeName))
            {
                throw new ArgumentException("Metadata type missing");
            }
            if (!NameToIndexType.TryGetValue(typeName, out var valueType))
            {
                throw new ArgumentException($"Unrecognized metadata type {typeName.ToStringUtf8()}");
            }
            // Convert to JSON element
            if (DataConverter.Default.PayloadConverter.ToValue<JsonElement?>(payload) is not JsonElement elem ||
                elem.ValueKind == JsonValueKind.Null)
            {
                // Old servers may have null elements, which we ignore
                return null;
            }
            // Convert element
            static object JsonElementToObject(JsonElement elem, IndexedValueType valueType) => elem.ValueKind switch
            {
                JsonValueKind.Array =>
                    elem.EnumerateArray().Select(j => JsonElementToObject(j, valueType)).OfType<string>().ToList(),
                JsonValueKind.False or JsonValueKind.True =>
                    elem.GetBoolean(),
                JsonValueKind.Number when valueType == IndexedValueType.Int =>
                    elem.GetInt64(),
                JsonValueKind.Number =>
                    elem.GetDouble(),
                JsonValueKind.String when valueType == IndexedValueType.Datetime =>
                    elem.TryGetDateTimeOffset(out var d) ? d : elem.GetString()!,
                JsonValueKind.String =>
                    elem.GetString()!,
                _ =>
                    throw new ArgumentException($"Unexpected search attribute kind {elem.ValueKind}"),
            };
            return (JsonElementToObject(elem, valueType), valueType);
        }

        /// <summary>
        /// Convert the key and value to a proto payload.
        /// </summary>
        /// <param name="key">Key to convert.</param>
        /// <param name="value">Value to set or null to be considered unset.</param>
        /// <returns>Payload.</returns>
        internal static Payload ToAttributePayload(SearchAttributeKey key, object? value)
        {
            // We intentionally are not over-validating here. We have to support
            // opaque write-through of old search attribute values which may be
            // things like lists of bools or something.
            var payload = DataConverter.Default.PayloadConverter.ToPayload(value);
            payload.Metadata["type"] = IndexedTypeToName[key.ValueType];
            return payload;
        }

        /// <summary>
        /// Mutate this collection with workflow updates. This is not thread safe.
        /// </summary>
        /// <param name="updates">Update to apply.</param>
        /// <exception cref="ArgumentException">If update validation fails.</exception>
        internal void ApplyUpdates(IReadOnlyCollection<Workflows.SearchAttributeUpdate> updates)
        {
            // Validate, then apply updates. Here are the potential validations we can do:
            // 1. "Multiple updates for the same key name"
            // 2. "Value being set doesn't match key type being used"
            // 3. "Setting key/value to different type than previous key/value"
            // 4. "Unsetting key/value with key as different type than previous key"
            // 5. "Unsetting value that doesn't exist"
            // We will validate 1. 2 should be impossible. 3, 4, and 5 will not be validated by
            // intention (we will let server catch most of this).
            var seenKeys = new HashSet<string>(); // No capacity hint allowed in < 4.7
            // We have to validate before performing any mutation
            foreach (var update in updates)
            {
                if (!seenKeys.Add(update.UntypedKey.Name))
                {
                    throw new ArgumentException($"Multiple updates seen for key {update.UntypedKey.Name}");
                }
            }
            foreach (var update in updates)
            {
                // Since we don't validate that a key has the same type, we must remove the key by
                // name, not its hash which is key + type.
                var existingKey = values.Keys.FirstOrDefault(k => k.Name == update.UntypedKey.Name);
                if (existingKey != null)
                {
                    values.Remove(existingKey);
                }
                // Non-null object means set value
                if (update.HasValue)
                {
                    values[update.UntypedKey] = update.UntypedValue;
                }
            }
        }

        /// <summary>
        /// Builder for creating <see cref="SearchAttributeCollection" />. This builder is not
        /// thread safe.
        /// </summary>
        public class Builder
        {
            private readonly SortedDictionary<SearchAttributeKey, object> values = new();

            /// <summary>
            /// Initializes a new instance of the <see cref="Builder"/> class to
            /// an empty set.
            /// </summary>
            public Builder()
            {
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="Builder"/> class with a copy of
            /// existing values.
            /// </summary>
            /// <param name="searchAttributes">Existing values to copy.</param>
            public Builder(SearchAttributeCollection searchAttributes) => Set(searchAttributes);

            /// <summary>
            /// Set a specific key and value.
            /// </summary>
            /// <typeparam name="T">Key type.</typeparam>
            /// <param name="key">Key to set.</param>
            /// <param name="value">Value to set.</param>
            /// <returns>This builder for chaining.</returns>
            /// <remarks>
            /// This will replace any existing key of the same name regardless of key type.
            /// </remarks>
            public Builder Set<T>(SearchAttributeKey<T> key, T value)
                where T : notnull => InternalSet(key, value);

            /// <summary>
            /// Set a copy of all values in the given collection as if calling
            /// <see cref="Set{T}(SearchAttributeKey{T}, T)" /> on each.
            /// </summary>
            /// <param name="searchAttributes">Search attributes to copy.</param>
            /// <returns>This builder for chaining.</returns>
            /// <remarks>
            /// This will replace any existing key of the same name regardless of key type.
            /// </remarks>
            public Builder Set(SearchAttributeCollection searchAttributes)
            {
                foreach (var kvp in searchAttributes.UntypedValues)
                {
                    InternalSet(kvp.Key, kvp.Value);
                }
                return this;
            }

            /// <summary>
            /// Remove the key for the given key name regardless of key type.
            /// </summary>
            /// <param name="key">Key with the name to remove.</param>
            /// <returns>This builder for chaining.</returns>
            public Builder Unset(SearchAttributeKey key) => Unset(key.Name);

            /// <summary>
            /// Remove the key for the given key name regardless of key type.
            /// </summary>
            /// <param name="name">Key name to remove.</param>
            /// <returns>This builder for chaining.</returns>
            public Builder Unset(string name)
            {
                var existingKey = values.Keys.FirstOrDefault(k => k.Name == name);
                if (existingKey != null)
                {
                    values.Remove(existingKey);
                }
                return this;
            }

            /// <summary>
            /// Build the search attribute collection.
            /// </summary>
            /// <returns>Built collection.</returns>
            /// <remarks>
            /// This copies the internal values when creating, so this builder can be reused.
            /// </remarks>
            public SearchAttributeCollection ToSearchAttributeCollection() =>
                // Intentionally copying
                new(new(values));

            private Builder InternalSet(SearchAttributeKey key, object value)
            {
                // If there's a key with this name already, remove it
                var existingKey = values.Keys.FirstOrDefault(k => k.Name == key.Name);
                if (existingKey != null)
                {
                    values.Remove(existingKey);
                }
                values[key] = key.NormalizeValue(
                    value ?? throw new ArgumentException("Null value", nameof(value)));
                return this;
            }
        }
    }
}