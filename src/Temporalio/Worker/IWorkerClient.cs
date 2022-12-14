namespace Temporalio.Worker
{
    public interface IWorkerClient
    {
        public string Namespace { get; }

        // TODO(cretz): public Converters.DataConverter DataConverter { get; }

        public Client.IBridgeClientProvider BridgeClientProvider { get; }
    }
}
