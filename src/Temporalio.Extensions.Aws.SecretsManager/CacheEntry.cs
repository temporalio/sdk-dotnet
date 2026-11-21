using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// One or more secrets that are fetched and replaced together, cached as a single parsed
    /// value.
    /// </summary>
    /// <typeparam name="T">Parsed value type.</typeparam>
    internal sealed class CacheEntry<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CacheEntry{T}"/> class.
        /// </summary>
        /// <param name="refs">Distinct secret IDs. The first one's secret type is used as the
        /// secret_type metric label.</param>
        /// <param name="parse">Parses newly fetched secrets, in refs order, into the cached value.
        /// Throws <see cref="InvalidResponseException" /> if they're invalid.</param>
        public CacheEntry(IReadOnlyList<SecretRef> refs, Func<IReadOnlyList<SecretValue>, T> parse)
        {
            Refs = refs;
            Parse = parse;
        }

        /// <summary>
        /// Gets the secrets, fetched in this order.
        /// </summary>
        public IReadOnlyList<SecretRef> Refs { get; }

        /// <summary>
        /// Gets the parser for newly fetched secrets.
        /// </summary>
        public Func<IReadOnlyList<SecretValue>, T> Parse { get; }

        /// <summary>
        /// Gets the lock that guards the properties below.
        /// </summary>
        public object Lock { get; } = new();

        /// <summary>
        /// Gets or sets the fetch in progress, or null if none.
        /// </summary>
        public Task<T>? InFlight { get; set; }

        /// <summary>
        /// Gets or sets the version IDs in refs order, or null until the first successful fetch.
        /// </summary>
        public IReadOnlyList<string>? VersionIds { get; set; }

        /// <summary>
        /// Gets or sets the parsed value, or default until the first successful fetch.
        /// </summary>
        public T? Value { get; set; }

        /// <summary>
        /// Gets or sets when the last successful fetch started.
        /// </summary>
        public TimeSpan FetchedAt { get; set; }

        /// <summary>
        /// Gets or sets the last failed fetch since the last successful fetch, or null if none.
        /// </summary>
        public TimeSpan? FailedAt { get; set; }
    }
}
