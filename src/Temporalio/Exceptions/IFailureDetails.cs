namespace Temporalio.Exceptions
{
    /// <summary>
    /// Represents details of a failure.
    /// </summary>
    /// <remarks>
    /// This is abstracted as an interface to separate converted, which uses
    /// <see cref="InboundFailureDetails" />, from not-yet-converted which uses
    /// <see cref="OutboundFailureDetails" />.
    /// </remarks>
    public interface IFailureDetails
    {
        /// <summary>
        /// Gets the number of details. May be 0.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Get a detail value at the given index, converting to the given type if needed.
        /// </summary>
        /// <typeparam name="T">
        /// The detail type to get. For outbound details this is a cast, for inbound this is a
        /// conversion.
        /// </typeparam>
        /// <param name="index">Index of the detail item.</param>
        /// <returns>Item at this index for the given type.</returns>
        /// <remarks>
        /// This will fail if the index is out of range or the type is invalid for the detail item
        /// within.
        /// </remarks>
        T ElementAt<T>(int index);
    }
}
