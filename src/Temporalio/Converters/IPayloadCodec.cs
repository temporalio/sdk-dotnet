using System.Collections.Generic;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Payload codec for translating bytes to bytes.
    /// </summary>
    /// <remarks>
    /// This is often useful for encryption and/or compression.
    /// </remarks>
    /// <remarks>
    /// Implementations must never mutate the payloads they are given and must never retain
    /// references to them after the call returns. The payloads are live objects that the SDK may
    /// still use or overwrite afterwards, so changes made in place can appear on unrelated
    /// payloads later. A payload that is actually altered must therefore be returned as a newly
    /// created instance. Usually the cleanest way to create it is to serialize the entire given
    /// payload into the data of the new payload on encode and do the inverse on decode. Cloning
    /// the given payload works too, but a clone also carries over the previous payload's metadata
    /// which is rarely wanted.
    /// </remarks>
    /// <remarks>
    /// A codec may decline to alter a payload, say a compression codec that leaves already-small
    /// payloads alone. To do so, return the given payload instance unchanged and the SDK will
    /// leave it as it is. This is decided per payload, so some payloads in a collection can be
    /// passed through while others are replaced. Codecs that do this have to be able to tell on
    /// decode which payloads they encoded, usually from metadata set on the payloads they created.
    /// </remarks>
    /// <remarks>
    /// Implementations must also be stateless with regards to individual payloads, because the same
    /// logical payload can pass through a codec more than once. For example, failure details are
    /// decoded when an activity failure reaches a workflow and then encoded again when that failure
    /// propagates out of the workflow, and any payload can be decoded repeatedly during workflow
    /// replay or when reading history on the client.
    /// </remarks>
    /// <remarks>
    /// Implementations of this interface can also implement
    /// <see cref="IWithSerializationContext{TResult}"/> for <c>IPayloadCodec</c> to customize the
    /// converter based on context.
    /// </remarks>
    public interface IPayloadCodec
    {
        /// <summary>
        /// Encode the given collection of payloads.
        /// </summary>
        /// <param name="payloads">
        /// Payloads to encode. Do not mutate these or retain references to them after this call
        /// returns.
        /// </param>
        /// <returns>
        /// Encoded payloads. Every altered payload must be a newly created instance, though
        /// payloads left alone can be returned as they were given. This must have at least one
        /// value and cannot have more than was given.
        /// </returns>
        Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads);

        /// <summary>
        /// Decode the given collection of payloads.
        /// </summary>
        /// <param name="payloads">
        /// Payloads to decode. Do not mutate these or retain references to them after this call
        /// returns.
        /// </param>
        /// <returns>
        /// Decoded payloads. Every altered payload must be a newly created instance, though
        /// payloads left alone can be returned as they were given. This must return the exact same
        /// number that was given to <see cref="EncodeAsync" />.
        /// </returns>
        Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads);
    }
}
