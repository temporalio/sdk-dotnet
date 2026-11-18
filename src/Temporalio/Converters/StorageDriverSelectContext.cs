namespace Temporalio.Converters
{
    /// <summary>
    /// Context given to a <see cref="StorageDriverSelector" />.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    public sealed class StorageDriverSelectContext
    {
        /// <summary>
        /// Gets the execution the payload is being stored on behalf of, or null if there is no
        /// associated execution.
        /// </summary>
        public IStorageDriverTargetInfo? Target { get; init; }

        /// <summary>
        /// Gets the namespace of the client or worker performing the store, or null when it is not
        /// known.
        /// </summary>
        public string? Namespace { get; init; }
    }
}
