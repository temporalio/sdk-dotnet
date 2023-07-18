#pragma warning disable VSTHRD003 // We await a task we created in constructor
#pragma warning disable CA1001 // We are disposing in destructor by intention

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Temporalio.Bridge.Api.Grpc.Health.V1;
using Temporalio.Runtime;

namespace Temporalio.Client
{
    /// <summary>
    /// Connection to Temporal.
    /// </summary>
    public sealed class TemporalConnection : ITemporalConnection
    {
        // Not set if not lazy
        private readonly SemaphoreSlim? semaphoreForLazyClient;
        private readonly object rpcMetadataLock = new();
        private Bridge.Client? client;
        private IReadOnlyCollection<KeyValuePair<string, string>> rpcMetadata;

        private TemporalConnection(TemporalConnectionOptions options, bool lazy)
        {
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
            // Set default identity if unset
            options.Identity ??= System.Diagnostics.Process.GetCurrentProcess().Id
                            + "@"
                            + System.Net.Dns.GetHostName();
            // Only set semaphore if lazy
            if (lazy)
            {
                semaphoreForLazyClient = new(1, 1);
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="TemporalConnection" /> class.
        /// </summary>
        ~TemporalConnection()
        {
            client?.Dispose();
            semaphoreForLazyClient?.Dispose();
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
                var client = this.client;
                if (client == null)
                {
                    throw new InvalidOperationException("Cannot set RPC metadata if client never connected");
                }
                lock (rpcMetadata)
                {
                    // Set on Rust side first to prevent errors from affecting field
#pragma warning disable VSTHRD002 // We know it's completed
                    client.UpdateMetadata(value);
#pragma warning restore VSTHRD002
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
        public bool IsConnected => client != null;

        /// <inheritdoc />
        public SafeHandle? BridgeClient => client;

        /// <summary>
        /// Connect to Temporal.
        /// </summary>
        /// <param name="options">Options for connecting.</param>
        /// <returns>The established connection.</returns>
        public static async Task<TemporalConnection> ConnectAsync(TemporalConnectionOptions options)
        {
            var conn = new TemporalConnection(options, lazy: false);
            await conn.GetBridgeClientAsync().ConfigureAwait(false);
            return conn;
        }

        /// <summary>
        /// Create a client that will connect to Temporal lazily upon first use. If an initial
        /// connection fails, it will be retried next time it is needed. Unconnected clients made
        /// from lazy connections cannot be used by workers. Note, <see cref="RpcMetadata" /> cannot
        /// be set until a connection is made.
        /// </summary>
        /// <param name="options">Options for connecting.</param>
        /// <returns>The not-yet-connected connection.</returns>
        public static TemporalConnection CreateLazy(TemporalConnectionOptions options) =>
            new(options, lazy: true);

        /// <inheritdoc />
        public async Task<bool> CheckHealthAsync(RpcService? service = null, RpcOptions? options = null)
        {
            var client = await GetBridgeClientAsync().ConfigureAwait(false);
            var serviceName = service?.FullName ?? "temporal.api.workflowservice.v1.WorkflowService";
            var resp = await client.CallAsync(
                Bridge.Interop.RpcService.Health,
                "Check",
                new HealthCheckRequest() { Service = serviceName },
                HealthCheckResponse.Parser,
                options?.Retry ?? false,
                options?.Metadata,
                options?.Timeout,
                options?.CancellationToken).ConfigureAwait(false);
            return resp.Status == HealthCheckResponse.Types.ServingStatus.Serving;
        }

        /// <inheritdoc />
        public Task ConnectAsync() => GetBridgeClientAsync();

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
        internal async Task<T> InvokeRpcAsync<T>(
            RpcService service,
            string rpc,
            IMessage req,
            MessageParser<T> resp,
            RpcOptions? options = null)
            where T : IMessage<T>
        {
            var client = await GetBridgeClientAsync().ConfigureAwait(false);
            return await client.CallAsync(
                service.Service,
                rpc,
                req,
                resp,
                options?.Retry ?? false,
                options?.Metadata,
                options?.Timeout,
                options?.CancellationToken).ConfigureAwait(false);
        }

        private async Task<Bridge.Client> GetBridgeClientAsync()
        {
            // Return client if already not-null (without lock)
            if (client is not null)
            {
                return client;
            }
            // Attempt connect under semaphore if present
            if (semaphoreForLazyClient is not null)
            {
                await semaphoreForLazyClient.WaitAsync().ConfigureAwait(false);
            }
            try
            {
                // Return client if already not-null (with lock)
#pragma warning disable CA1508 // False positive in concurrent situation
                if (client != null)
                {
                    return client;
                }
#pragma warning restore CA1508
                var runtime = Options.Runtime ?? TemporalRuntime.Default;
                client = await Bridge.Client.ConnectAsync(runtime.Runtime, Options).ConfigureAwait(false);
                return client;
            }
            finally
            {
                semaphoreForLazyClient?.Release();
            }
        }
    }
}
