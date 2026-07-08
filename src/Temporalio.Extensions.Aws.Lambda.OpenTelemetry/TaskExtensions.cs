using System;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Extensions.Aws.Lambda.OpenTelemetry
{
    /// <summary>
    /// Task helpers for target frameworks without built-in cancellation-aware waiting.
    /// </summary>
    internal static class TaskExtensions
    {
        /// <summary>
        /// Waits for a task to complete or for cancellation to be requested.
        /// </summary>
        /// <param name="task">Task to wait for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the input task completes.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="task" /> is null.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested before the task completes.</exception>
#pragma warning disable VSTHRD003 // This helper intentionally awaits a caller-owned task
        public static async Task WaitAsync(this Task task, CancellationToken cancellationToken)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }
            if (task.IsCompleted || !cancellationToken.CanBeCanceled)
            {
                await task.ConfigureAwait(false);
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();

            var cancellationTaskSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancellationTaskSource.TrySetResult(true)))
            {
                if (task != await Task.WhenAny(
                    task,
                    cancellationTaskSource.Task).ConfigureAwait(false))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            await task.ConfigureAwait(false);
        }
#pragma warning restore VSTHRD003

        /// <summary>
        /// Observes a task fault without awaiting it.
        /// </summary>
        /// <param name="task">Task whose fault should be observed.</param>
        /// <exception cref="ArgumentNullException"><paramref name="task" /> is null.</exception>
        public static void Forget(this Task task)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            _ = task.ContinueWith(
                completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
