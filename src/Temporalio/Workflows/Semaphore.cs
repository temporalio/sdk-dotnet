using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Semaphore is an alternative to <see cref="System.Threading.Semaphore"/> and
    /// <see cref="SemaphoreSlim"/> built to be deterministic and lightweight. Many of the concepts
    /// are similar to <see cref="SemaphoreSlim"/>.
    /// </summary>
    public class Semaphore
    {
        private static readonly object SemaphoreUnit = new();

        private readonly LinkedList<object> waiters = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="Semaphore"/> class.
        /// </summary>
        /// <param name="initialCount">
        /// The initial number of permits that will be available for requests.
        /// </param>
        public Semaphore(int initialCount)
        {
            if (!Workflow.InWorkflow)
            {
                throw new InvalidOperationException("Cannot use workflow semaphore outside of workflow");
            }
            Max = initialCount;
            CurrentCount = initialCount;
        }

        /// <summary>
        /// Gets the fixed maximum number of permits that can be granted concurrently. This is
        /// always the <c>initialCount</c> value from the constructor.
        /// </summary>
        public int Max { get; init; }

        /// <summary>
        /// Gets the current number of available permits that can be granted concurrently. This is
        /// decremented on each successful <c>WaitAsync</c> call and incremented on each
        /// <c>Release</c> call.
        /// </summary>
        public int CurrentCount { get; private set; }

        /// <summary>
        /// Wait for a permit to become available. If the task succeeds, users must call
        /// <see cref="Release"/> to properly give the permit back when done.
        /// </summary>
        /// <param name="cancellationToken">
        /// Cancellation token that can interrupt this wait. If unset, this defaults to
        /// <see cref="Workflow.CancellationToken"/>. Upon cancel, awaiting the resulting task will
        /// throw a <see cref="TaskCanceledException"/> exception.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the result of the asynchronous operation. This task is
        /// canceled if the cancellation token is.
        /// </returns>
        public Task WaitAsync(CancellationToken? cancellationToken = null) =>
            WaitInternalAsync(null, cancellationToken);

        /// <summary>
        /// Wait for a permit to become available or timeout to be reached. If the task returns
        /// true, users must call <see cref="Release"/> to properly give the permit back when done.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// Milliseconds until timeout. If this is 0, this is a non-blocking task that will return
        /// immediately. If this value is -1 (i.e. <see cref="Timeout.Infinite"/>), there is no
        /// timeout and the result will always be true on success (at which point you might as well
        /// use <see cref="WaitAsync(CancellationToken?)"/>). Otherwise a timer is started for the
        /// timeout and canceled if the wait succeeds.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token that can interrupt this wait. If unset, this defaults to
        /// <see cref="Workflow.CancellationToken"/>. Upon cancel, awaiting the resulting task will
        /// throw a <see cref="TaskCanceledException"/> exception.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the result of the asynchronous operation. The task is
        /// true if the wait succeeded and false if it timed out. This task is canceled if the
        /// cancellation token is.
        /// </returns>
        public Task<bool> WaitAsync(
            int millisecondsTimeout,
            CancellationToken? cancellationToken = null) =>
            WaitInternalAsync(TimeSpan.FromMilliseconds(millisecondsTimeout), cancellationToken);

        /// <summary>
        /// Wait for a permit to become available or timeout to be reached. If the task returns
        /// true, users must call <see cref="Release"/> to properly give the permit back when done.
        /// </summary>
        /// <param name="timeout">
        /// TimeSpan until timeout. If this is <see cref="TimeSpan.Zero"/>, this is a non-blocking
        /// task that will return immediately. If this value is -1ms (i.e.
        /// <see cref="Timeout.InfiniteTimeSpan"/>), there is no timeout and the result will always
        /// be true on success (at which point you might as well use
        /// <see cref="WaitAsync(CancellationToken?)"/>). Otherwise a timer is started for the
        /// timeout and canceled if the wait succeeds.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token that can interrupt this wait. If unset, this defaults to
        /// <see cref="Workflow.CancellationToken"/>. Upon cancel, awaiting the resulting task will
        /// throw a <see cref="TaskCanceledException"/> exception.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the result of the asynchronous operation. The task is
        /// true if the wait succeeded and false if it timed out. This task is canceled if the
        /// cancellation token is.
        /// </returns>
        public Task<bool> WaitAsync(
            TimeSpan timeout,
            CancellationToken? cancellationToken = null) =>
            WaitInternalAsync(timeout, cancellationToken);

        /// <summary>
        /// Release a permit for use by another waiter.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this release call would extend the <see cref="CurrentCount"/> beyond
        /// <see cref="Max"/> which means more <c>Release</c> calls were made than successful
        /// <c>WaitAsync</c> calls.
        /// </exception>
        public void Release()
        {
            if (CurrentCount >= Max)
            {
                throw new InvalidOperationException("More released than successfully waited");
            }
            CurrentCount++;
        }

        private Task<bool> WaitInternalAsync(
            TimeSpan? timeout = null,
            CancellationToken? cancellationToken = null)
        {
            // Infinite means no timeout
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                timeout = null;
            }
            else if ((timeout ?? TimeSpan.Zero) < TimeSpan.Zero)
            {
                throw new ArgumentException("Timeout cannot be less than zero (except -1ms for infinite)");
            }

            // Attempt non-blocking acquire. Since we don't yield within here, we don't have to
            // lock. We accept that this is an unfair semaphore in that non-blocking wait requests
            // could always win.
            if (CurrentCount > 0)
            {
                CurrentCount--;
                return Task.FromResult(true);
            }

            // If there is 0 timeout and no cancellation, this is non-blocking
            if (timeout == TimeSpan.Zero && cancellationToken == null)
            {
                return Task.FromResult(false);
            }

            // Now we know we need to wait, so add ourselves as waiter and wait until it's our
            // waiter's turn.
            var me = waiters.AddLast(SemaphoreUnit);

            // The timeout overload keeps its existing timer summary, and the options form of the
            // non-timeout overload gives us a Task<bool> so both branches can share a continuation
            var task = timeout is { } timeoutNonNull ?
                Workflow.WaitConditionAsync(
                    () => CurrentCount > 0 && waiters.Count > 0 && waiters.First == me,
                    timeoutNonNull,
                    cancellationToken) :
                Workflow.WaitConditionWithOptionsAsync(new(
                    () => CurrentCount > 0 && waiters.Count > 0 && waiters.First == me,
                    cancellationToken: cancellationToken));

            // This must run synchronously so the permit is taken before any other workflow code
            // can observe the count. It runs regardless of outcome and hands back the original
            // task, because an only-on-success continuation would turn a faulted wait into a
            // cancellation and lose the exception.
            return task.
                ContinueWith(
                    task =>
                    {
                        waiters.Remove(me);
#pragma warning disable CA1849, VSTHRD103 // We know it's completed
                        if (task.Status == TaskStatus.RanToCompletion && task.Result)
#pragma warning restore CA1849, VSTHRD103
                        {
                            CurrentCount--;
                        }
#pragma warning disable VSTHRD003 // This is safe to reuse tasks for our case
                        return task;
#pragma warning restore VSTHRD003
                    },
                    default,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Current).
                Unwrap();
        }
    }
}