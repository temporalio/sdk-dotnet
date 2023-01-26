using System.Threading.Tasks;
using Google.Protobuf;

namespace Temporalio.Client
{
    /// <summary>
    /// Base class for raw gRPC services.
    /// </summary>
    public abstract class RpcService
    {
        /// <summary>
        /// Gets the service.
        /// </summary>
        internal abstract Bridge.Interop.RpcService Service { get; }

        /// <summary>
        /// Invoke an RPC method.
        /// </summary>
        /// <typeparam name="T">Resulting protobuf message type.</typeparam>
        /// <param name="rpc">Name of the RPC method.</param>
        /// <param name="req">Protobuf message request.</param>
        /// <param name="resp">Parser for the protobuf message response.</param>
        /// <param name="options">Optional RPC options for the call.</param>
        /// <returns>Parsed protobuf result.</returns>
        protected abstract Task<T> InvokeRpcAsync<T>(
            string rpc,
            IMessage req,
            MessageParser<T> resp,
            RpcOptions? options = null)
            where T : IMessage<T>;
    }
}
