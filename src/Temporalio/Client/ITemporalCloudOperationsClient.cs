namespace Temporalio.Client
{
    /// <summary>
    /// Interface to a client to Temporal Cloud Operations API.
    /// </summary>
    /// <remarks>
    /// Clients are thread-safe and are encouraged to be reused to properly reuse the underlying
    /// connection.
    /// </remarks>
    /// <seealso cref="TemporalCloudOperationsClient" />
    /// <remarks>
    /// WARNING: Cloud Operations API and its client are experimental and APIs may change.
    /// </remarks>
    public interface ITemporalCloudOperationsClient
    {
        /// <summary>
        /// Gets the connection associated with this client.
        /// </summary>
        ITemporalConnection Connection { get; }

        /// <summary>
        /// Gets the raw gRPC cloud service.
        /// </summary>
        CloudService CloudService { get; }
    }
}