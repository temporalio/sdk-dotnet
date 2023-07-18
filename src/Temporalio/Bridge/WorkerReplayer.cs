using System;
using System.Runtime.InteropServices;
using Google.Protobuf;
using Temporalio.Api.History.V1;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned replayer. Callers should dispose of Worker themselves before disposing this.
    /// </summary>
    internal class WorkerReplayer : SafeHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerReplayer"/> class.
        /// </summary>
        /// <param name="runtime">Runtime for the replayer.</param>
        /// <param name="options">Options for the replayer.</param>
        public WorkerReplayer(
            Runtime runtime, Temporalio.Worker.WorkflowReplayerOptions options)
            : base(IntPtr.Zero, true)
        {
            Runtime = runtime;
            using (var scope = new Scope())
            {
                unsafe
                {
                    var replayerOrFail = Interop.Methods.worker_replayer_new(
                        runtime.Ptr, scope.Pointer(options.ToInteropOptions(scope)));
                    if (replayerOrFail.fail != null)
                    {
                        string failStr;
                        using (var byteArray = new ByteArray(Runtime, replayerOrFail.fail))
                        {
                            failStr = byteArray.ToUTF8();
                        }
                        throw new InvalidOperationException(failStr);
                    }
                    Worker = new(runtime, replayerOrFail.worker);
                    Ptr = replayerOrFail.worker_replay_pusher;
                    SetHandle((IntPtr)Ptr);
                }
            }
        }

        /// <summary>
        /// Gets the worker. This should be disposed explicitly by caller.
        /// </summary>
        public Worker Worker { get; private init; }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => false;

        /// <summary>
        /// Gets the runtime associated with this replayer.
        /// </summary>
        internal Runtime Runtime { get; private init; }

        /// <summary>
        /// Gets a pointer to the pusher.
        /// </summary>
        internal unsafe Interop.WorkerReplayPusher* Ptr { get; private init; }

        /// <summary>
        /// Push history to the replayer.
        /// </summary>
        /// <param name="workflowId">ID of the workflow.</param>
        /// <param name="history">History proto for the workflow.</param>
        public void PushHistory(string workflowId, History history)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    Interop.Methods.worker_replay_push(
                        Worker.Ptr,
                        Ptr,
                        scope.ByteArray(workflowId),
                        scope.ByteArray(history.ToByteArray()));
                }
            }
        }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.worker_replay_pusher_free(Ptr);
            return true;
        }
    }
}