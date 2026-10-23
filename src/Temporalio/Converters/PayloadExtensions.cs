using Google.Protobuf;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Extension methods for <see cref="Payload" />.
    /// </summary>
    internal static class PayloadExtensions
    {
        /// <summary>
        /// Make this payload equal to another, keeping this instance for callers that hold it.
        /// </summary>
        /// <param name="payload">Payload to overwrite.</param>
        /// <param name="replacement">Payload to copy from.</param>
        /// <remarks>
        /// Every field must be cleared here because a merge appends to repeated and map fields
        /// rather than replacing them. A test fails when <see cref="Payload" /> gains a field.
        /// </remarks>
        public static void ReplaceWith(this Payload payload, Payload replacement)
        {
            payload.Metadata.Clear();
            payload.Data = ByteString.Empty;
            payload.ExternalPayloads.Clear();
            payload.MergeFrom(replacement);
        }
    }
}
