using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Worker
{
    /// <summary>
    /// Interface for temporal worker plugins.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    public interface ITemporalWorkerPlugin
    {
        /// <summary>
        /// Gets the plugin name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Configures the worker options.
        /// </summary>
        /// <param name="options">The worker options to configure.</param>
        void ConfigureWorker(TemporalWorkerOptions options);

        /// <summary>
        /// Runs the worker asynchronously.
        /// </summary>
        /// <typeparam name="TResult">Result type. For most worker run calls, this is
        /// <see cref="ValueTuple"/>.</typeparam>
        /// <param name="worker">The worker to run.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <param name="stoppingToken">Cancellation token to stop the worker.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<TResult> RunWorkerAsync<TResult>(
            TemporalWorker worker,
            Func<TemporalWorker, CancellationToken, Task<TResult>> continuation,
            CancellationToken stoppingToken);

        /// <summary>
        /// Configures the replayer options.
        /// </summary>
        /// <param name="options">The replayer options to configure.</param>
        void ConfigureReplayer(WorkflowReplayerOptions options);

        /// <summary>
        /// Runs the replayer asynchronously.
        /// </summary>
        /// <param name="replayer">The replayer to run.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <param name="cancellationToken">Cancellation token to stop the replay.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<IEnumerable<WorkflowReplayResult>> ReplayWorkflowsAsync(
            WorkflowReplayer replayer,
            Func<WorkflowReplayer, CancellationToken, Task<IEnumerable<WorkflowReplayResult>>> continuation,
            CancellationToken cancellationToken);

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Runs the replayer asynchronously.
        /// </summary>
        /// <param name="replayer">The replayer to run.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <param name="cancellationToken">Cancellation token to stop the replay.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        IAsyncEnumerable<WorkflowReplayResult> ReplayWorkflowsAsync(
            WorkflowReplayer replayer,
            Func<WorkflowReplayer, IAsyncEnumerable<WorkflowReplayResult>> continuation,
            CancellationToken cancellationToken);
#endif
    }
}