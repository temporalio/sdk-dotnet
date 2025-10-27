using System;
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
        /// <param name="worker">The worker to run.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RunWorkerAsync(TemporalWorker worker, Func<TemporalWorker, Task> continuation);

        /// <summary>
        /// Configures the replayer options.
        /// </summary>
        /// <param name="options">The replayer options to configure.</param>
        void ConfigureReplayer(WorkflowReplayerOptions options);

        /// <summary>
        /// Runs the replayer asynchronously.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="replayer">The replayer to run.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        T ReplayWorkflows<T>(WorkflowReplayer replayer, Func<WorkflowReplayer, T> continuation);
    }
}