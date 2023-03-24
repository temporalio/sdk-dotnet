#pragma warning disable CA1724 // We are ok with service and core names clashing w/ other namespaces

using System.Threading.Tasks;
using Google.Protobuf;

namespace Temporalio.Client
{
    /// <summary>
    /// Raw gRPC workflow service.
    /// </summary>
    public abstract partial class WorkflowService : RpcService
    {
        /// <inheritdoc/>
        internal override Bridge.Interop.RpcService Service => Bridge.Interop.RpcService.Workflow;

        /// <summary>
        /// Implementation of the workflow service.
        /// </summary>
        public class Core : WorkflowService
        {
            private readonly TemporalConnection connection;

            /// <summary>
            /// Initializes a new instance of the <see cref="Core"/> class.
            /// </summary>
            /// <param name="connection">Connection to use.</param>
            public Core(TemporalConnection connection) => this.connection = connection;

            /// <inheritdoc />
            protected override Task<T> InvokeRpcAsync<T>(
                string rpc, IMessage req, MessageParser<T> resp, RpcOptions? options = null) =>
                connection.InvokeRpcAsync(this, rpc, req, resp, options);
        }
    }
}
