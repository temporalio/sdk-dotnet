using System.Threading.Tasks;
using Google.Protobuf;

namespace Temporalio.Client
{
    public abstract partial class OperatorService
    {
        protected abstract Task<T> InvokeRpcAsync<T>(
            string rpc,
            IMessage req,
            MessageParser<T> resp,
            RpcOptions? options = null
        ) where T : Google.Protobuf.IMessage<T>;

        public class Impl : OperatorService
        {
            private readonly TemporalConnection connection;

            public Impl(TemporalConnection connection)
            {
                this.connection = connection;
            }

            protected async override Task<T> InvokeRpcAsync<T>(
                string rpc,
                IMessage req,
                MessageParser<T> resp,
                RpcOptions? options = null
            )
            {
                return await connection.InvokeRpcAsync(
                    TemporalConnection.RpcService.Operator,
                    rpc,
                    req,
                    resp,
                    options
                );
            }
        }
    }
}
