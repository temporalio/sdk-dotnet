using System.Threading.Tasks;

namespace Temporalio.Client
{
    public class TemporalClient : ITemporalClient
    {
        public static async Task<TemporalClient> ConnectAsync(TemporalClientConnectOptions options)
        {
            return new TemporalClient(
                await TemporalConnection.ConnectAsync(options),
                options.AsClientOptions()
            );
        }

        public TemporalClient(TemporalConnection connection, TemporalClientOptions options)
        {
            Connection = connection;
            Options = options;
        }

        public string Namespace => Options.Namespace;

        // TODO(cretz): public Converters.DataConverter DataConverter => Options.DataConverter;
        public IBridgeClientProvider BridgeClientProvider => Connection;
        public TemporalConnection Connection { get; private init; }
        public TemporalClientOptions Options { get; private init; }

        // TODO(cretz): High-level client methods
    }
}
