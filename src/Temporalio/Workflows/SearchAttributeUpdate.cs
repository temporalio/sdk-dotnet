#pragma warning disable SA1402 // We allow same-named types in the same file

using System;
using Temporalio.Api.Common.V1;

namespace Temporalio.Workflows
{
    /// <summary>
    /// A single mutation to a search attribute key. This can be a set or, if
    /// <see cref="HasValue" /> is false, an unset.
    /// </summary>
    public abstract class SearchAttributeUpdate
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SearchAttributeUpdate"/> class.
        /// </summary>
        internal SearchAttributeUpdate()
        {
        }

        /// <summary>
        /// Gets a value indicating whether this update has a value to set or represents an unset.
        /// </summary>
        public bool HasValue { get; internal init; }

        /// <summary>
        /// Gets the untyped form of the key to update.
        /// </summary>
        public abstract SearchAttributeKey UntypedKey { get; }

        /// <summary>
        /// Gets the untyped value to update.
        /// </summary>
        /// <exception cref="InvalidOperationException">If there is no value.</exception>
        public abstract object UntypedValue { get; }

        /// <summary>
        /// Create an update to set a key. Users may prefer
        /// <see cref="SearchAttributeKey{T}.ValueSet(T)" /> instead.
        /// </summary>
        /// <typeparam name="T">Key type.</typeparam>
        /// <param name="key">Key to set.</param>
        /// <param name="value">Value to set.</param>
        /// <returns>Search attribute update.</returns>
        public static SearchAttributeUpdate<T> ValueSet<T>(SearchAttributeKey<T> key, T value)
            where T : notnull => new(key, value);

        /// <summary>
        /// Create an update to unset a key. Users may prefer
        /// <see cref="SearchAttributeKey{T}.ValueUnset" /> instead.
        /// </summary>
        /// <typeparam name="T">Key type.</typeparam>
        /// <param name="key">Key to unset.</param>
        /// <returns>Search attribute update.</returns>
        public static SearchAttributeUpdate<T> ValueUnset<T>(SearchAttributeKey<T> key)
            where T : notnull => new(key);

        /// <summary>
        /// Create a payload from this update.
        /// </summary>
        /// <returns>Payload.</returns>
        internal Payload ToUpsertPayload() =>
            SearchAttributeCollection.ToAttributePayload(UntypedKey, HasValue ? UntypedValue : null);
    }

    /// <summary>
    /// A single mutation to a search attribute key. This can be a set or, if
    /// <see cref="SearchAttributeUpdate.HasValue" /> is false, an unset.
    /// </summary>
    /// <typeparam name="T">Key type.</typeparam>
    public class SearchAttributeUpdate<T> : SearchAttributeUpdate
        where T : notnull
    {
        private readonly T? value;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchAttributeUpdate{T}"/> class. This is
        /// an unset.
        /// </summary>
        /// <param name="key">Key to unset.</param>
        internal SearchAttributeUpdate(SearchAttributeKey<T> key) => Key = key;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchAttributeUpdate{T}"/> class. This is
        /// a set.
        /// </summary>
        /// <param name="key">Key to set.</param>
        /// <param name="value">Value to set.</param>
        internal SearchAttributeUpdate(SearchAttributeKey<T> key, T value)
        {
            Key = key;
            this.value = value;
            HasValue = true;
        }

        /// <summary>
        /// Gets the key to update.
        /// </summary>
        public SearchAttributeKey<T> Key { get; private init; }

        /// <summary>
        /// Gets the value to update or fails if this is an unset.
        /// </summary>
        /// <exception cref="InvalidOperationException">If there is no value.</exception>
        public T Value => HasValue ? value! : throw new InvalidOperationException("No value");

        /// <inheritdoc />
        public override SearchAttributeKey UntypedKey => Key;

        /// <inheritdoc />
        public override object UntypedValue => Value;
    }
}