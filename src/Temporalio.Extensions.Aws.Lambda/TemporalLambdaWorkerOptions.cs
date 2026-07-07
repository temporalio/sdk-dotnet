using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Worker;

namespace Temporalio.Extensions.Aws.Lambda
{
    /// <summary>
    /// Options for <see cref="TemporalLambdaWorker.CreateHandler(Temporalio.Common.WorkerDeploymentVersion, Action{TemporalLambdaWorkerOptions})" />.
    /// </summary>
    public class TemporalLambdaWorkerOptions
    {
        private static readonly TimeSpan DefaultShutdownDeadlineBuffer = TimeSpan.FromSeconds(7);

        private readonly List<Func<CancellationToken, Task>> shutdownHooks = new();
        private Func<TemporalClientConnectOptions>? loadClientOptions;
        private TemporalClientConnectOptions? clientOptions;
        private bool clientOptionsSet;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalLambdaWorkerOptions"/> class.
        /// </summary>
        public TemporalLambdaWorkerOptions()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalLambdaWorkerOptions"/> class.
        /// </summary>
        /// <param name="loadClientOptions">Lazy client options loader.</param>
        internal TemporalLambdaWorkerOptions(Func<TemporalClientConnectOptions>? loadClientOptions)
        {
            this.loadClientOptions = loadClientOptions;
            WorkerOptions.MaxConcurrentActivities = 2;
            WorkerOptions.MaxConcurrentWorkflowTasks = 10;
            WorkerOptions.MaxConcurrentLocalActivities = 2;
            WorkerOptions.MaxConcurrentNexusTasks = 5;
            WorkerOptions.GracefulShutdownTimeout = TimeSpan.FromSeconds(5);
            WorkerOptions.MaxCachedWorkflows = 30;
            WorkerOptions.MaxConcurrentWorkflowTaskPolls = 2;
            WorkerOptions.MaxConcurrentActivityTaskPolls = 1;
            WorkerOptions.MaxConcurrentNexusTaskPolls = 1;
            WorkerOptions.DisableEagerActivityExecution = true;
        }

        /// <summary>
        /// Gets or sets the client connection options.
        /// </summary>
        public TemporalClientConnectOptions ClientOptions
        {
            get
            {
                if (!clientOptionsSet)
                {
                    clientOptions = loadClientOptions == null ?
                        new TemporalClientConnectOptions() :
                        loadClientOptions();
                    loadClientOptions = null;
                    clientOptionsSet = true;
                }
                return clientOptions!;
            }

            set
            {
                clientOptions = value;
                loadClientOptions = null;
                clientOptionsSet = true;
            }
        }

        /// <summary>
        /// Gets or sets the worker options.
        /// </summary>
        public TemporalWorkerOptions WorkerOptions { get; set; } = new TemporalWorkerOptions();

        /// <summary>
        /// Gets or sets the deadline buffer reserved for worker shutdown and hooks.
        /// </summary>
        public TimeSpan ShutdownDeadlineBuffer { get; set; } = DefaultShutdownDeadlineBuffer;

        /// <summary>
        /// Gets hooks to run after each invocation's worker has shut down.
        /// </summary>
        internal IReadOnlyList<Func<CancellationToken, Task>> ShutdownHooks => shutdownHooks;

        /// <summary>
        /// Adds a hook to run after each invocation's worker has shut down.
        /// </summary>
        /// <param name="hook">Hook to run.</param>
        public void AddShutdownHook(Func<CancellationToken, Task> hook)
        {
            if (hook == null)
            {
                throw new ArgumentNullException(nameof(hook));
            }

            shutdownHooks.Add(hook);
        }
    }
}
