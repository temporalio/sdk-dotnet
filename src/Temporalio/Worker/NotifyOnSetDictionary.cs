using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Temporalio.Worker
{
    /// <summary>
    /// Specialized dictionary that invokes a callback on each value set (but not value delete).
    /// </summary>
    /// <typeparam name="TKey">Dictionary key type.</typeparam>
    /// <typeparam name="TValue">Dictionary value type.</typeparam>
    /// <remarks>
    /// This is not thread safe.
    /// </remarks>
    internal class NotifyOnSetDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> dict;
        private readonly Action<TKey, TValue> callback;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotifyOnSetDictionary{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="copyFrom">Dictionary to copy existing values from.</param>
        /// <param name="callback">Callback to invoke on value set.</param>
        public NotifyOnSetDictionary(
            IEnumerable<KeyValuePair<TKey, TValue>> copyFrom, Action<TKey, TValue> callback)
        {
            // Can't use kvp enumerable constructor on Dictionary, too new
            dict = copyFrom.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            this.callback = callback;
        }

        /// <inheritdoc />
        public ICollection<TKey> Keys => dict.Keys;

        /// <inheritdoc />
        public ICollection<TValue> Values => dict.Values;

        /// <inheritdoc />
        public int Count => dict.Count;

        /// <inheritdoc />
        public bool IsReadOnly => false;

        /// <inheritdoc />
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => dict.Keys;

        /// <inheritdoc />
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => dict.Values;

        /// <inheritdoc />
        public TValue this[TKey key]
        {
            get
            {
                return dict[key];
            }

            set
            {
                Set(key, value, true);
            }
        }

        /// <inheritdoc />
        public void Add(TKey key, TValue value) => Set(key, value, false);

        /// <inheritdoc />
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        /// <inheritdoc />
        public void Clear() => dict.Clear();

        /// <inheritdoc />
        public bool Contains(KeyValuePair<TKey, TValue> item) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)dict).Contains(item);

        /// <inheritdoc />
        public bool ContainsKey(TKey key) => dict.ContainsKey(key);

        /// <inheritdoc />
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)dict).CopyTo(array, arrayIndex);

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => dict.GetEnumerator();

        /// <inheritdoc />
        public bool Remove(TKey key) => dict.Remove(key);

        /// <inheritdoc />
        public bool Remove(KeyValuePair<TKey, TValue> item) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)dict).Remove(item);

        /// <inheritdoc />
        public bool TryGetValue(TKey key, out TValue value) =>
#pragma warning disable CS8601
            dict.TryGetValue(key, out value);
#pragma warning disable CS8601

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => dict.GetEnumerator();

        private void Set(TKey key, TValue value, bool overwrite)
        {
            if (!overwrite && ContainsKey(key))
            {
                throw new ArgumentException("Key already exists");
            }
            dict[key] = value;
            callback(key, value);
        }
    }
}