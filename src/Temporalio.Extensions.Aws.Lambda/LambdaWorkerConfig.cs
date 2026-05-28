using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.Worker.Tuning;

namespace Temporalio.Extensions.Aws.Lambda
{
    /// <summary>
    /// Configuration for <see cref="TemporalLambdaWorker.CreateHandler(Temporalio.Common.WorkerDeploymentVersion, Action{LambdaWorkerConfig})" />.
    /// </summary>
    public class LambdaWorkerConfig
    {
        /// <summary>
        /// Default time reserved after the worker run budget for worker shutdown and hooks.
        /// </summary>
        public static readonly TimeSpan DefaultShutdownDeadlineBuffer = TimeSpan.FromSeconds(7);

        /// <summary>
        /// Initializes a new instance of the <see cref="LambdaWorkerConfig"/> class.
        /// </summary>
        public LambdaWorkerConfig()
        {
            WorkerOptions.MaxConcurrentActivities = 2;
            WorkerOptions.MaxConcurrentWorkflowTasks = 10;
            WorkerOptions.MaxConcurrentLocalActivities = 2;
            WorkerOptions.MaxConcurrentNexusTasks = 5;
            WorkerOptions.GracefulShutdownTimeout = TimeSpan.FromSeconds(5);
            WorkerOptions.MaxCachedWorkflows = 30;
            WorkerOptions.WorkflowTaskPollerBehavior = new PollerBehavior.SimpleMaximum(2);
            WorkerOptions.ActivityTaskPollerBehavior = new PollerBehavior.SimpleMaximum(1);
            WorkerOptions.NexusTaskPollerBehavior = new PollerBehavior.SimpleMaximum(1);
            WorkerOptions.DisableEagerActivityExecution = true;
        }

        /// <summary>
        /// Gets or sets the client connection options.
        /// </summary>
        public TemporalClientConnectOptions ClientOptions { get; set; } = new TemporalClientConnectOptions();

        /// <summary>
        /// Gets or sets the worker options.
        /// </summary>
        public TemporalWorkerOptions WorkerOptions { get; set; } = new TemporalWorkerOptions();

        /// <summary>
        /// Gets or sets the deadline buffer reserved for worker shutdown and hooks.
        /// </summary>
        public TimeSpan ShutdownDeadlineBuffer { get; set; } = DefaultShutdownDeadlineBuffer;

        /// <summary>
        /// Gets or sets hooks to run after each invocation's worker has shut down.
        /// </summary>
#pragma warning disable CA2227 // The public API intentionally allows replacing the list during configuration.
        public IList<Func<CancellationToken, Task>> ShutdownHooks { get; set; } =
            new List<Func<CancellationToken, Task>>();
#pragma warning restore CA2227
    }
}
