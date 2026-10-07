using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Temporalio.Common
{
    /// <summary>
    /// Shared empty <see cref="IReadOnlyDictionary{TKey, TValue}" /> for one key and value type.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    internal static class EmptyReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        /// <summary>
        /// Shared empty dictionary. The <see cref="ReadOnlyDictionary{TKey, TValue}" /> wrapper
        /// rejects mutation, including after a cast from
        /// <see cref="IReadOnlyDictionary{TKey, TValue}" />.
        /// </summary>
        internal static readonly IReadOnlyDictionary<TKey, TValue> Value =
            new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>(0));
    }
}
