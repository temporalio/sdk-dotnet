using System;
using System.Collections.Generic;
using System.Linq;

namespace Temporalio.Converters
{
    /// <summary>
    /// Driver-specific data identifying a single payload in an external storage system.
    /// </summary>
    /// <param name="ClaimData">
    /// Key/value pairs the driver needs to retrieve the payload later. This is written into history,
    /// so it must contain everything required for retrieval and must not contain secrets.
    /// </param>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal sealed record StorageDriverClaim(IReadOnlyDictionary<string, string> ClaimData)
    {
        /// <summary>
        /// Whether this claim identifies the same stored payload as another.
        /// </summary>
        /// <param name="other">Claim to compare with.</param>
        /// <returns>True if both hold the same claim data.</returns>
        /// <remarks>
        /// Compared by content. The generated record equality would compare the dictionary by
        /// reference, so two claims built from equal but separate dictionaries would not be equal.
        /// </remarks>
        public bool Equals(StorageDriverClaim? other) =>
            other is not null &&
            ClaimData.Count == other.ClaimData.Count &&
            ClaimData.All(pair =>
                other.ClaimData.TryGetValue(pair.Key, out var value) && value == pair.Value);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = default(HashCode);
            // The sort is required, not cosmetic: HashCode.Add is order-dependent while Equals is
            // not, and two claims holding the same entries can enumerate them in different orders
            // depending on how each dictionary was built.
            foreach (var pair in ClaimData.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                hash.Add(pair.Key);
                hash.Add(pair.Value);
            }
            return hash.ToHashCode();
        }
    }
}
