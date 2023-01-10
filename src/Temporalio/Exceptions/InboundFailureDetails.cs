using System.Collections.Generic;
using System.Linq;
using Temporalio.Api.Common.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Implementation of <see cref="IFailureDetails" /> for not-yet-converted payloads that will be
    /// converted lazily.
    /// </summary>
    /// <param name="Converter">Converter to use for conversion.</param>
    /// <param name="Payloads">Raw payloads to convert.</param>
    public record InboundFailureDetails(
        Converters.IPayloadConverter Converter,
        IReadOnlyCollection<Payload> Payloads
    ) : IFailureDetails
    {
        /// <inheritdoc />
        public int Count => Payloads.Count;

        /// <inheritdoc />
        public T? ElementAt<T>(int index)
        {
            return Converter.ToValue<T>(Payloads.ElementAt(index));
        }
    }
}
