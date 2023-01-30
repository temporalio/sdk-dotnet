namespace Temporalio.Worker
{
    /// <summary>
    /// Client that can be used to create a worker.
    /// </summary>
    public interface IWorkerClient
    {
        /// <summary>
        /// Gets the options used to create this client.
        /// </summary>
        Client.TemporalClientOptions Options { get; }

        /// <summary>
        /// Gets the bridge client provider for this client.
        /// </summary>
        public Client.IBridgeClientProvider BridgeClientProvider { get; }
    }
}
