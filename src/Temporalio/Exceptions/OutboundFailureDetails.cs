using System;
using System.Collections.Generic;
using System.Linq;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Implementation of <see cref="IFailureDetails" /> for language values that are converted
    /// later.
    /// </summary>
    /// <param name="Details">Collection of details to reference.</param>
    public record OutboundFailureDetails(IReadOnlyCollection<object?>? Details) : IFailureDetails
    {
        /// <inheritdoc />
        public int Count => Details?.Count ?? 0;

        /// <inheritdoc />
        public T ElementAt<T>(int index)
        {
            // Have to check ourselves here just in case no collection present
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
#else
            if (index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
#endif
            return (T)Details?.ElementAt(index)!;
        }
    }
}
