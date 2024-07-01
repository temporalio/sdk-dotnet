using System;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Mutex is a thin wrapper around <see cref="Semaphore"/> with a single count.
    /// </summary>
    public class Mutex
    {
        private readonly Semaphore semaphore = new(1);

        /// <summary>
        /// Wait for the mutex to become available. If the task succeeds, users must call
        /// <see cref="ReleaseMutex"/> to properly release the mutex when done.
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
        public Task WaitOneAsync(CancellationToken? cancellationToken = null) =>
            semaphore.WaitAsync(cancellationToken);

        /// <summary>
        /// Wait for the mutex to become available or timeout to be reached. If the task returns
        /// true, users must call <see cref="ReleaseMutex"/> to properly release the mutex when
        /// done.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// Milliseconds until timeout. If this is 0, this is a non-blocking task that will return
        /// immediately. If this value is -1 (i.e. <see cref="Timeout.Infinite"/>), there is no
        /// timeout and the result will always be true on success (at which point you might as well
        /// use <see cref="WaitOneAsync(CancellationToken?)"/>). Otherwise a timer is started for
        /// the timeout and canceled if the wait succeeds.
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
        public Task<bool> WaitOneAsync(
            int millisecondsTimeout,
            CancellationToken? cancellationToken = null) =>
            semaphore.WaitAsync(millisecondsTimeout, cancellationToken);

        /// <summary>
        /// Wait for the mutex to become available or timeout to be reached. If the task returns
        /// true, users must call <see cref="ReleaseMutex"/> to properly release the mutex when
        /// done.
        /// </summary>
        /// <param name="timeout">
        /// TimeSpan until timeout. If this is <see cref="TimeSpan.Zero"/>, this is a non-blocking
        /// task that will return immediately. If this value is -1ms (i.e.
        /// <see cref="Timeout.InfiniteTimeSpan"/>), there is no timeout and the result will always
        /// be true on success (at which point you might as well use
        /// <see cref="WaitOneAsync(CancellationToken?)"/>). Otherwise a timer is started for the
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
        public Task<bool> WaitOneAsync(
            TimeSpan timeout,
            CancellationToken? cancellationToken = null) =>
            semaphore.WaitAsync(timeout, cancellationToken);

        /// <summary>
        /// Release the mutex for use by another waiter.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the mutex was not waited on successsfully.
        /// </exception>
        public void ReleaseMutex() => semaphore.Release();
    }
}