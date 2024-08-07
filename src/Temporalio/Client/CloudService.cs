#pragma warning disable CA1724 // We are ok with service and core names clashing w/ other namespaces

using System.Threading.Tasks;
using Google.Protobuf;

namespace Temporalio.Client
{
    /// <summary>
    /// Raw gRPC cloud service.
    /// </summary>
    /// <remarks>
    /// WARNING: Cloud Operations API and its client are experimental and APIs may change.
    /// </remarks>
    public abstract partial class CloudService : RpcService
    {
        /// <inheritdoc/>
        internal override Bridge.Interop.RpcService Service => Bridge.Interop.RpcService.Cloud;

        /// <inheritdoc/>
        internal override string FullName => "temporal.api.cloud.cloudservice.v1.CloudService";

        /// <summary>
        /// Implementation of the cloud service.
        /// </summary>
        public class Core : CloudService
        {
            private readonly TemporalConnection connection;

            /// <summary>
            /// Initializes a new instance of the <see cref="CloudService.Core"/> class.
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
