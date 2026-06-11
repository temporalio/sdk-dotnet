#pragma warning disable CS0618 // This package forces deployment options and clears legacy versioning fields.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;

namespace Temporalio.Extensions.Aws.Lambda
{
    /// <summary>
    /// Helpers for running a Temporal worker inside an AWS Lambda invocation.
    /// </summary>
    public static class TemporalLambdaWorker
    {
        private const string ConfigFileEnvironmentVariable = "TEMPORAL_CONFIG_FILE";
        private const string DefaultConfigFileName = "temporal.toml";
        private const string LambdaTaskRootEnvironmentVariable = "LAMBDA_TASK_ROOT";
        private const string TaskQueueEnvironmentVariable = "TEMPORAL_TASK_QUEUE";
        private static readonly TimeSpan LowWorkBudgetWarningThreshold = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Create an AWS Lambda handler that runs a Temporal worker for each invocation.
        /// </summary>
        /// <param name="version">Worker deployment version for this Lambda worker.</param>
        /// <param name="configure">Callback to configure client and worker options.</param>
        /// <returns>A Lambda handler delegate.</returns>
        public static Func<object?, ILambdaContext, Task> CreateHandler(
            WorkerDeploymentVersion version,
            Action<LambdaWorkerConfig> configure) =>
            CreateHandler(
                version,
                configure,
                new TemporalLambdaWorkerHandlerOptions
                {
                    LoadClientConnectOptions = options => LoadClientConnectOptions(options),
                });

        /// <summary>
        /// Load Temporal client connection options using AWS Lambda-aware config file resolution.
        /// </summary>
        /// <param name="options">Options for loading the configuration profile.</param>
        /// <returns>Client connection options.</returns>
        public static TemporalClientConnectOptions LoadClientConnectOptions(
            ClientEnvConfig.ProfileLoadOptions? options = null)
        {
            var loadOptions = options == null ?
                new ClientEnvConfig.ProfileLoadOptions() :
                (ClientEnvConfig.ProfileLoadOptions)options.Clone();
            if (loadOptions.ConfigSource == null &&
                !loadOptions.DisableFile &&
                string.IsNullOrEmpty(GetEnvironmentVariable(
                    loadOptions,
                    ConfigFileEnvironmentVariable)))
            {
                var lambdaTaskRoot = GetEnvironmentVariable(
                    loadOptions,
                    LambdaTaskRootEnvironmentVariable);
                var root = string.IsNullOrEmpty(lambdaTaskRoot) ? "." : lambdaTaskRoot;
                loadOptions.ConfigSource = DataSource.FromPath(
                    Path.Combine(root, DefaultConfigFileName));
            }

            return ClientEnvConfig.LoadClientConnectOptions(loadOptions);
        }

        /// <summary>
        /// Create an AWS Lambda handler with overridable internals for tests.
        /// </summary>
        /// <param name="version">Worker deployment version for this Lambda worker.</param>
        /// <param name="configure">Callback to configure client and worker options.</param>
        /// <param name="handlerOptions">Internal handler options.</param>
        /// <returns>A Lambda handler delegate.</returns>
        internal static Func<object?, ILambdaContext, Task> CreateHandler(
            WorkerDeploymentVersion version,
            Action<LambdaWorkerConfig> configure,
            TemporalLambdaWorkerHandlerOptions handlerOptions)
        {
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }
            if (handlerOptions == null)
            {
                throw new ArgumentNullException(nameof(handlerOptions));
            }
            if (string.IsNullOrWhiteSpace(version.DeploymentName))
            {
                throw new ArgumentException("Deployment name must be set", nameof(version));
            }
            if (string.IsNullOrWhiteSpace(version.BuildId))
            {
                throw new ArgumentException("Build ID must be set", nameof(version));
            }

