using System;
using System.Linq;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    /// <summary>
    /// Client for a Temporal namespace.
    /// </summary>
    /// <remarks>
    /// Clients are thread-safe and are encouraged to be reused to properly reuse the underlying
    /// connection.
    /// </remarks>
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
            if (options.Interceptors != null)
            {
                OutboundInterceptor = options.Interceptors.Reverse().Aggregate(
                    OutboundInterceptor, (v, impl) => impl.InterceptClient(v));
            }
        }

        /// <inheritdoc />
        public IBridgeClientProvider BridgeClientProvider => Connection;

        /// <inheritdoc />
        public ITemporalConnection Connection { get; private init; }

        /// <inheritdoc />
        public WorkflowService WorkflowService => Connection.WorkflowService;

        /// <inheritdoc />
        public OperatorService OperatorService => Connection.OperatorService;

        /// <inheritdoc />
        public TemporalClientOptions Options { get; private init; }

        /// <inheritdoc />
        public Interceptors.ClientOutboundInterceptor OutboundInterceptor { get; private init; }

        /// <summary>
        /// Gets a fixed set of retry-only RPC options.
        /// </summary>
        protected internal static RpcOptions RetryRpcOptions { get; } = new RpcOptions() { Retry = true };

        /// <summary>
        /// Connect to a Temporal namespace.
        /// </summary>
        /// <param name="options">Options for connecting.</param>
        /// <returns>The connected client.</returns>
        public static async Task<TemporalClient> ConnectAsync(
            TemporalClientConnectOptions options) =>
            new(
                await TemporalConnection.ConnectAsync(options).ConfigureAwait(false),
                options.ToClientOptions());

        /// <summary>
        /// Create a client to a Temporal namespace that does not connect until first call.
        /// Unconnected lazy clients cannot be used by workers. If an initial client connection
        /// fails, it will be retried next time it is needed.
        /// </summary>
        /// <param name="options">Options for connecting.</param>
        /// <returns>The not-yet-connected client.</returns>
        public static TemporalClient CreateLazy(TemporalClientConnectOptions options) =>
            new(TemporalConnection.CreateLazy(options), options.ToClientOptions());

        /// <summary>
        /// Get a default set of retry options given the optional options. This will not mutate the
        /// given options. This only sets retry if original options are not present or they have not
        /// already set a retry.
        /// </summary>
        /// <param name="origOptions">Original options to use as a base for the return.</param>
        /// <returns>Options with default retry set.</returns>
        protected internal static RpcOptions DefaultRetryOptions(RpcOptions? origOptions)
        {
            // Override retry if there are options but that is unset
            if (origOptions == null)
            {
                return RetryRpcOptions;
            }
            else if (origOptions.Retry != null)
            {
                return origOptions;
            }
            var newOptions = (RpcOptions)origOptions.Clone();
            newOptions.Retry = true;
            return newOptions;
        }

        internal partial class Impl : Interceptors.ClientOutboundInterceptor
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Impl"/> class.
            /// </summary>
            /// <param name="client">Client to use.</param>
            public Impl(TemporalClient client) => Client = client;

            /// <summary>
            /// Gets the client.
            /// </summary>
            internal TemporalClient Client { get; init; }
        }
    }
}
