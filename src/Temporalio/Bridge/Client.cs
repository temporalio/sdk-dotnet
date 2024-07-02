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
        private unsafe Client(Runtime runtime, Interop.Client* ptr)
            : base((IntPtr)ptr, true)
        {
            Runtime = runtime;
            Ptr = ptr;
        }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => Ptr == null;

        /// <summary>
        /// Gets the runtime associated with this client.
        /// </summary>
        internal Runtime Runtime { get; private init; }

        /// <summary>
        /// Gets the pointer to the client.
        /// </summary>
        internal unsafe Interop.Client* Ptr { get; private init; }

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
                    Interop.Methods.client_connect(
                        runtime.Ptr,
                        scope.Pointer(options.ToInteropOptions(scope)),
                        null,
                        scope.FunctionPointer<Interop.ClientConnectCallback>(
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
        public void UpdateMetadata(IEnumerable<KeyValuePair<string, string>> metadata)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    Interop.Methods.client_update_metadata(Ptr, scope.Metadata(metadata));
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
                    Interop.Methods.client_update_api_key(Ptr, scope.ByteArray(apiKey));
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
        /// <param name="timeout">Timeout for the call.</param>
        /// <param name="cancellationToken">Cancellation token for the call.</param>
        /// <returns>Response proto.</returns>
        public async Task<T> CallAsync<T>(
            Interop.RpcService service,
            string rpc,
            IMessage req,
            MessageParser<T> resp,
            bool retry,
            IEnumerable<KeyValuePair<string, string>>? metadata,
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
                    Interop.Methods.client_rpc_call(
                        Ptr,
                        scope.Pointer(
                            new Interop.RpcCallOptions()
                            {
                                service = service,
                                rpc = scope.ByteArray(rpc),
                                req = scope.ByteArray(req.ToByteArray()),
                                retry = (byte)(retry ? 1 : 0),
                                metadata = scope.Metadata(metadata),
                                timeout_millis = (uint)(timeout?.TotalMilliseconds ?? 0),
                                cancellation_token = scope.CancellationToken(cancellationToken),
                            }),
                        null,
                        scope.FunctionPointer<Interop.ClientRpcCallCallback>(
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
            Interop.Methods.client_free(Ptr);
            return true;
        }
    }
}
