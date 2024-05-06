using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned Temporal worker.
    /// </summary>
    internal class Worker : SafeHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Worker"/> class.
        /// </summary>
        /// <param name="client">Client for the worker.</param>
        /// <param name="namespace_">Namespace for the worker.</param>
        /// <param name="options">Options for the worker.</param>
        /// <exception cref="Exception">
        /// If any of the options are invalid including improperly defined workflows/activities.
        /// </exception>
        public Worker(
            Client client, string namespace_, Temporalio.Worker.TemporalWorkerOptions options)
            : base(IntPtr.Zero, true)
        {
            Runtime = client.Runtime;
            using (var scope = new Scope())
            {
                unsafe
                {
                    var workerOrFail = Interop.Methods.worker_new(
                        client.Ptr, scope.Pointer(options.ToInteropOptions(scope, namespace_)));
                    if (workerOrFail.fail != null)
                    {
                        string failStr;
                        using (var byteArray = new ByteArray(Runtime, workerOrFail.fail))
                        {
                            failStr = byteArray.ToUTF8();
                        }
                        throw new InvalidOperationException(failStr);
                    }
                    Ptr = workerOrFail.worker;
                    SetHandle((IntPtr)Ptr);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Worker"/> class. For use when a worker
        /// pointer already exists (like for replayer).
        /// </summary>
        /// <param name="runtime">Runtime.</param>
        /// <param name="ptr">Pointer.</param>
        internal unsafe Worker(Runtime runtime, Interop.Worker* ptr)
            : base(IntPtr.Zero, true)
        {
            Runtime = runtime;
            Ptr = ptr;
            SetHandle((IntPtr)ptr);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Worker"/> class from another. Only for
        /// testing.
        /// </summary>
        /// <param name="other">Other worker to reference.</param>
        internal Worker(Worker other)
            : base(IntPtr.Zero, true)
        {
            unsafe
            {
                Runtime = other.Runtime;
                Ptr = other.Ptr;
                SetHandle((IntPtr)Ptr);
            }
        }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => false;

        /// <summary>
        /// Gets the runtime associated with this worker.
        /// </summary>
        internal Runtime Runtime { get; private init; }

        /// <summary>
        /// Gets a pointer to the worker.
        /// </summary>
        internal unsafe Interop.Worker* Ptr { get; private set; }

        /// <summary>
        /// Replace the client.
        /// </summary>
        /// <param name="client">New client.</param>
        public void ReplaceClient(Client client)
        {
            unsafe
            {
                Interop.Methods.worker_replace_client(Ptr, client.Ptr);
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
                var completion = new TaskCompletionSource<ByteArray?>();
                unsafe
                {
                    Interop.Methods.worker_poll_workflow_activation(
                        Ptr,
                        null,
                        scope.FunctionPointer<Interop.WorkerPollCallback>(
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
                var completion = new TaskCompletionSource<ByteArray?>();
                unsafe
                {
                    Interop.Methods.worker_poll_activity_task(
                        Ptr,
                        null,
                        scope.FunctionPointer<Interop.WorkerPollCallback>(
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
        /// Complete a workflow activation.
        /// </summary>
        /// <param name="comp">Activation completion.</param>
        /// <returns>Completion task.</returns>
        public async Task CompleteWorkflowActivationAsync(
            Api.WorkflowCompletion.WorkflowActivationCompletion comp)
        {
            using (var scope = new Scope())
            {
                var completion = new TaskCompletionSource<bool>();
                unsafe
                {
                    Interop.Methods.worker_complete_workflow_activation(
                        Ptr,
                        scope.ByteArray(comp.ToByteArray()),
                        null,
                        scope.FunctionPointer<Interop.WorkerCallback>(
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
                var completion = new TaskCompletionSource<bool>();
                unsafe
                {
                    Interop.Methods.worker_complete_activity_task(
                        Ptr,
                        scope.ByteArray(comp.ToByteArray()),
                        null,
                        scope.FunctionPointer<Interop.WorkerCallback>(
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
                    var fail = Interop.Methods.worker_record_activity_heartbeat(
                        Ptr, scope.ByteArray(heartbeat.ToByteArray()));
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
                    Interop.Methods.worker_request_workflow_eviction(
                        Ptr, scope.ByteArray(runId));
                }
            }
        }

        /// <summary>
        /// Initiate shutdown for this worker.
        /// </summary>
        public void InitiateShutdown()
        {
            unsafe
            {
                Interop.Methods.worker_initiate_shutdown(Ptr);
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
                var completion = new TaskCompletionSource<bool>();
                unsafe
                {
                    Interop.Methods.worker_finalize_shutdown(
                        Ptr,
                        null,
                        scope.FunctionPointer<Interop.WorkerCallback>(
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

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.worker_free(Ptr);
            return true;
        }
    }
}