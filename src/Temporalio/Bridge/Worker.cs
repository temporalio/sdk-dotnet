using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned Temporal worker.
    /// </summary>
    internal class Worker : IDisposable
    {
        private readonly SafeHandleReference<SafeWorkerHandle> handleRef;

        /// <summary>
        /// Initializes a new instance of the <see cref="Worker"/> class.
        /// </summary>
        /// <param name="runtime">Runtime for the worker.</param>
        /// <param name="clientHandle">Client handle for the worker.</param>
        /// <param name="namespace">Namespace for the worker.</param>
        /// <param name="options">Options for the worker.</param>
        /// <param name="loggerFactory">Logger factory, used instead of the one in options by
        ///   anything in the bridge that needs it, since it's guaranteed to be set.</param>
        /// <param name="clientPlugins">Client plugins to include in heartbeat.</param>
        /// <exception cref="Exception">
        /// If any of the options are invalid including improperly defined workflows/activities.
        /// </exception>
        public Worker(
            Runtime runtime,
            SafeClientHandle clientHandle,
            string @namespace,
            Temporalio.Worker.TemporalWorkerOptions options,
            ILoggerFactory loggerFactory,
            IReadOnlyCollection<Temporalio.Client.ITemporalClientPlugin>? clientPlugins = null)
            : this(runtime, CreateWorkerHandle(runtime, clientHandle, @namespace, options, loggerFactory, clientPlugins))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Worker"/> class. For use when a worker
        /// pointer already exists (like for replayer).
        /// </summary>
        /// <param name="runtime">Runtime.</param>
        /// <param name="handle">Pointer.</param>
        internal Worker(Runtime runtime, SafeWorkerHandle handle)
            : this(runtime, SafeHandleReference<SafeWorkerHandle>.AddRef(handle))
        {
        }

        private Worker(Runtime runtime, SafeHandleReference<SafeWorkerHandle> handleRef)
        {
            Runtime = runtime;
            this.handleRef = handleRef;
        }

        /// <summary>
        /// Gets the worker handle.
        /// </summary>
        internal SafeWorkerHandle Handle => handleRef.Handle;

        /// <summary>
        /// Gets the runtime associated with this worker.
        /// </summary>
        internal Runtime Runtime { get; private init; }

        /// <summary>
        /// Validate the worker.
        /// </summary>
        /// <returns>Validation task.</returns>
        public async Task ValidateAsync()
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                unsafe
                {
                    Interop.Methods.temporal_core_worker_validate(
                        scope.Pointer(Handle),
                        null,
                        scope.FunctionPointer<Interop.TemporalCoreWorkerCallback>(
                            (userData, fail) =>
                            {
                                if (fail != null)
                                {
                                    completion.TrySetException(new InvalidOperationException(
                                        new ByteArray(Runtime, fail).ToUTF8()));
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

        /// <summary>
        /// Replace the client.
        /// </summary>
        /// <param name="clientHandle">New client handle.</param>
        public void ReplaceClient(SafeClientHandle clientHandle)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    Interop.Methods.temporal_core_worker_replace_client(
                        scope.Pointer(Handle),
                        scope.Pointer(clientHandle));
                }
            }
        }

        /// <summary>
        /// Poll for the next workflow activation.
        /// </summary>
        /// <remarks>Only virtual for testing.</remarks>
        /// <returns>The activation or null if poller is shut down.</returns>
        public virtual async Task<Api.WorkflowActivation.WorkflowActivation?> PollWorkflowActivationAsync()
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<ByteArray?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                unsafe
                {
                    Interop.Methods.temporal_core_worker_poll_workflow_activation(
                        scope.Pointer(Handle),
                        null,
                        scope.FunctionPointer<Interop.TemporalCoreWorkerPollCallback>(
                            (userData, success, fail) =>
                            {
                                if (fail != null)
                                {
                                    completion.TrySetException(new InvalidOperationException(
                                        new ByteArray(Runtime, fail).ToUTF8()));
                                }
                                else if (success == null)
                                {
                                    completion.TrySetResult(null);
                                }
                                else
                                {
                                    completion.TrySetResult(new ByteArray(Runtime, success));
                                }
                            }));
                }
                return (await completion.Task.ConfigureAwait(false))?.ToProto(
                    Api.WorkflowActivation.WorkflowActivation.Parser);
            }
        }

        /// <summary>
        /// Poll for the next activity task.
        /// </summary>
        /// <remarks>Only virtual for testing.</remarks>
        /// <returns>The task or null if poller is shut down.</returns>
        public virtual async Task<Api.ActivityTask.ActivityTask?> PollActivityTaskAsync()
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<ByteArray?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                unsafe
                {
                    Interop.Methods.temporal_core_worker_poll_activity_task(
                        scope.Pointer(Handle),
                        null,
                        scope.FunctionPointer<Interop.TemporalCoreWorkerPollCallback>(
                            (userData, success, fail) =>
                            {
                                if (fail != null)
                                {
                                    completion.TrySetException(new InvalidOperationException(
                                        new ByteArray(Runtime, fail).ToUTF8()));
                                }
                                else if (success == null)
                                {
                                    completion.TrySetResult(null);
                                }
                                else
                                {
                                    completion.TrySetResult(new ByteArray(Runtime, success));
                                }
                            }));
                }
                return (await completion.Task.ConfigureAwait(false))?.ToProto(
                    Api.ActivityTask.ActivityTask.Parser);
            }
        }

        /// <summary>
        /// Poll for the next Nexus task.
        /// </summary>
        /// <remarks>Only virtual for testing.</remarks>
        /// <returns>The task or null if poller is shut down.</returns>
        public virtual async Task<Api.Nexus.NexusTask?> PollNexusTaskAsync()
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<ByteArray?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                unsafe
                {
                    Interop.Methods.temporal_core_worker_poll_nexus_task(
                        scope.Pointer(Handle),
                        null,
                        scope.FunctionPointer<Interop.TemporalCoreWorkerPollCallback>(
                            (userData, success, fail) =>
                            {
                                if (fail != null)
                                {
                                    completion.TrySetException(new InvalidOperationException(
                                        new ByteArray(Runtime, fail).ToUTF8()));
                                }
                                else if (success == null)
                                {
                                    completion.TrySetResult(null);
                                }
                                else
                                {
                                    completion.TrySetResult(new ByteArray(Runtime, success));
                                }
                            }));
                }
                return (await completion.Task.ConfigureAwait(false))?.ToProto(
                    Api.Nexus.NexusTask.Parser);
            }
        }

        /// <summary>
        /// Complete a workflow activation.
        /// </summary>
        /// <param name="comp">Activation completion.</param>
        /// <returns>Completion task.</returns>
        public async Task CompleteWorkflowActivationAsync(
            Api.WorkflowCompletion.WorkflowActivationCompletion comp)
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                unsafe
                {
                    Interop.Methods.temporal_core_worker_complete_workflow_activation(
                        scope.Pointer(Handle),
                        scope.ByteArray(comp.ToByteArray()),
                        null,
                        scope.FunctionPointer<Interop.TemporalCoreWorkerCallback>(
                            (userData, fail) =>
                            {
                                if (fail != null)
                                {
                                    completion.TrySetException(new InvalidOperationException(
                                        new ByteArray(Runtime, fail).ToUTF8()));
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

        /// <summary>
        /// Complete an activity task.
        /// </summary>
        /// <param name="comp">Task completion.</param>
        /// <returns>Completion task.</returns>
        public async Task CompleteActivityTaskAsync(Api.ActivityTaskCompletion comp)
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                unsafe
                {
                    Interop.Methods.temporal_core_worker_complete_activity_task(
                        scope.Pointer(Handle),
                        scope.ByteArray(comp.ToByteArray()),
                        null,
                        scope.FunctionPointer<Interop.TemporalCoreWorkerCallback>(
                            (userData, fail) =>
                            {
                                if (fail != null)
                                {
                                    completion.TrySetException(new InvalidOperationException(
                                        new ByteArray(Runtime, fail).ToUTF8()));
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

        /// <summary>
        /// Complete a Nexus task.
        /// </summary>
        /// <param name="comp">Task completion.</param>
        /// <returns>Completion task.</returns>
        public async Task CompleteNexusTaskAsync(Api.Nexus.NexusTaskCompletion comp)
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                unsafe
                {
                    Interop.Methods.temporal_core_worker_complete_nexus_task(
                        scope.Pointer(Handle),
                        scope.ByteArray(comp.ToByteArray()),
                        null,
                        scope.FunctionPointer<Interop.TemporalCoreWorkerCallback>(
                            (userData, fail) =>
                            {
                                if (fail != null)
                                {
                                    completion.TrySetException(new InvalidOperationException(
                                        new ByteArray(Runtime, fail).ToUTF8()));
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

        /// <summary>
        /// Record an activity heartbeat.
        /// </summary>
        /// <param name="heartbeat">Heartbeat to record.</param>
        public void RecordActivityHeartbeat(Api.ActivityHeartbeat heartbeat)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    var fail = Interop.Methods.temporal_core_worker_record_activity_heartbeat(
                        scope.Pointer(Handle),
                        scope.ByteArray(heartbeat.ToByteArray()));
                    if (fail != null)
                    {
                        string failStr;
                        using (var byteArray = new ByteArray(Runtime, fail))
                        {
                            failStr = byteArray.ToUTF8();
                        }
                        throw new InvalidOperationException(failStr);
                    }
                }
            }
        }

        /// <summary>
        /// Request workflow eviction.
        /// </summary>
        /// <param name="runId">Run ID of the workflow to evict.</param>
        public void RequestWorkflowEviction(string runId)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    Interop.Methods.temporal_core_worker_request_workflow_eviction(
                        scope.Pointer(Handle),
                        scope.ByteArray(runId));
                }
            }
        }

        /// <summary>
        /// Initiate shutdown for this worker.
        /// </summary>
        public void InitiateShutdown()
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    Interop.Methods.temporal_core_worker_initiate_shutdown(scope.Pointer(Handle));
                }
            }
        }

        /// <summary>
        /// Finalize shutdown of this worker. This should only be called after shutdown and all
        /// polling has stopped.
        /// </summary>
        /// <returns>Completion task.</returns>
        public async Task FinalizeShutdownAsync()
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                unsafe
                {
                    Interop.Methods.temporal_core_worker_finalize_shutdown(
                        scope.Pointer(Handle),
                        null,
                        scope.FunctionPointer<Interop.TemporalCoreWorkerCallback>(
                            (userData, fail) =>
                            {
                                if (fail != null)
                                {
                                    completion.TrySetException(new InvalidOperationException(
                                        new ByteArray(Runtime, fail).ToUTF8()));
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

        /// <inheritdoc/>
        public void Dispose()
        {
            // The handle reference understands whether to dispose or release the handle
            handleRef.Dispose();
        }

        private static unsafe SafeHandleReference<SafeWorkerHandle> CreateWorkerHandle(
            Runtime runtime,
            SafeClientHandle clientHandle,
            string @namespace,
            Temporalio.Worker.TemporalWorkerOptions options,
            ILoggerFactory loggerFactory,
            IReadOnlyCollection<Temporalio.Client.ITemporalClientPlugin>? clientPlugins)
        {
            using Scope scope = new();

            var workerOrFail = Interop.Methods.temporal_core_worker_new(
                scope.Pointer(clientHandle),
                scope.Pointer(options.ToInteropOptions(scope, @namespace, loggerFactory, clientPlugins)));

            if (workerOrFail.fail != null)
            {
                string failStr;
                using (var byteArray = new ByteArray(runtime, workerOrFail.fail))
                {
                    failStr = byteArray.ToUTF8();
                }
                throw new InvalidOperationException(failStr);
            }

            return SafeHandleReference<SafeWorkerHandle>.Owned(
                new SafeWorkerHandle(workerOrFail.worker));
        }
    }
}
