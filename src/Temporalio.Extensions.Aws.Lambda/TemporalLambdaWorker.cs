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
    /// <remarks>WARNING: AWS Lambda support is experimental.</remarks>
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
        /// <param name="configure">Callback to configure client and worker options per invocation.</param>
        /// <returns>A Lambda handler delegate.</returns>
        public static Func<object?, ILambdaContext, Task> CreateHandler(
            WorkerDeploymentVersion version,
            Action<TemporalLambdaWorkerOptions> configure) =>
            CreateHandler(
                version,
                configure,
                new TemporalLambdaWorkerHandlerOptions
                {
                    LoadClientConnectOptions = options => LoadClientConnectOptions(options),
                });

        /// <summary>
        /// Create an AWS Lambda handler that runs a Temporal worker for each invocation.
        /// </summary>
        /// <param name="version">Worker deployment version for this Lambda worker.</param>
        /// <param name="configureAsync">Callback to configure client and worker options per invocation.</param>
        /// <returns>A Lambda handler delegate.</returns>
        public static Func<object?, ILambdaContext, Task> CreateHandler(
            WorkerDeploymentVersion version,
            Func<TemporalLambdaWorkerOptions, Task> configureAsync) =>
            CreateHandler(
                version,
                configureAsync,
                new TemporalLambdaWorkerHandlerOptions
                {
                    LoadClientConnectOptions = options => LoadClientConnectOptions(options),
                });

        /// <summary>
        /// Load Temporal client connection options using AWS Lambda-aware config file resolution.
        /// </summary>
        /// <param name="options">Options for loading the configuration profile.</param>
        /// <returns>Client connection options.</returns>
        internal static TemporalClientConnectOptions LoadClientConnectOptions(
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
        /// <param name="configure">Callback to configure client and worker options per invocation.</param>
        /// <param name="handlerOptions">Internal handler options.</param>
        /// <returns>A Lambda handler delegate.</returns>
        internal static Func<object?, ILambdaContext, Task> CreateHandler(
            WorkerDeploymentVersion version,
            Action<TemporalLambdaWorkerOptions> configure,
            TemporalLambdaWorkerHandlerOptions handlerOptions)
        {
            ValidateCreateHandlerArgs(version, configure, nameof(configure), handlerOptions);
            var state = new ConfiguringLambdaWorkerHandlerState(
                version,
                options =>
                {
                    configure(options);
                    return Task.CompletedTask;
                },
                handlerOptions);
            return state.HandleAsync;
        }

        /// <summary>
        /// Create an AWS Lambda handler with overridable internals for tests.
        /// </summary>
        /// <param name="version">Worker deployment version for this Lambda worker.</param>
        /// <param name="configureAsync">Callback to configure client and worker options per invocation.</param>
        /// <param name="handlerOptions">Internal handler options.</param>
        /// <returns>A Lambda handler delegate.</returns>
        internal static Func<object?, ILambdaContext, Task> CreateHandler(
            WorkerDeploymentVersion version,
            Func<TemporalLambdaWorkerOptions, Task> configureAsync,
            TemporalLambdaWorkerHandlerOptions handlerOptions)
        {
            ValidateCreateHandlerArgs(version, configureAsync, nameof(configureAsync), handlerOptions);
            var state = new ConfiguringLambdaWorkerHandlerState(version, configureAsync, handlerOptions);
            return state.HandleAsync;
        }

        private static void ValidateCreateHandlerArgs(
            WorkerDeploymentVersion version,
            // Keep an explicit parameter name until this project can require C# 10's CallerArgumentExpression.
            object configure,
            string configureParamName,
            TemporalLambdaWorkerHandlerOptions handlerOptions)
        {
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(configureParamName);
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
        }

        private static TemporalLambdaWorkerOptions CreateOptions(
            WorkerDeploymentVersion version,
            TemporalLambdaWorkerHandlerOptions handlerOptions)
        {
            var loadClientConnectOptions = handlerOptions.LoadClientConnectOptions;
            var options = new TemporalLambdaWorkerOptions(
                loadClientConnectOptions == null ?
                    null :
                    () => loadClientConnectOptions(null));
            var environmentTaskQueue = handlerOptions.GetEnvironmentVariable(TaskQueueEnvironmentVariable);
            if (environmentTaskQueue != null)
            {
                options.WorkerOptions.TaskQueue = environmentTaskQueue;
            }
            ApplyDeploymentVersion(options.WorkerOptions, version);

            return options;
        }

        private static LambdaWorkerHandlerState PrepareHandlerState(
            WorkerDeploymentVersion version,
            TemporalLambdaWorkerOptions options,
            TemporalLambdaWorkerHandlerOptions handlerOptions)
        {
            if (options.ClientOptions == null)
            {
                throw new InvalidOperationException("ClientOptions must be set");
            }
            if (options.WorkerOptions == null)
            {
                throw new InvalidOperationException("WorkerOptions must be set");
            }
            if (options.ShutdownDeadlineBuffer < TimeSpan.Zero)
            {
                throw new InvalidOperationException("ShutdownDeadlineBuffer cannot be negative");
            }
            if (string.IsNullOrWhiteSpace(options.WorkerOptions.TaskQueue))
            {
                throw new InvalidOperationException(
                    "WorkerOptions.TaskQueue must be set or TEMPORAL_TASK_QUEUE must be present");
            }

            AppendWorkerPlugin(
                options.WorkerOptions,
                new InternalWorkerPlugin(
                    "Temporalio.Extensions.Aws.Lambda",
                    workerOptions =>
                    {
                        ApplyDeploymentVersion(workerOptions, version);
                        ClearConcurrencyLimitsIfTunerSet(workerOptions);
                    }));

            return new LambdaWorkerHandlerState(
                (TemporalClientConnectOptions)options.ClientOptions.Clone(),
                (TemporalWorkerOptions)options.WorkerOptions.Clone(),
                options.ShutdownDeadlineBuffer,
                new List<Func<CancellationToken, Task>>(options.ShutdownHooks),
                handlerOptions);
        }

        private static void AppendWorkerPlugin(
            TemporalWorkerOptions workerOptions,
            ITemporalWorkerPlugin plugin)
        {
            var plugins = new List<ITemporalWorkerPlugin>();
            if (workerOptions.Plugins != null)
            {
                plugins.AddRange(workerOptions.Plugins);
            }
            plugins.Add(plugin);
            workerOptions.Plugins = plugins;
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

        private sealed class ConfiguringLambdaWorkerHandlerState
        {
            private readonly WorkerDeploymentVersion version;
            private readonly Func<TemporalLambdaWorkerOptions, Task> configureAsync;
            private readonly TemporalLambdaWorkerHandlerOptions handlerOptions;

            public ConfiguringLambdaWorkerHandlerState(
                WorkerDeploymentVersion version,
                Func<TemporalLambdaWorkerOptions, Task> configureAsync,
                TemporalLambdaWorkerHandlerOptions handlerOptions)
            {
                this.version = version;
                this.configureAsync = configureAsync;
                this.handlerOptions = handlerOptions;
            }

            public async Task HandleAsync(object? input, ILambdaContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                var options = CreateOptions(version, handlerOptions);
                await configureAsync(options).ConfigureAwait(false);
                var state = PrepareHandlerState(version, options, handlerOptions);
                await state.HandleAsync(input, context).ConfigureAwait(false);
            }
        }
    }
}
