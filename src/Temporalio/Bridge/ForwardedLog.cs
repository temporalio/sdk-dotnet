using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Representation of log state for a Core log.
    /// </summary>
    /// <param name="Level">Log level.</param>
    /// <param name="Target">Log target.</param>
    /// <param name="Message">Log message.</param>
    /// <param name="TimestampMilliseconds">Ms since Unix epoch.</param>
    /// <param name="JsonFields">JSON fields, or null to not include. The keys are the field names
    /// and the values are raw JSON strings.</param>
    internal record ForwardedLog(
        LogLevel Level,
        string Target,
        string Message,
        ulong TimestampMilliseconds,
        IReadOnlyDictionary<string, string>? JsonFields) : IReadOnlyList<KeyValuePair<string, object?>>
    {
        // Unfortunately DateTime.UnixEpoch not in standard library in all versions we need
        private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Gets the timestamp for this log.
        /// </summary>
        public DateTime Timestamp => UnixEpoch.AddMilliseconds(TimestampMilliseconds);

        /// <inheritdoc />
        public int Count => 5;

        /// <inheritdoc />
        public KeyValuePair<string, object?> this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return new("Level", Level);
                    case 1:
                        return new("Target", Target);
                    case 2:
                        return new("Message", Message);
                    case 3:
                        return new("Timestamp", Timestamp);
                    case 4:
                        return new("JsonFields", JsonFields);
                    default:
#pragma warning disable CA2201 // We intentionally use this usually-internal-use-only exception
                        throw new IndexOutOfRangeException(nameof(index));
#pragma warning restore CA2201
                }
            }
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (int i = 0; i < Count; ++i)
            {
                yield return this[i];
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var message = $"[sdk_core::{Target}] {Message}";
            if (JsonFields is { } jsonFields)
            {
                message += " " + string.Join(", ", jsonFields.Select(kv => $"{kv.Key}={kv.Value}"));
            }
            return message;
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}