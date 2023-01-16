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

        /// <summary>
        /// Implementation of the operator service.
        /// </summary>
        public class Impl : OperatorService
        {
            private readonly TemporalConnection connection;

            /// <summary>
            /// Initializes a new instance of the <see cref="Impl"/> class.
            /// </summary>
            /// <param name="connection">Connection to use.</param>
            public Impl(TemporalConnection connection)
            {
                this.connection = connection;
            }

            /// <inheritdoc />
            protected async override Task<T> InvokeRpcAsync<T>(
                string rpc,
                IMessage req,
                MessageParser<T> resp,
                RpcOptions? options = null)
            {
                return await connection.InvokeRpcAsync(this, rpc, req, resp, options);
            }
        }
    }
}