            var loadClientConnectOptions = handlerOptions.LoadClientConnectOptions;
            var config = new LambdaWorkerConfig(
                loadClientConnectOptions == null ?
                    null :
                    () => loadClientConnectOptions(null));
            var environmentTaskQueue = handlerOptions.GetEnvironmentVariable(TaskQueueEnvironmentVariable);
            if (environmentTaskQueue != null)
            {
                config.WorkerOptions.TaskQueue = environmentTaskQueue;
            }
            ApplyDeploymentVersion(config.WorkerOptions, version);

            configure(config);
            var state = PrepareHandlerState(version, config, handlerOptions);
            return state.HandleAsync;
        }

        private static LambdaWorkerHandlerState PrepareHandlerState(
            WorkerDeploymentVersion version,
            LambdaWorkerConfig config,
            TemporalLambdaWorkerHandlerOptions handlerOptions)
        {
            if (config.ClientOptions == null)
            {
                throw new InvalidOperationException("ClientOptions must be set");
            }
            if (config.WorkerOptions == null)
            {
                throw new InvalidOperationException("WorkerOptions must be set");
            }
            if (config.ShutdownHooks == null)
            {
                throw new InvalidOperationException("ShutdownHooks must be set");
            }
            if (config.ShutdownDeadlineBuffer < TimeSpan.Zero)
            {
                throw new InvalidOperationException("ShutdownDeadlineBuffer cannot be negative");
            }
            if (string.IsNullOrWhiteSpace(config.WorkerOptions.TaskQueue))
            {
                throw new InvalidOperationException(
                    "WorkerOptions.TaskQueue must be set or TEMPORAL_TASK_QUEUE must be present");
            }

            var postPluginConfiguration = config.WorkerOptions.PostPluginConfiguration;
            config.WorkerOptions.PostPluginConfiguration = options =>
            {
                postPluginConfiguration?.Invoke(options);
                ApplyDeploymentVersion(options, version);
                ClearConcurrencyLimitsIfTunerSet(options);
            };
            config.WorkerOptions.ApplyPostPluginConfiguration();

            foreach (var hook in config.ShutdownHooks)
            {
                if (hook == null)
                {
                    throw new InvalidOperationException("ShutdownHooks cannot contain null entries");
                }
            }

            return new LambdaWorkerHandlerState(
                (TemporalClientConnectOptions)config.ClientOptions.Clone(),
                (TemporalWorkerOptions)config.WorkerOptions.Clone(),
                config.ShutdownDeadlineBuffer,
                new List<Func<CancellationToken, Task>>(config.ShutdownHooks),
                handlerOptions);
        }

        private static void ApplyDeploymentVersion(
            TemporalWorkerOptions workerOptions,
            WorkerDeploymentVersion version)
        {
            var defaultVersioningBehavior =
                workerOptions.DeploymentOptions?.DefaultVersioningBehavior is { } behavior &&
                behavior != VersioningBehavior.Unspecified ?
                    behavior :
                    VersioningBehavior.AutoUpgrade;
            workerOptions.DeploymentOptions = new WorkerDeploymentOptions(
                version,
                useWorkerVersioning: true)
            {
                DefaultVersioningBehavior = defaultVersioningBehavior,
            };
            workerOptions.BuildId = null;
            workerOptions.UseWorkerVersioning = false;
        }

        private static void ClearConcurrencyLimitsIfTunerSet(TemporalWorkerOptions workerOptions)
        {
            if (workerOptions.Tuner == null)
            {
                return;
            }

            workerOptions.MaxConcurrentActivities = null;
            workerOptions.MaxConcurrentWorkflowTasks = null;
            workerOptions.MaxConcurrentLocalActivities = null;
            workerOptions.MaxConcurrentNexusTasks = null;
        }

        private static CancellationTokenSource CreateHookCancellationTokenSource(
            TimeSpan remainingTime)
        {
            if (remainingTime <= TimeSpan.Zero)
            {
                var cts = new CancellationTokenSource();
                cts.Cancel();
                return cts;
            }
            return new CancellationTokenSource(remainingTime);
        }

