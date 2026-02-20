#pragma warning disable CA1001 // IAsyncLifetime is substitute for IAsyncDisposable here

namespace Temporalio.Tests;

using System;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Worker;
using Xunit;

public class WorkflowEnvironment : IAsyncLifetime
{
    public const int ContinueAsNewSuggestedHistoryCount = 50;

    private readonly Lazy<KitchenSinkWorker> kitchenSinkWorker;
    private Temporalio.Testing.WorkflowEnvironment? env;

    public WorkflowEnvironment()
    {
        kitchenSinkWorker = new(StartKitchenSinkWorker);
    }

    public ITemporalClient Client =>
        env?.Client ?? throw new InvalidOperationException("Environment not created");

    public Temporalio.Testing.WorkflowEnvironment TestEnv =>
        env ?? throw new InvalidOperationException("Environment not created");

    public string KitchenSinkWorkerTaskQueue => kitchenSinkWorker.Value.TaskQueue;

    public async Task InitializeAsync()
    {
        // If an existing target is given via environment variable, use that
        if (Environment.GetEnvironmentVariable("TEMPORAL_TEST_CLIENT_TARGET_HOST") is string host)
        {
            var options = new TemporalClientConnectOptions(host)
            {
                Namespace = Environment.GetEnvironmentVariable("TEMPORAL_TEST_CLIENT_NAMESPACE") ??
                    throw new InvalidOperationException("Missing test namespace. Set TEMPORAL_TEST_CLIENT_NAMESPACE"),
            };
            var clientCert = Environment.GetEnvironmentVariable("TEMPORAL_TEST_CLIENT_CERT");
            var clientKey = Environment.GetEnvironmentVariable("TEMPORAL_TEST_CLIENT_KEY");
            var apiKey = Environment.GetEnvironmentVariable("TEMPORAL_CLIENT_CLOUD_API_KEY");
            if (clientCert == null != (clientKey == null))
            {
                throw new InvalidOperationException("Must have both cert/key or neither");
            }
            if (clientCert != null && clientKey != null)
            {
                options.Tls = new()
                {
                    ClientCert = System.Text.Encoding.ASCII.GetBytes(clientCert),
                    ClientPrivateKey = System.Text.Encoding.ASCII.GetBytes(clientKey),
                };
            }
            if (apiKey != null)
            {
                // API key auto-enables TLS when Tls is not set
                options.ApiKey = apiKey;
            }
            env = new(await TemporalClient.ConnectAsync(options));
        }
        else
        {
            // Otherwise, local server is good
            env = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync(new()
            {
                DevServerOptions = new()
                {
                    DownloadVersion = "v1.6.1-server-1.31.0-151.0",
                    ExtraArgs = new List<string>
                    {
                        // Disable search attribute cache
                        "--dynamic-config-value",
                        "system.forceSearchAttributesCacheRefreshOnRead=true",
                        // Enable versioning
                        "--dynamic-config-value",
                        "frontend.workerVersioningDataAPIs=true",
                        "--dynamic-config-value",
                        "frontend.workerVersioningWorkflowAPIs=true",
                        "--dynamic-config-value",
                        "worker.buildIdScavengerEnabled=true",
                        "--dynamic-config-value",
                        $"limit.historyCount.suggestContinueAsNew={ContinueAsNewSuggestedHistoryCount}",
                        // Enable multi-op
                        "--dynamic-config-value",
                        "frontend.enableExecuteMultiOperation=true",
                        "--dynamic-config-value",
                        "system.enableDeploymentVersions=true",
                        // Enable standalone activities
                        "--dynamic-config-value", "frontend.activityAPIsEnabled=true",
                        "--dynamic-config-value", "activity.enableStandalone=true",
                        "--dynamic-config-value", "history.enableChasm=true",
                        "--dynamic-config-value", "history.enableTransitionHistory=true",
                        // Enable Nexus cancellation types
                        "--dynamic-config-value",
                        "component.nexusoperations.recordCancelRequestCompletionEvents=true",
                    },
                },
            });
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (kitchenSinkWorker.IsValueCreated)
            {
                kitchenSinkWorker.Value.WorkerRunCompletion.SetResult();
                await kitchenSinkWorker.Value.WorkerRunTask;
            }
        }
        finally
        {
            if (kitchenSinkWorker.IsValueCreated)
            {
                kitchenSinkWorker.Value.Worker.Dispose();
            }
            if (env != null)
            {
                await env.ShutdownAsync();
            }
        }
    }

    private KitchenSinkWorker StartKitchenSinkWorker()
    {
        var taskQueue = Guid.NewGuid().ToString();
#pragma warning disable CA2000 // We dispose later
        var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(taskQueue).AddWorkflow<KitchenSinkWorkflow>());
#pragma warning restore CA2000
        var comp = new TaskCompletionSource();
        var task = Task.Run(async () =>
        {
            try
            {
                await worker.ExecuteAsync(() => comp.Task);
            }
#pragma warning disable CA1031 // We want to catch all
            catch (Exception e)
#pragma warning restore CA1031
            {
                Client.Options.LoggerFactory.CreateLogger<WorkflowEnvironment>().LogError(
                    e, "Workflow run failure");
                throw;
            }
        });
        return new(taskQueue, worker, task, comp);
    }

    private record KitchenSinkWorker(
        string TaskQueue,
        TemporalWorker Worker,
        Task WorkerRunTask,
        TaskCompletionSource WorkerRunCompletion);
}
