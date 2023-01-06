using System.Threading.Tasks;

namespace Temporalio.Client
{
    /// <summary>
    /// Client for a Temporal namespace.
    /// </summary>
    public class TemporalClient : ITemporalClient
    {
        /// <summary>
        /// Connect to a Temporal namespace.
        /// </summary>
        /// <param name="options">Options for connecting.</param>
        /// <returns>The connected client.</returns>
        public static async Task<TemporalClient> ConnectAsync(TemporalClientConnectOptions options)
        {
            return new TemporalClient(
                await TemporalConnection.ConnectAsync(options),
                options.ToClientOptions()
            );
        }

        /// <summary>
        /// Create a client from an existing connection.
        /// </summary>
        /// <param name="connection">Connection for this client.</param>
        /// <param name="options">Options for this client.</param>
        public TemporalClient(ITemporalConnection connection, TemporalClientOptions options)
        {
            Connection = connection;
            Options = options;
        }

        /// <inheritdoc />
        public string Namespace => Options.Namespace;

        // TODO(cretz): public Converters.DataConverter DataConverter => Options.DataConverter;

        /// <inheritdoc />
        public IBridgeClientProvider BridgeClientProvider => Connection;

        /// <inheritdoc />
        public ITemporalConnection Connection { get; private init; }

        /// <summary>
        /// Gets the options originally passed to this client.
        /// </summary>
        public TemporalClientOptions Options { get; private init; }

        // TODO(cretz): High-level client methods
    }
}