        private static string? GetEnvironmentVariable(
            ClientEnvConfig.ProfileLoadOptions options,
            string name)
        {
            if (options.OverrideEnvVars != null)
            {
                return options.OverrideEnvVars.TryGetValue(name, out var value) ? value : null;
            }

            return Environment.GetEnvironmentVariable(name);
        }

        private static void LogLine(ILambdaContext context, string message) =>
            context.Logger?.LogLine(message);

        private sealed class LambdaWorkerHandlerState
        {
            private readonly TemporalClientConnectOptions clientOptions;
            private readonly TemporalWorkerOptions workerOptions;
            private readonly TimeSpan shutdownDeadlineBuffer;
            private readonly IReadOnlyCollection<Func<CancellationToken, Task>> shutdownHooks;
            private readonly TemporalLambdaWorkerHandlerOptions handlerOptions;

            public LambdaWorkerHandlerState(
                TemporalClientConnectOptions clientOptions,
                TemporalWorkerOptions workerOptions,
                TimeSpan shutdownDeadlineBuffer,
                IReadOnlyCollection<Func<CancellationToken, Task>> shutdownHooks,
                TemporalLambdaWorkerHandlerOptions handlerOptions)
            {
                this.clientOptions = clientOptions;
                this.workerOptions = workerOptions;
                this.shutdownDeadlineBuffer = shutdownDeadlineBuffer;
                this.shutdownHooks = shutdownHooks;
                this.handlerOptions = handlerOptions;
            }

            public async Task HandleAsync(object? input, ILambdaContext context)
            {
                _ = input;
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                var initialWorkBudget = context.RemainingTime - shutdownDeadlineBuffer;
                if (initialWorkBudget <= TimeSpan.Zero)
                {
                    throw new InvalidOperationException(
                        "Lambda remaining time is too low to start a Temporal worker");
                }
                if (initialWorkBudget < LowWorkBudgetWarningThreshold)
                {
                    LogLine(
                        context,
                        $"WARNING: Temporal Lambda worker budget is only {initialWorkBudget.TotalSeconds:F3} seconds");
                }

                try
                {
                    var invocationClientOptions =
                        (TemporalClientConnectOptions)clientOptions.Clone();
                    if (invocationClientOptions.Identity == null)
                    {
                        invocationClientOptions.Identity =
                            $"{context.AwsRequestId}@{context.InvokedFunctionArn}";
                    }

                    var invocationWorkerOptions =
                        (TemporalWorkerOptions)workerOptions.Clone();
                    var client = await handlerOptions.ConnectClientAsync(
                        invocationClientOptions).ConfigureAwait(false);
                    using (var worker = handlerOptions.CreateWorker(
                        client,
                        invocationWorkerOptions))
                    {
                        var workBudget = context.RemainingTime - shutdownDeadlineBuffer;
                        if (workBudget <= TimeSpan.Zero)
                        {
                            return;
                        }

                        using (var runCts = new CancellationTokenSource(workBudget))
                        {
                            await ExecuteWorkerAsync(worker, runCts).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    await RunShutdownHooksAsync(context).ConfigureAwait(false);
                }
            }

            private static async Task ExecuteWorkerAsync(
                ILambdaWorker worker,
                CancellationTokenSource runCts)
            {
                try
                {
                    await worker.ExecuteAsync(runCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (runCts.IsCancellationRequested)
                {
                    // Expected path when the Lambda worker reaches its run budget.
                }
            }

            private async Task RunShutdownHooksAsync(ILambdaContext context)
            {
                using (var hookCts = CreateHookCancellationTokenSource(context.RemainingTime))
                {
                    foreach (var hook in shutdownHooks)
                    {
#pragma warning disable CA1031 // All hook failures are logged and later hooks still run.
                        try
                        {
                            await hook(hookCts.Token).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            LogLine(
                                context,
                                $"ERROR: Temporal Lambda worker shutdown hook failed: {e}");
                        }
#pragma warning restore CA1031
                    }
                }
            }
        }
    }
}
