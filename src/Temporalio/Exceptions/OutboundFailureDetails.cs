using System.Collections.Generic;
using System.Linq;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Implementation of <see cref="IFailureDetails" /> for language values that are converted
    /// later.
    /// </summary>
    /// <param name="Details">Collection of details to reference.</param>
    public record OutboundFailureDetails(IReadOnlyCollection<object> Details) : IFailureDetails
    {
        /// <inheritdoc />
        public int Count => Details.Count;

        /// <inheritdoc />
        public T? ElementAt<T>(int index)
        {
            return (T)Details.ElementAt(index);
        }
    }
}
