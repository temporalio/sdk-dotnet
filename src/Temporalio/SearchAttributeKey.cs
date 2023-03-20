#pragma warning disable SA1402 // We allow same-named types in the same file

using System;
using System.Collections.Generic;
using Temporalio.Api.Enums.V1;

namespace Temporalio
{
    /// <summary>
    /// A search attribute key and value type. Use one of the "Create" methods to create.
    /// </summary>
    public class SearchAttributeKey : IComparable<SearchAttributeKey>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SearchAttributeKey"/> class.
        /// </summary>
        /// <param name="name">Key name.</param>
        /// <param name="valueType">Key value type.</param>
        internal SearchAttributeKey(string name, IndexedValueType valueType)
        {
            Name = name;
            ValueType = valueType;
        }

        /// <summary>
        /// Gets the key name.
        /// </summary>
        public string Name { get; private init; }

        /// <summary>
        /// Gets the key value type.
        /// </summary>
        public IndexedValueType ValueType { get; private init; }

        /// <summary>
        /// Test equal.
        /// </summary>
        /// <param name="left">Left.</param>
        /// <param name="right">Right.</param>
        /// <returns>Result.</returns>
        public static bool operator ==(SearchAttributeKey? left, SearchAttributeKey? right) =>
            left is null ? right is null : left.Equals(right!);

        /// <summary>
        /// Test not equal.
        /// </summary>
        /// <param name="left">Left.</param>
        /// <param name="right">Right.</param>
        /// <returns>Result.</returns>
        public static bool operator !=(SearchAttributeKey? left, SearchAttributeKey? right) =>
            !(left == right);

        /// <summary>
        /// Test less than.
        /// </summary>
        /// <param name="left">Left.</param>
        /// <param name="right">Right.</param>
        /// <returns>Result.</returns>
        public static bool operator <(SearchAttributeKey? left, SearchAttributeKey? right) =>
            left is null ? right is not null : left.CompareTo(right) < 0;

        /// <summary>
        /// Test less than or equal.
        /// </summary>
        /// <param name="left">Left.</param>
        /// <param name="right">Right.</param>
        /// <returns>Result.</returns>
        public static bool operator <=(SearchAttributeKey? left, SearchAttributeKey? right) =>
            left is null || left.CompareTo(right) <= 0;

        /// <summary>
        /// Test greater than.
        /// </summary>
        /// <param name="left">Left.</param>
        /// <param name="right">Right.</param>
        /// <returns>Result.</returns>
        public static bool operator >(SearchAttributeKey? left, SearchAttributeKey? right) =>
            left is not null && left.CompareTo(right) > 0;

        /// <summary>
        /// Test greater than or equal.
        /// </summary>
        /// <param name="left">Left.</param>
        /// <param name="right">Right.</param>
        /// <returns>Result.</returns>
        public static bool operator >=(SearchAttributeKey? left, SearchAttributeKey? right) =>
            left is null ? right is null : left.CompareTo(right) >= 0;

        /// <summary>
        /// Create a "text" search attribute key.
        /// </summary>
        /// <param name="name">Key name.</param>
        /// <returns>Search attribute key.</returns>
        public static SearchAttributeKey<string> CreateText(string name) =>
            new(name, IndexedValueType.Text);

        /// <summary>
        /// Create a "keyword" search attribute key.
        /// </summary>
        /// <param name="name">Key name.</param>
        /// <returns>Search attribute key.</returns>
        public static SearchAttributeKey<string> CreateKeyword(string name) =>
            new(name, IndexedValueType.Keyword);

        /// <summary>
        /// Create an "int" search attribute key.
        /// </summary>
        /// <param name="name">Key name.</param>
        /// <returns>Search attribute key.</returns>
        public static SearchAttributeKey<long> CreateLong(string name) =>
            new(name, IndexedValueType.Int);

        /// <summary>
        /// Create a "double" search attribute key.
        /// </summary>
        /// <param name="name">Key name.</param>
        /// <returns>Search attribute key.</returns>
        public static SearchAttributeKey<double> CreateDouble(string name) =>
            new(name, IndexedValueType.Double);

        /// <summary>
        /// Create a "bool" search attribute key.
        /// </summary>
        /// <param name="name">Key name.</param>
        /// <returns>Search attribute key.</returns>
        public static SearchAttributeKey<bool> CreateBool(string name) =>
            new(name, IndexedValueType.Bool);

        /// <summary>
        /// Create a "datetime" search attribute key.
        /// </summary>
        /// <param name="name">Key name.</param>
        /// <returns>Search attribute key.</returns>
        public static SearchAttributeKey<DateTimeOffset> CreateDateTimeOffset(string name) =>
            new(name, IndexedValueType.Datetime);

        /// <summary>
        /// Create a "keyword list" search attribute key.
        /// </summary>
        /// <param name="name">Key name.</param>
        /// <returns>Search attribute key.</returns>
        public static SearchAttributeKey<IReadOnlyCollection<string>> CreateKeywordList(string name) =>
            new(name, IndexedValueType.KeywordList);

        /// <inheritdoc />
        public int CompareTo(SearchAttributeKey? other)
        {
            if (other == null)
            {
                return 1;
            }
            var cmp = Name.CompareTo(other.Name);
            return cmp != 0 ? cmp : ValueType.CompareTo(other.ValueType);
        }

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Name, ValueType);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is SearchAttributeKey sa && Equals(sa);

        /// <summary>
        /// Test for equality.
        /// </summary>
        /// <param name="other">Other key.</param>
        /// <returns>Whether equal.</returns>
        public bool Equals(SearchAttributeKey? other) =>
            other != null && other.Name == Name && other.ValueType == ValueType;
    }

    /// <summary>
    /// Type safe search attribute key.
    /// </summary>
    /// <typeparam name="T">.NET type accepted as value.</typeparam>
    public class SearchAttributeKey<T> : SearchAttributeKey
        where T : notnull
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SearchAttributeKey{T}"/> class.
        /// </summary>
        /// <param name="name">Key name.</param>
        /// <param name="valueType">Key value type.</param>
        internal SearchAttributeKey(string name, IndexedValueType valueType)
            : base(name, valueType)
        {
        }

        /// <summary>
        /// Create a workflow search attribute update to set a value for this key.
        /// </summary>
        /// <param name="value">Value to set.</param>
        /// <returns>Workflow search attribute update.</returns>
        public Workflows.SearchAttributeUpdate<T> ValueSet(T value) =>
            Workflows.SearchAttributeUpdate.ValueSet(this, value);

        /// <summary>
        /// Create a workflow search attribute update to unset this key.
        /// </summary>
        /// <returns>Workflow search attribute update.</returns>
        public Workflows.SearchAttributeUpdate<T> ValueUnset() =>
            Workflows.SearchAttributeUpdate.ValueUnset(this);
    }
}