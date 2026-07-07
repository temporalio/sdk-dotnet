using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Worker
{
    /// <summary>
    /// Internal pass-through plugin for extensions that need worker plugin ordering.
    /// </summary>
    internal sealed class InternalWorkerPlugin : ITemporalWorkerPlugin
    {
        private readonly Action<TemporalWorkerOptions> configureWorker;

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalWorkerPlugin"/> class.
        /// </summary>
        /// <param name="name">Plugin name reported to the worker.</param>
        /// <param name="configureWorker">Worker options configuration callback.</param>
        public InternalWorkerPlugin(string name, Action<TemporalWorkerOptions> configureWorker)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.configureWorker = configureWorker ??
                throw new ArgumentNullException(nameof(configureWorker));
        }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public void ConfigureWorker(TemporalWorkerOptions options) => configureWorker(options);

        /// <inheritdoc />
        public Task<TResult> RunWorkerAsync<TResult>(
            TemporalWorker worker,
            Func<TemporalWorker, CancellationToken, Task<TResult>> continuation,
            CancellationToken stoppingToken) =>
            continuation(worker, stoppingToken);

        /// <inheritdoc />
        public void ConfigureReplayer(WorkflowReplayerOptions options)
        {
            _ = options;
        }

        /// <inheritdoc />
        public Task<IEnumerable<WorkflowReplayResult>> ReplayWorkflowsAsync(
            WorkflowReplayer replayer,
            Func<WorkflowReplayer, CancellationToken, Task<IEnumerable<WorkflowReplayResult>>> continuation,
            CancellationToken cancellationToken) =>
            continuation(replayer, cancellationToken);

#if NETCOREAPP3_0_OR_GREATER
        /// <inheritdoc />
        public IAsyncEnumerable<WorkflowReplayResult> ReplayWorkflowsAsync(
            WorkflowReplayer replayer,
            Func<WorkflowReplayer, IAsyncEnumerable<WorkflowReplayResult>> continuation,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return continuation(replayer);
        }
#endif
    }
}
