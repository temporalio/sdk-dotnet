using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Representation of a raw value that is already decoded but not converted.
    /// </summary>
    public interface IRawValue
    {
        /// <summary>
        /// Gets the raw payload value.
        /// </summary>
        Payload Payload { get; }
    }
}