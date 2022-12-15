namespace Temporalio.Client
{
    /// <summary>
    /// Interface to a client to Temporal.
    /// </summary>
    /// <seealso cref="TemporalClient" />
    public interface ITemporalClient : Worker.IWorkerClient
    {
        /// <summary>
        /// Gets the connection associated with this client.
        /// </summary>
        ITemporalConnection Connection { get; }
    }
}
