using System;
using Temporalio.Api.Common.V1;
using System.Collections.Generic;
using System.Collections;

namespace Temporalio {
    public class SearchAttributeDictionary :
        ICollection,
        ICollection<KeyValuePair<string, Payload>>,
        IDictionary,
        IDictionary<string, Payload>,
        IEnumerable,
        IEnumerable<KeyValuePair<string, Payload>>,
        IEquatable<SearchAttributeDictionary>,
        IReadOnlyCollection<KeyValuePair<string, Payload>>,
        IReadOnlyDictionary<string, Payload>
    {
        private readonly Google.Protobuf.Collections.MapField<string, Payload> map;

        public SearchAttributeDictionary() : this(new()) { }

        public SearchAttributeDictionary(SearchAttributes attributes, bool readOnly = false)
            : this(attributes.IndexedFields, readOnly) { }

        private SearchAttributeDictionary(Google.Protobuf.Collections.MapField<string, Payload> map, bool readOnly)
        {
            this.map = map;
            IsReadOnly = readOnly;
        }

        /// <inheritdoc/>
        public bool IsReadOnly { get; private init; }

        /// <inheritdoc/>
        public ICollection<string> Keys => map.Keys;

        /// <inheritdoc/>
        public ICollection<Payload> Values => map.Values;

        /// <inheritdoc/>
        public int Count => map.Count;

        /// <inheritdoc/>
        bool IDictionary.IsFixedSize => IsReadOnly;

        /// <inheritdoc/>
        ICollection IDictionary.Keys => (ICollection)Keys;

        /// <inheritdoc/>
        ICollection IDictionary.Values => (ICollection)Values;

        /// <inheritdoc/>
        bool ICollection.IsSynchronized => false;

        /// <inheritdoc/>
        object ICollection.SyncRoot => this;

        /// <inheritdoc/>
        IEnumerable<string> IReadOnlyDictionary<string, Payload>.Keys => Keys;

        /// <inheritdoc/>
        IEnumerable<Payload> IReadOnlyDictionary<string, Payload>.Values => Values;

        /// <inheritdoc/>
        public Payload this[string key]
        {
            get => map[key];
            set
            {
                AssertNotReadOnly();
                map[key] = value;
            }
        }

        /// <inheritdoc/>
        object? IDictionary.this[object key]
        {
            get => ((IDictionary)map)[key];
            set
            {
                this[(string)key] = value as Payload ?? throw new ArgumentNullException();
            }
        }

        public void AddBool(string key, bool value)
        {
            throw new System.NotImplementedException();
        }

        public void AddDateTime(string key, DateTime value)
        {
            throw new System.NotImplementedException();
        }

        public void AddDouble(string key, double value)
        {
            throw new System.NotImplementedException();
        }

        public void AddInt(string key, int value)
        {
            throw new System.NotImplementedException();
        }

        public void AddLong(string key, long value)
        {
            throw new System.NotImplementedException();
        }

        public void AddKeywords(string key, IEnumerable<string> values)
        {
            throw new System.NotImplementedException();
        }

        public void AddText(string key, string value)
        {
            throw new System.NotImplementedException();
        }

        public void SetBool(string key, bool value)
        {
            throw new System.NotImplementedException();
        }

        public void SetDateTime(string key, DateTime value)
        {
            throw new System.NotImplementedException();
        }

        public void SetDouble(string key, double value)
        {
            throw new System.NotImplementedException();
        }

        public void SetInt(string key, int value)
        {
            throw new System.NotImplementedException();
        }

        public void SetLong(string key, long value)
        {
            throw new System.NotImplementedException();
        }

        public void SetKeywords(string key, IEnumerable<string> values)
        {
            throw new System.NotImplementedException();
        }

        public void SetText(string key, string value)
        {
            throw new System.NotImplementedException();
        }

        public SearchAttributeDictionary AsReadOnly() => IsReadOnly ? this : new(map, true);

        /// <inheritdoc/>
        public void Add(string key, Payload value)
        {
            AssertNotReadOnly();
            map.Add(key, value);
        }

        /// <inheritdoc/>
        public bool ContainsKey(string key) => map.ContainsKey(key);

        /// <inheritdoc/>
        public bool Remove(string key)
        {
            AssertNotReadOnly();
            return map.Remove(key);
        }

        /// <inheritdoc/>
        public bool TryGetValue(string key, out Payload value) => map.TryGetValue(key, out value);

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, Payload>> GetEnumerator() => map.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        void ICollection<KeyValuePair<string, Payload>>.Add(KeyValuePair<string, Payload> item)
        {
            AssertNotReadOnly();
            ((ICollection<KeyValuePair<string, Payload>>)map).Add(item);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            AssertNotReadOnly();
            map.Clear();
        }

        /// <inheritdoc/>
        bool ICollection<KeyValuePair<string, Payload>>.Contains(KeyValuePair<string, Payload> item) =>
            ((ICollection<KeyValuePair<string, Payload>>)map).Contains(item);

        /// <inheritdoc/>
        void ICollection<KeyValuePair<string, Payload>>.CopyTo(KeyValuePair<string, Payload>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<string, Payload>>)map).CopyTo(array, arrayIndex);

        /// <inheritdoc/>
        bool ICollection<KeyValuePair<string, Payload>>.Remove(KeyValuePair<string, Payload> item)
        {
            AssertNotReadOnly();
            return ((ICollection<KeyValuePair<string, Payload>>)map).Remove(item);
        }

        /// <inheritdoc/>
        public override bool Equals(object? other) => Equals(other as SearchAttributeDictionary);

        /// <inheritdoc/>
        public override int GetHashCode() => map.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(SearchAttributeDictionary? other) => other?.map?.Equals(map) ?? false;

        /// <inheritdoc/>
        public override string ToString() => map.ToString();

        /// <inheritdoc/>
        void IDictionary.Add(object key, object? value) =>
            Add((string)key, value as Payload ?? throw new ArgumentNullException());

        /// <inheritdoc/>
        bool IDictionary.Contains(object key) => key is string k && ContainsKey(k);

        /// <inheritdoc/>
        IDictionaryEnumerator IDictionary.GetEnumerator() => ((IDictionary)map).GetEnumerator();

        /// <inheritdoc/>
        void IDictionary.Remove(object key)
        {
            if (key is string k)
            {
                Remove(k);
            }
        }

        /// <inheritdoc/>
        void ICollection.CopyTo(Array array, int index) => ((ICollection)map).CopyTo(array, index);

        private void AssertNotReadOnly()
        {
            if (IsReadOnly)
            {
                throw new NotSupportedException("Search attributes are read only");
            }
        }
    }
}