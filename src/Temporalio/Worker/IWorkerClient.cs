namespace Temporalio.Worker
{
    /// <summary>
    /// Client that can be used to create a worker.
    /// </summary>
    public interface IWorkerClient
    {
        /// <summary>
        /// Gets the namespace for this client.
        /// </summary>
        public string Namespace { get; }

        // TODO(cretz): public Converters.DataConverter DataConverter { get; }

        /// <summary>
        /// Gets the bridge client provider for this client.
        /// </summary>
        public Client.IBridgeClientProvider BridgeClientProvider { get; }
    }
}
