#pragma warning disable CA1724 // We are ok with service and core names clashing w/ other namespaces

using System.Threading.Tasks;
using Google.Protobuf;

namespace Temporalio.Client
{
    /// <summary>
    /// Raw gRPC operator service.
    /// </summary>
    public abstract partial class OperatorService : RpcService
    {
        /// <inheritdoc/>
        internal override Bridge.Interop.RpcService Service => Bridge.Interop.RpcService.Operator;

        /// <inheritdoc/>
        internal override string FullName => "temporal.api.operatorservice.v1.OperatorService";

        /// <summary>
        /// Implementation of the operator service.
        /// </summary>
        public class Core : OperatorService
        {
            private readonly TemporalConnection connection;

            /// <summary>
            /// Initializes a new instance of the <see cref="Temporalio.Client.OperatorService.Core"/> class.
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
