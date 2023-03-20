using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// A single mutation to a memo key. This can be a set or, if <see cref="HasValue" /> is false,
    /// an unset.
    /// </summary>
    public class MemoUpdate
    {
        private readonly object? value;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoUpdate"/> class. This is an unset.
        /// </summary>
        /// <param name="key">Key to unset.</param>
        internal MemoUpdate(string key) => UntypedKey = key;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoUpdate"/> class. This is a set.
        /// </summary>
        /// <param name="key">Key to set.</param>
        /// <param name="value">Value to set.</param>
        internal MemoUpdate(string key, object value)
        {
            UntypedKey = key;
            this.value = value;
            HasValue = true;
        }

        /// <summary>
        /// Gets a value indicating whether this update has a value to set or represents an unset.
        /// </summary>
        public bool HasValue { get; private init; }

        /// <summary>
        /// Gets the string key to update.
        /// </summary>
        public string UntypedKey { get; private init; }

        /// <summary>
        /// Gets the value to update or fail if there is not a value (i.e. this is an unset).
        /// </summary>
        /// <exception cref="InvalidOperationException">If there is no value.</exception>
        public object UntypedValue =>
            HasValue ? value! : throw new InvalidOperationException("No value");

        /// <summary>
        /// Create an update to set a key.
        /// </summary>
        /// <param name="key">Key to set.</param>
        /// <param name="value">Value to set. Must not be null.</param>
        /// <returns>Memo update.</returns>
        /// <exception cref="ArgumentException">If the value is null.</exception>
        public static MemoUpdate ValueSet(string key, object value)
        {
            if (value == null)
            {
                throw new ArgumentException("Value cannot be null", nameof(value));
            }
            return new(key, value);
        }

        /// <summary>
        /// Create an update to unset a key.
        /// </summary>
        /// <param name="key">Key to unset.</param>
        /// <returns>Memo update.</returns>
        public static MemoUpdate ValueUnset(string key) => new(key);
    }
}