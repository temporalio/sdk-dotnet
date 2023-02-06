using System;
using System.Collections.Generic;
using System.Linq;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Implementation of <see cref="IFailureDetails" /> for not-yet-converted payloads that will be
    /// converted lazily.
    /// </summary>
    /// <param name="Converter">Converter to use for conversion.</param>
    /// <param name="Payloads">Raw payloads to convert.</param>
    public record InboundFailureDetails(
        IPayloadConverter Converter,
        IReadOnlyCollection<Payload>? Payloads) : IFailureDetails
    {
        /// <inheritdoc />
        public int Count => Payloads?.Count ?? 0;

        /// <inheritdoc />
        public T? ElementAt<T>(int index)
        {
            // Have to check ourselves here just in case no collection present
            if (index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return Converter.ToValue<T>(Payloads.ElementAt(index));
        }
    }
}
