using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned Temporal client.
    /// </summary>
    internal class Client : SafeHandle
    {
        private unsafe Client(Runtime runtime, Interop.TemporalCoreClient* ptr)
            : base((IntPtr)ptr, true)
        {
            Runtime = runtime;
            Handle = new SafeClientHandle(ptr);
        }

        /// <inheritdoc />
        public override bool IsInvalid => Handle.IsInvalid;

        /// <summary>
        /// Gets the runtime associated with this client.
        /// </summary>
        internal Runtime Runtime { get; private init; }

        /// <summary>
        /// Gets the safe handle for the client.
        /// </summary>
        internal SafeClientHandle Handle { get; private init; }

        /// <summary>
        /// Connect to Temporal.
        /// </summary>
        /// <param name="runtime">Runtime to use.</param>
        /// <param name="options">Options for connection.</param>
        /// <returns>Connected client.</returns>
        public static async Task<Client> ConnectAsync(
            Runtime runtime,
            Temporalio.Client.TemporalConnectionOptions options)
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<Client>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                unsafe
                {
                    Interop.Methods.temporal_core_client_connect(
                        runtime.Ptr,
                        scope.Pointer(options.ToInteropOptions(scope)),
                        null,
                        scope.FunctionPointer<Interop.TemporalCoreClientConnectCallback>(
                            (userData, success, fail) =>
                            {
                                if (fail != null)
                                {
                                    completion.TrySetException(
                                        new InvalidOperationException(
                                            new ByteArray(runtime, fail).ToUTF8()));
                                }
                                else
                                {
                                    completion.TrySetResult(new Client(runtime, success));
                                }
                            }));
                }
                return await completion.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Update client metadata (i.e. headers).
        /// </summary>
        /// <param name="metadata">Metadata to set.</param>
        public void UpdateMetadata(IReadOnlyCollection<KeyValuePair<string, string>> metadata)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    Interop.Methods.temporal_core_client_update_metadata(
                        scope.Pointer(Handle),
                        scope.ByteArrayArray(metadata));
                }
            }
        }

        /// <summary>
        /// Update client gRPC binary metadata (i.e. binary headers).
        /// </summary>
        /// <param name="metadata">Binary metadata to set.</param>
        public void UpdateBinaryMetadata(IReadOnlyCollection<KeyValuePair<string, byte[]>> metadata)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    Interop.Methods.temporal_core_client_update_binary_metadata(
                        scope.Pointer(Handle),
                        scope.ByteArrayArray(metadata));
                }
            }
        }

        /// <summary>
        /// Update client API key.
        /// </summary>
        /// <param name="apiKey">API key to set.</param>
        public void UpdateApiKey(string? apiKey)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    Interop.Methods.temporal_core_client_update_api_key(
                        scope.Pointer(Handle),
                        scope.ByteArray(apiKey));
                }
            }
        }

        /// <summary>
        /// Make RPC call to Temporal.
        /// </summary>
        /// <typeparam name="T">Return proto type.</typeparam>
        /// <param name="service">Service to call.</param>
        /// <param name="rpc">RPC operation to call.</param>
        /// <param name="req">Proto request.</param>
        /// <param name="resp">Proto response parser.</param>
        /// <param name="retry">Whether to retry or not.</param>
        /// <param name="metadata">Metadata to include.</param>
        /// <param name="binaryMetadata">Binary metadata to include.</param>
        /// <param name="timeout">Timeout for the call.</param>
        /// <param name="cancellationToken">Cancellation token for the call.</param>
        /// <returns>Response proto.</returns>
        public async Task<T> CallAsync<T>(
            Interop.TemporalCoreRpcService service,
            string rpc,
            IMessage req,
            MessageParser<T> resp,
            bool retry,
            IReadOnlyCollection<KeyValuePair<string, string>>? metadata,
            IReadOnlyCollection<KeyValuePair<string, byte[]>>? binaryMetadata,
            TimeSpan? timeout,
            System.Threading.CancellationToken? cancellationToken)
            where T : IMessage<T>
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<ByteArray>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                unsafe
                {
                    Interop.Methods.temporal_core_client_rpc_call(
                        scope.Pointer(Handle),
                        scope.Pointer(
                            new Interop.TemporalCoreRpcCallOptions()
                            {
                                service = service,
                                rpc = scope.ByteArray(rpc),
                                req = scope.ByteArray(req.ToByteArray()),
                                retry = (byte)(retry ? 1 : 0),
                                metadata = scope.ByteArrayArray(metadata),
                                binary_metadata = scope.ByteArrayArray(binaryMetadata),
                                timeout_millis = (uint)(timeout?.TotalMilliseconds ?? 0),
                                cancellation_token = scope.CancellationToken(cancellationToken),
                            }),
                        null,
                        scope.FunctionPointer<Interop.TemporalCoreClientRpcCallCallback>(
                            (userData, success, statusCode, failureMessage, failureDetails) =>
                            {
                                if (failureMessage != null && statusCode > 0)
                                {
                                    byte[]? rawStatus = null;
                                    if (failureDetails != null)
                                    {
                                        rawStatus = new ByteArray(Runtime, failureDetails).ToByteArray();
                                    }
                                    completion.TrySetException(new Exceptions.RpcException(
                                        (Exceptions.RpcException.StatusCode)statusCode,
                                        new ByteArray(Runtime, failureMessage).ToUTF8(),
                                        rawStatus));
                                }
                                else if (failureMessage != null)
                                {
                                    var failureString = new ByteArray(Runtime, failureMessage).ToUTF8();
                                    // If the cancellation token caused cancel, throw that instead
                                    if (cancellationToken.HasValue &&
                                        cancellationToken.Value.IsCancellationRequested &&
                                        failureString == "Cancelled")
                                    {
                                        completion.TrySetException(
                                            new OperationCanceledException(cancellationToken.Value));
                                    }
                                    else
                                    {
                                        completion.TrySetException(
                                            new InvalidOperationException(failureString));
                                    }
                                }
                                else
                                {
                                    completion.TrySetResult(new ByteArray(Runtime, success));
                                }
                            }));
                }
                return (await completion.Task.ConfigureAwait(false)).ToProto(resp);
            }
        }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Handle.Dispose();
            return true;
        }
    }
}
