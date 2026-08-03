using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
#if NETCOREAPP3_0_OR_GREATER
using System.Threading;
#endif
using System.Threading.Tasks;
using Temporalio.Extensions.WorkflowStreams.Internal;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// An async, single-use subscription over a workflow stream. Polling runs on the thread pool
    /// without occupying a thread while a poll is blocked on the server; the consumer only waits
    /// for the next item. The subscription ends cleanly (<see cref="MoveNextAsync" /> returns
    /// false) when the workflow reaches a terminal state, and automatically follows
    /// continue-as-new chains; closing the owning <see cref="WorkflowStreamClient" /> also ends
    /// it.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change.
    /// <para>
    /// <see cref="Dispose" /> stops the subscription before the next poll; a poll already blocked
    /// on the server is not interrupted. Items already buffered still drain after
    /// <see cref="Dispose" />.
    /// </para>
    /// </remarks>
#if NETCOREAPP3_0_OR_GREATER
    public sealed class WorkflowStreamSubscription :
        IDisposable, IAsyncEnumerable<WorkflowStreamItem>, IAsyncEnumerator<WorkflowStreamItem>
#else
    public sealed class WorkflowStreamSubscription : IDisposable
#endif
    {
        private readonly SubscriptionDriver driver;
        private readonly object lockObj = new();

        // Hand-off state, guarded by lockObj. The driver's pending-task backpressure keeps the
        // buffer at no more than one item: each OnNextAsync parks the driver on a gate that
        // MoveNextAsync releases when the consumer takes the item, so the next long poll only
        // fires once the consumer drains what the driver already fetched — the same pacing as
        // driving the poll loop on the consumer's task.
        private readonly Queue<WorkflowStreamItem> buffer = new();
        private TaskCompletionSource<object?>? availability;
        private TaskCompletionSource<object?>? pendingGate;
        private Exception? error;
        private bool streamDone;

        // Consumer-side state.
        private bool started;
        private bool errorThrown;
#if NETCOREAPP3_0_OR_GREATER
        private int enumeratorTaken;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStreamSubscription"/> class.
        /// </summary>
        /// <param name="driverFactory">Builds the driver for a listener; the subscription passes
        /// its adapter listener.</param>
        internal WorkflowStreamSubscription(Func<WorkflowStreamListener, SubscriptionDriver> driverFactory)
        {
            driver = driverFactory(new AdapterListener(this));
            // One hook covers every way the stream ends: terminal state, failure, Dispose, and
            // the owning client closing. It wakes a consumer blocked in MoveNextAsync.
            _ = ObserveCompletionAsync();
        }

        /// <summary>
        /// Gets the item most recently produced by <see cref="MoveNextAsync" />.
        /// </summary>
        public WorkflowStreamItem Current { get; private set; } = default!;

        /// <summary>
        /// Advances to the next item, waiting asynchronously until one is available, the stream
        /// ends, or an error occurs. The first call starts the polling. An unrecoverable poll
        /// failure is rethrown from the first call that observes it (once — the subscription is
        /// over and later calls return false).
        /// </summary>
        /// <returns>True if <see cref="Current" /> holds a new item; false at the end of the
        /// stream or after <see cref="Dispose" /> once buffered items have drained.</returns>
        public async Task<bool> MoveNextAsync()
        {
            if (!started)
            {
                started = true;
                driver.Start();
            }
            while (true)
            {
                Task? waitTask = null;
                TaskCompletionSource<object?>? gateToRelease = null;
                lock (lockObj)
                {
                    if (buffer.Count > 0)
                    {
                        Current = buffer.Dequeue();
                        if (buffer.Count == 0 && pendingGate != null)
                        {
                            gateToRelease = pendingGate;
                            pendingGate = null;
                        }
                    }
                    else if (streamDone)
                    {
                        if (error != null && !errorThrown)
                        {
                            errorThrown = true;
                            ExceptionDispatchInfo.Capture(error).Throw();
                        }
                        return false;
                    }
                    else
                    {
                        availability ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                        waitTask = availability.Task;
                    }
                }
                if (waitTask == null)
                {
                    // Took an item. Release its gate outside the lock: it resumes the driver,
                    // which may call OnNextAsync and take the lock again.
                    gateToRelease?.TrySetResult(null);
                    return true;
                }
                await waitTask.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Stops the subscription before the next poll; a poll already blocked on the server is
        /// not interrupted. Items already fetched still drain. Idempotent.
        /// </summary>
        public void Dispose()
        {
            TaskCompletionSource<object?>? gate;
            lock (lockObj)
            {
                gate = pendingGate;
                pendingGate = null;
            }
            driver.Close();
            gate?.TrySetResult(null);
        }

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Returns this subscription as its own enumerator. The subscription is single-use, so
        /// this may only be called once (typically via <c>await foreach</c>).
        /// </summary>
        /// <param name="cancellationToken">Ignored; stopping is done via <see cref="Dispose" />.
        /// </param>
        /// <returns>This subscription.</returns>
        public IAsyncEnumerator<WorkflowStreamItem> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref enumeratorTaken, 1) != 0)
            {
                throw new InvalidOperationException(
                    "WorkflowStreamSubscription is single-use and cannot be enumerated twice");
            }
            return this;
        }

        /// <inheritdoc />
        ValueTask<bool> IAsyncEnumerator<WorkflowStreamItem>.MoveNextAsync() =>
            new ValueTask<bool>(MoveNextAsync());

        /// <summary>
        /// Disposes the subscription; see <see cref="Dispose" />.
        /// </summary>
        /// <returns>A completed value task.</returns>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
#endif

        private async Task ObserveCompletionAsync()
        {
            try
            {
#pragma warning disable VSTHRD003 // Awaiting our own driver's completion source task
                await driver.Completion.ConfigureAwait(false);
#pragma warning restore VSTHRD003
            }
#pragma warning disable CA1031 // Any driver failure is recorded for MoveNextAsync to rethrow
            catch (Exception e)
#pragma warning restore CA1031
            {
                lock (lockObj)
                {
                    error = e;
                }
            }
            lock (lockObj)
            {
                streamDone = true;
                availability?.TrySetResult(null);
                availability = null;
            }
        }

        private void OnNext(WorkflowStreamItem item, TaskCompletionSource<object?> gate)
        {
            lock (lockObj)
            {
                buffer.Enqueue(item);
                pendingGate = gate;
                availability?.TrySetResult(null);
                availability = null;
            }
        }

        /// <summary>
        /// Feeds the driver's items into the consumer-visible buffer, parking the driver on a
        /// per-item gate until the consumer takes the item (1-item buffer backpressure). The
        /// completion hook above records the end state, so the error/completed callbacks have
        /// nothing to do.
        /// </summary>
        private sealed class AdapterListener : WorkflowStreamListener
        {
            private readonly WorkflowStreamSubscription subscription;

            public AdapterListener(WorkflowStreamSubscription subscription) =>
                this.subscription = subscription;

            public override Task OnNextAsync(WorkflowStreamItem item)
            {
                var gate = new TaskCompletionSource<object?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                subscription.OnNext(item, gate);
                return gate.Task;
            }
        }
    }
}
