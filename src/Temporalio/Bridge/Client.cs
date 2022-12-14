using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Temporalio.Bridge
{
    internal class Client : SafeHandle
    {
        public static async Task<Client> ConnectAsync(
            Runtime runtime,
            Temporalio.Client.TemporalConnectionOptions options
        )
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<Client>();
                unsafe
                {
                    Interop.Methods.client_connect(
                        runtime.ptr,
                        scope.Pointer(options.ToInteropOptions(scope)),
                        null,
                        scope.FunctionPointer<Interop.ClientConnectCallback>(
                            (userData, success, fail) =>
                            {
                                if (fail != null)
                                {
                                    completion.TrySetException(
                                        new InvalidOperationException(
                                            new ByteArray(runtime, fail).ToUTF8()
                                        )
                                    );
                                }
                                else
                                {
                                    completion.TrySetResult(new Client(runtime, success));
                                }
                            }
                        )
                    );
                }
                return await completion.Task;
            }
        }

        private readonly Runtime runtime;
        private readonly unsafe Interop.Client* ptr;

        private unsafe Client(Runtime runtime, Interop.Client* ptr) : base((IntPtr)ptr, true)
        {
            this.runtime = runtime;
            this.ptr = ptr;
        }

        public override unsafe bool IsInvalid => false;

        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.client_free(this.ptr);
            return true;
        }

        public async Task<T> Call<T>(
            Interop.RpcService service,
            string rpc,
            Google.Protobuf.IMessage req,
            Google.Protobuf.MessageParser<T> resp,
            bool retry,
            IEnumerable<KeyValuePair<string, string>>? metadata,
            TimeSpan? timeout,
            System.Threading.CancellationToken? cancellationToken
        ) where T : Google.Protobuf.IMessage<T>
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<ByteArray>();
                unsafe
                {
                    Interop.Methods.client_rpc_call(
                        ptr,
                        scope.Pointer(
                            new Interop.RpcCallOptions()
                            {
                                service = service,
                                rpc = scope.ByteArray(rpc),
                                req = scope.ByteArray(req.ToByteArray()),
                                retry = (byte)(retry ? 1 : 0),
                                metadata = scope.Metadata(metadata),
                                timeout_millis = (uint)(timeout?.TotalMilliseconds ?? 0),
                                cancellation_token = scope.CancellationToken(cancellationToken)
                            }
                        ),
                        null,
                        scope.FunctionPointer<Interop.ClientRpcCallCallback>(
                            (userData, success, fail) =>
                            {
                                if (fail != null)
                                {
                                    completion.TrySetException(
                                        new InvalidOperationException(
                                            new ByteArray(runtime, fail).ToUTF8()
                                        )
                                    );
                                }
                                else
                                {
                                    completion.TrySetResult(new ByteArray(runtime, success));
                                }
                            }
                        )
                    );
                }
                return (await completion.Task).ToProto(resp);
            }
        }
    }
}
