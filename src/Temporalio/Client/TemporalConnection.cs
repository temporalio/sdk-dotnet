using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Google.Protobuf;
using Temporalio.Runtime;

namespace Temporalio.Client
{
    /// <summary>
    /// Connection to Temporal.
    /// </summary>
    public sealed class TemporalConnection : ITemporalConnection
    {
        private readonly Bridge.Client client;
        private readonly object rpcMetadataLock = new();
        private IReadOnlyCollection<KeyValuePair<string, string>> rpcMetadata;

        private TemporalConnection(Bridge.Client client, TemporalConnectionOptions options)
        {
            this.client = client;
            WorkflowService = new WorkflowService.Core(this);
            OperatorService = new OperatorService.Core(this);
            TestService = new TestService.Core(this);
            Options = options;
            if (options.RpcMetadata == null)
            {
                rpcMetadata = Array.Empty<KeyValuePair<string, string>>();
            }
            else
            {
                rpcMetadata = new List<KeyValuePair<string, string>>(options.RpcMetadata);
            }
        }

        /// <inheritdoc />
        public IReadOnlyCollection<KeyValuePair<string, string>> RpcMetadata
        {
            get
            {
                lock (rpcMetadataLock)
                {
                    return rpcMetadata;
                }
            }

            set
            {
                lock (rpcMetadata)
                {
                    // Set on Rust side first to prevent errors from affecting field
                    client.UpdateMetadata(value);
                    // We copy this every time just to be safe
                    rpcMetadata = new List<KeyValuePair<string, string>>(value);
                }
            }
        }

        /// <inheritdoc />
        public WorkflowService WorkflowService { get; private init; }

        /// <inheritdoc />
        public OperatorService OperatorService { get; private init; }

        /// <inheritdoc />
        public TestService TestService { get; private init; }

        /// <inheritdoc />
        public TemporalConnectionOptions Options { get; private init; }

        /// <inheritdoc />
        public SafeHandle BridgeClient => client;

        /// <summary>
        /// Connect to Temporal.
        /// </summary>
        /// <param name="options">Options for connecting.</param>
        /// <returns>The established connection.</returns>
        public static async Task<TemporalConnection> ConnectAsync(TemporalConnectionOptions options)
        {
            // Set default identity if unset
            options.Identity ??= System.Diagnostics.Process.GetCurrentProcess().Id
                            + "@"
                            + System.Net.Dns.GetHostName();
            var runtime = options.Runtime ?? TemporalRuntime.Default;
            var client = await Bridge.Client.ConnectAsync(
                runtime.Runtime, options).ConfigureAwait(false);
            return new TemporalConnection(client, options);
        }

        /// <inheritdoc />
        public Task<bool> CheckHealthAsync(
            RpcService? service = null, RpcOptions? options = null) =>
            throw new NotImplementedException();

        /// <summary>
        /// Invoke RPC call on this connection.
        /// </summary>
        /// <typeparam name="T">Proto response type.</typeparam>
        /// <param name="service">RPC service to call.</param>
        /// <param name="rpc">RPC operation.</param>
        /// <param name="req">Request proto.</param>
        /// <param name="resp">Response proto parser.</param>
        /// <param name="options">RPC options.</param>
        /// <returns>Response proto.</returns>
        internal Task<T> InvokeRpcAsync<T>(
            RpcService service,
            string rpc,
            IMessage req,
            MessageParser<T> resp,
            RpcOptions? options = null)
            where T : IMessage<T> =>
            client.CallAsync(
                service.Service,
                rpc,
                req,
                resp,
                options?.Retry ?? false,
                options?.Metadata,
                options?.Timeout,
                options?.CancellationToken);
    }
}
