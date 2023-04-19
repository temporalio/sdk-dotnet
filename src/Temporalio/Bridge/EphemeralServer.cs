using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned ephemeral server.
    /// </summary>
    internal class EphemeralServer : SafeHandle
    {
        private readonly Runtime runtime;

        private readonly unsafe Interop.EphemeralServer* ptr;

        private unsafe EphemeralServer(
            Runtime runtime,
            Interop.EphemeralServer* ptr,
            string target,
            bool hasTestService)
            : base((IntPtr)ptr, true)
        {
            this.runtime = runtime;
            this.ptr = ptr;
            Target = target;
            HasTestService = hasTestService;
        }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => false;

        /// <summary>
        /// Gets the target host:port of the server.
        /// </summary>
        public string Target { get; private init; }

        /// <summary>
        /// Gets a value indicating whether the server implements test service.
        /// </summary>
        public bool HasTestService { get; private init; }

        /// <summary>
        /// Start Temporalite.
        /// </summary>
        /// <param name="runtime">Runtime to use.</param>
        /// <param name="options">Options to use.</param>
        /// <returns>Started server.</returns>
        public static async Task<EphemeralServer> StartTemporaliteAsync(
            Runtime runtime,
            Testing.WorkflowEnvironmentStartLocalOptions options)
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<EphemeralServer>();
                unsafe
                {
                    Interop.Methods.ephemeral_server_start_temporalite(
                        runtime.Ptr,
                        scope.Pointer(options.ToInteropOptions(scope)),
                        null,
                        CallbackForStart(runtime, scope, false, completion));
                }
                return await completion.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Start test server.
        /// </summary>
        /// <param name="runtime">Runtime to use.</param>
        /// <param name="options">Options to use.</param>
        /// <returns>Started server.</returns>
        public static async Task<EphemeralServer> StartTestServerAsync(
            Runtime runtime,
            Testing.WorkflowEnvironmentStartTimeSkippingOptions options)
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<EphemeralServer>();
                unsafe
                {
                    Interop.Methods.ephemeral_server_start_test_server(
                        runtime.Ptr,
                        scope.Pointer(options.ToInteropOptions(scope)),
                        null,
                        CallbackForStart(runtime, scope, true, completion));
                }
                return await completion.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Shutdown the server.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ShutdownAsync()
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<bool>();
                unsafe
                {
                    Interop.Methods.ephemeral_server_shutdown(
                        ptr,
                        null,
                        scope.FunctionPointer<Interop.EphemeralServerShutdownCallback>(
                            (userData, fail) =>
                            {
                                if (fail != null)
                                {
                                    completion.TrySetException(
                                        new InvalidOperationException(
                                            new ByteArray(runtime, fail).ToUTF8()));
                                }
                                else
                                {
                                    completion.TrySetResult(true);
                                }
                            }));
                }
                await completion.Task.ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.ephemeral_server_free(ptr);
            return true;
        }

        private static unsafe IntPtr CallbackForStart(
            Runtime runtime,
            Scope scope,
            bool hasTestService,
            TaskCompletionSource<EphemeralServer> completion)
        {
            return scope.FunctionPointer<Interop.EphemeralServerStartCallback>(
                (userData, success, successTarget, fail) =>
                {
                    if (fail != null)
                    {
                        completion.TrySetException(
                            new InvalidOperationException(new ByteArray(runtime, fail).ToUTF8()));
                    }
                    else
                    {
                        completion.TrySetResult(
                            new EphemeralServer(
                                runtime,
                                success,
                                new ByteArray(runtime, successTarget).ToUTF8(),
                                hasTestService));
                    }
                });
        }
    }
}
