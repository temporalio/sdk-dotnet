#pragma warning disable CA1724 // We are ok with service and core names clashing w/ other namespaces

using System.Threading.Tasks;
using Google.Protobuf;

namespace Temporalio.Client
{
    /// <summary>
    /// Raw gRPC test service.
    /// </summary>
    public abstract partial class TestService : RpcService
    {
        /// <inheritdoc/>
        internal override Bridge.Interop.RpcService Service => Bridge.Interop.RpcService.Test;

        /// <inheritdoc/>
        internal override string FullName => "temporal.api.testservice.v1.TestService";

        /// <summary>
        /// Implementation of the test service.
        /// </summary>
        public class Core : TestService
        {
            private readonly TemporalConnection connection;

            /// <summary>
            /// Initializes a new instance of the <see cref="TestService.Core"/> class.
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
