using System;
using System.Collections.Generic;
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
        public string Name { get; }

        /// <summary>
        /// Configures the worker options.
        /// </summary>
        /// <param name="options">The worker options to configure.</param>
        public void ConfigureWorker(TemporalWorkerOptions options);

        /// <summary>
        /// Runs the worker asynchronously.
        /// </summary>
        /// <param name="worker">The worker to run.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task RunWorkerAsync(TemporalWorker worker, Func<TemporalWorker, Task> continuation);

        /// <summary>
        /// Configures the replayer options.
        /// </summary>
        /// <param name="options">The replayer options to configure.</param>
        public void ConfigureReplayer(WorkflowReplayerOptions options);

        /// <summary>
        /// Runs the replayer asynchronously.
        /// </summary>
        /// <param name="replayer">The replayer to run.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task<IEnumerable<WorkflowReplayResult>> RunReplayerAsync(WorkflowReplayer replayer, Func<WorkflowReplayer, Task<IEnumerable<WorkflowReplayResult>>> continuation);
    }
}