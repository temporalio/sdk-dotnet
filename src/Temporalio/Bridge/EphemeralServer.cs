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
        public static async Task<EphemeralServer> StartTemporaliteAsync(
            Runtime runtime,
            Testing.WorkflowEnvironmentStartLocalOptions options
        )
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<EphemeralServer>();
                unsafe
                {
                    Interop.Methods.ephemeral_server_start_temporalite(
                        runtime.ptr,
                        scope.Pointer(options.ToInteropOptions(scope)),
                        null,
                        CallbackForStart(runtime, scope, true, completion)
                    );
                }
                return await completion.Task;
            }
        }

        public static async Task<EphemeralServer> StartTestServerAsync(
            Runtime runtime,
            Testing.WorkflowEnvironmentStartTimeSkippingOptions options
        )
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<EphemeralServer>();
                unsafe
                {
                    Interop.Methods.ephemeral_server_start_test_server(
                        runtime.ptr,
                        scope.Pointer(options.ToInteropOptions(scope)),
                        null,
                        CallbackForStart(runtime, scope, true, completion)
                    );
                }
                return await completion.Task;
            }
        }

        private static unsafe IntPtr CallbackForStart(
            Runtime runtime,
            Scope scope,
            bool hasTestService,
            TaskCompletionSource<EphemeralServer> completion
        )
        {
            return scope.FunctionPointer<Interop.EphemeralServerStartCallback>(
                (userData, success, successTarget, fail) =>
                {
                    if (fail != null)
                    {
                        completion.TrySetException(
                            new InvalidOperationException(new ByteArray(runtime, fail).ToUTF8())
                        );
                    }
                    else
                    {
                        completion.TrySetResult(
                            new EphemeralServer(
                                runtime,
                                success,
                                new ByteArray(runtime, successTarget).ToUTF8(),
                                hasTestService
                            )
                        );
                    }
                }
            );
        }

        private readonly Runtime runtime;
        private readonly unsafe Interop.EphemeralServer* ptr;

        private unsafe EphemeralServer(
            Runtime runtime,
            Interop.EphemeralServer* ptr,
            string target,
            bool hasTestService
        ) : base((IntPtr)ptr, true)
        {
            this.runtime = runtime;
            this.ptr = ptr;
            Target = target;
            HasTestService = hasTestService;
        }

        public override unsafe bool IsInvalid => false;

        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.ephemeral_server_free(ptr);
            return true;
        }

        public string Target { get; private init; }
        public bool HasTestService { get; private init; }

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
                                            new ByteArray(runtime, fail).ToUTF8()
                                        )
                                    );
                                }
                                else
                                {
                                    completion.TrySetResult(true);
                                }
                            }
                        )
                    );
                }
                await completion.Task;
            }
        }
    }
}
