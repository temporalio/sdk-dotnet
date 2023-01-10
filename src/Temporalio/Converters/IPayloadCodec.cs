using System.Collections.Generic;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;

namespace Temporalio
{
    /// <summary>
    /// Payload codec for translating bytes to bytes.
    /// </summary>
    /// <remarks>
    /// This is often useful for encryption and/or compression.
    /// </remarks>
    public interface IPayloadCodec
    {
        /// <summary>
        /// Encode the given collection of payloads.
        /// </summary>
        /// <param name="payloads">Payloads to encode. Do not mutate these.</param>
        /// <returns>
        /// Encoded payloads. This must have at least one value and cannot have more than was given.
        /// </returns>
        Task<IEnumerable<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads);

        /// <summary>
        /// Decode the given collection of payloads.
        /// </summary>
        /// <param name="payloads">Payloads to decode. Do not mutate these.</param>
        /// <returns>
        /// Decoded payloads. This must return the exact same number that was given to
        /// <see cref="EncodeAsync" />.
        /// </returns>
        Task<IEnumerable<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads);
    }
}
