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

        private readonly unsafe Interop.TemporalCoreEphemeralServer* ptr;

        private unsafe EphemeralServer(
            Runtime runtime,
            Interop.TemporalCoreEphemeralServer* ptr,
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
        public override unsafe bool IsInvalid => ptr == null;

        /// <summary>
        /// Gets the target <c>host:port</c> of the server.
        /// </summary>
        public string Target { get; private init; }

        /// <summary>
        /// Gets a value indicating whether the server implements test service.
        /// </summary>
        public bool HasTestService { get; private init; }

        /// <summary>
        /// Start dev server.
        /// </summary>
        /// <param name="runtime">Runtime to use.</param>
        /// <param name="options">Options to use.</param>
        /// <returns>Started server.</returns>
        public static Task<EphemeralServer> StartDevServerAsync(
            Runtime runtime,
            Testing.WorkflowEnvironmentStartLocalOptions options) =>
            Scope.WithScopeAsync<EphemeralServer>(
                runtime,
                scope =>
                {
                    unsafe
                    {
                        Interop.Methods.temporal_core_ephemeral_server_start_dev_server(
                            runtime.Ptr,
                            scope.Pointer(options.ToInteropOptions(scope)),
                            null,
                            CallbackForStart(runtime, scope, false));
                    }
                },
                TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Start test server.
        /// </summary>
        /// <param name="runtime">Runtime to use.</param>
        /// <param name="options">Options to use.</param>
        /// <returns>Started server.</returns>
        public static Task<EphemeralServer> StartTestServerAsync(
            Runtime runtime,
            Testing.WorkflowEnvironmentStartTimeSkippingOptions options) =>
            Scope.WithScopeAsync<EphemeralServer>(
                runtime,
                scope =>
                {
                    unsafe
                    {
                        Interop.Methods.temporal_core_ephemeral_server_start_test_server(
                            runtime.Ptr,
                            scope.Pointer(options.ToInteropOptions(scope)),
                            null,
                            CallbackForStart(runtime, scope, true));
                    }
                },
                TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Shutdown the server.
        /// </summary>
        /// <returns>Task.</returns>
        public Task ShutdownAsync() => Scope.WithScopeAsync<bool>(this, scope =>
        {
            unsafe
            {
                try
                {
                    Interop.Methods.temporal_core_ephemeral_server_shutdown(
                        ptr,
                        null,
                        scope.CallbackPointer<Interop.TemporalCoreEphemeralServerShutdownCallback>(
                            (userData, fail) =>
                            {
                                if (fail != null)
                                {
                                    scope.Completion.TrySetException(
                                        new InvalidOperationException(
                                            new ByteArray(runtime, fail).ToUTF8()));
                                }
                                else
                                {
                                    scope.Completion.TrySetResult(true);
                                }
                            }));
                }
                finally
                {
                    scope.OnCallbackExit();
                }
            }
        });

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.temporal_core_ephemeral_server_free(ptr);
            return true;
        }

        private static unsafe IntPtr CallbackForStart(
            Runtime runtime,
            Scope.WithCallback<EphemeralServer> scope,
            bool hasTestService) =>
            scope.CallbackPointer<Interop.TemporalCoreEphemeralServerStartCallback>(
                (userData, success, successTarget, fail) =>
                {
                    try
                    {
                        if (fail != null)
                        {
                            scope.Completion.TrySetException(
                                new InvalidOperationException(new ByteArray(runtime, fail).ToUTF8()));
                        }
                        else
                        {
                            scope.Completion.TrySetResult(
                                new EphemeralServer(
                                    runtime,
                                    success,
                                    new ByteArray(runtime, successTarget).ToUTF8(),
                                    hasTestService));
                        }
                    }
                    finally
                    {
                        scope.OnCallbackExit();
                    }
                });
    }
}
