using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Temporalio.Client
{
    /// <summary>
    /// Connection to Temporal.
    /// </summary>
    public sealed class TemporalConnection : ITemporalConnection
    {
        /// <summary>
        /// Connect to Temporal.
        /// </summary>
        /// <param name="options">Options for connecting.</param>
        /// <returns>The established connection.</returns>
        public static async Task<TemporalConnection> ConnectAsync(TemporalConnectionOptions options)
        {
            var runtime = options.Runtime ?? Runtime.Default;
            var client = await Bridge.Client.ConnectAsync(runtime.runtime, options);
            return new TemporalConnection(client);
        }

        private readonly Bridge.Client client;

        private TemporalConnection(Bridge.Client client)
        {
            this.client = client;
            WorkflowService = new WorkflowService.Impl(this);
            OperatorService = new OperatorService.Impl(this);
            TestService = new TestService.Impl(this);
        }

        /// <inheritdoc />
        public WorkflowService WorkflowService { get; private init; }

        /// <inheritdoc />
        public OperatorService OperatorService { get; private init; }

        /// <inheritdoc />
        public TestService TestService { get; private init; }

        /// <summary>
        /// Check health for the given service type.
        /// </summary>
        /// <param name="service">Service type to check health for. Defaults to
        /// <see cref="TemporalConnection.WorkflowService" /></param>
        /// <param name="options">RPC options for the check call.</param>
        /// <returns>True if healthy, false otherwise.</returns>
        public Task<bool> CheckHealthAsync(RpcService? service = null, RpcOptions? options = null)
        {
            throw new NotImplementedException();
        }

        internal async Task<T> InvokeRpcAsync<T>(
            RpcService service,
            string rpc,
            IMessage req,
            MessageParser<T> resp,
            RpcOptions? options = null
        ) where T : IMessage<T>
        {
            return await client.Call(
                service.Service,
                rpc,
                req,
                resp,
                options?.Retry ?? false,
                options?.Metadata,
                options?.Timeout,
                options?.CancellationToken
            );
        }

        /// <inheritdoc />
        public SafeHandle BridgeClient => this.client;
    }
}
