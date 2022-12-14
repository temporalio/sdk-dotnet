using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Temporalio.Client
{
    public class TemporalConnection : IBridgeClientProvider
    {
        public static async Task<TemporalConnection> ConnectAsync(TemporalConnectionOptions options)
        {
            var conn = new TemporalConnection(options, true);
            // Ask for the value to force attempted connect
            if (!options.Lazy)
            {
                await conn.client.Value;
            }
            return conn;
        }

        private readonly Lazy<Task<Bridge.Client>> client;

        public TemporalConnection(TemporalConnectionOptions options) : this(options, false) { }

        private TemporalConnection(TemporalConnectionOptions options, bool allowNonLazy)
        {
            if (!allowNonLazy && !options.Lazy)
            {
                throw new ArgumentException(
                    "Option must have lazy as true if instantiating directly, otherwise use 'Connect'"
                );
            }
            var runtime = options.Runtime ?? Runtime.Default;
            // TODO(cretz): TLS
            if (options.TargetHost == null)
            {
                throw new ArgumentException("TargetHost is required");
            }
            else if (options.TargetHost.Contains("://"))
            {
                throw new ArgumentException("TargetHost cannot have ://");
            }
            var targetUrl = $"http://{options.TargetHost}";

            // Default thread safety of "only one can initialize/publish" is
            // what we want
            client = new Lazy<Task<Bridge.Client>>(
                () =>
                    Task.Factory
                        .StartNew(
                            async () => await Bridge.Client.ConnectAsync(runtime.runtime, options)
                        )
                        .Unwrap()
            );
            WorkflowService = new WorkflowService.Impl(this);
            OperatorService = new OperatorService.Impl(this);
            TestService = new TestService.Impl(this);
        }

        public WorkflowService WorkflowService { get; private init; }
        public OperatorService OperatorService { get; private init; }
        public TestService TestService { get; private init; }

        public Task<bool> CheckHealthAsync(
            RpcService service = RpcService.Workflow,
            RpcOptions? options = null
        )
        {
            throw new NotImplementedException();
        }

        internal protected async Task<T> InvokeRpcAsync<T>(
            RpcService service,
            string rpc,
            IMessage req,
            MessageParser<T> resp,
            RpcOptions? options = null
        ) where T : IMessage<T>
        {
            var client = await this.client.Value;
            return await client.Call(
                (Bridge.Interop.RpcService)service,
                rpc,
                req,
                resp,
                options?.Retry ?? false,
                options?.Metadata,
                options?.Timeout,
                options?.CancellationToken
            );
        }

        public async Task<SafeHandle> ConnectedBridgeClientAsync()
        {
            return await this.client.Value;
        }

        public enum RpcService
        {
            Workflow = 1,
            Operator,
            Test,
            Health,
        }
    }
}
