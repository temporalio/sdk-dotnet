using System.Threading.Tasks;
using Google.Protobuf;

namespace Temporalio.Client
{
    /// <summary>
    /// Raw gRPC test service.
    /// </summary>
    public abstract partial class TestService : RpcService
    {
        internal override Bridge.Interop.RpcService Service => Bridge.Interop.RpcService.Test;

        /// <summary>
        /// Implementation of the test service.
        /// </summary>
        public class Impl : TestService
        {
            private readonly TemporalConnection connection;

            /// <summary>
            /// Create an implementation of the test service.
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
                RpcOptions? options = null
            )
            {
                return await connection.InvokeRpcAsync(this, rpc, req, resp, options);
            }
        }
    }
}
