using System.Threading.Tasks;
using System.Linq;

namespace Temporalio.Client
{
    /// <summary>
    /// Client for a Temporal namespace.
    /// </summary>
    public partial class TemporalClient : ITemporalClient
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalClient"/> class from an existing
        /// connection.
        /// </summary>
        /// <param name="connection">Connection for this client.</param>
        /// <param name="options">Options for this client.</param>
        public TemporalClient(ITemporalConnection connection, TemporalClientOptions options)
        {
            Connection = connection;
            Options = options;
            OutboundInterceptor = new Impl(this);
            // Build from interceptors in reverse
            if (options.Interceptors != null) {
                OutboundInterceptor = options.Interceptors.Reverse().Aggregate(
                    OutboundInterceptor, (v, impl) => impl.InterceptClient(v));
            }
        }

        /// <inheritdoc />
        public string Namespace => Options.Namespace;

        /// <inheritdoc />
        public Converters.DataConverter DataConverter => Options.DataConverter;

        /// <inheritdoc />
        public IBridgeClientProvider BridgeClientProvider => Connection;

        /// <inheritdoc />
        public ITemporalConnection Connection { get; private init; }

        /// <inheritdoc />
        public TemporalClientOptions Options { get; private init; }

        public Interceptors.ClientOutboundInterceptor OutboundInterceptor { get; private init; }

        protected static RpcOptions RetryRpcOptions { get; } = new RpcOptions() { Retry = true };

        /// <summary>
        /// Connect to a Temporal namespace.
        /// </summary>
        /// <param name="options">Options for connecting.</param>
        /// <returns>The connected client.</returns>
        public static async Task<TemporalClient> ConnectAsync(TemporalClientConnectOptions options)
        {
            return new TemporalClient(
                await TemporalConnection.ConnectAsync(options),
                options.ToClientOptions());
        }

        protected internal static RpcOptions DefaultRetryOptions(RpcOptions? origOptions) {
            // Override retry if there are options but that is unset
            if (origOptions == null) {
                return RetryRpcOptions;
            } else if (origOptions.Retry != null) {
                return origOptions;
            }
            var newOptions = (RpcOptions) origOptions.Clone();
            newOptions.Retry = true;
            return newOptions;
        }

        internal partial class Impl : Interceptors.ClientOutboundInterceptor {

            public Impl(TemporalClient client) {
                Client = client;
            }

            internal TemporalClient Client { get; init; }

        }
    }
}
