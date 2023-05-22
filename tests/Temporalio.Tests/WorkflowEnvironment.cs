namespace Temporalio.Tests;

using System;
using Microsoft.Extensions.Logging;
using Temporalio.Worker;
using Xunit;

public class WorkflowEnvironment : IAsyncLifetime
{
    private readonly Lazy<KitchenSinkWorker> kitchenSinkWorker;
    private Temporalio.Testing.WorkflowEnvironment? env;

    public WorkflowEnvironment()
    {
        kitchenSinkWorker = new(StartKitchenSinkWorker);
    }

    public Temporalio.Client.ITemporalClient Client =>
        env?.Client ?? throw new InvalidOperationException("Environment not created");

    public string KitchenSinkWorkerTaskQueue => kitchenSinkWorker.Value.TaskQueue;

    public async Task InitializeAsync()
    {
        // TODO(cretz): Support other environments
        env = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync(new()
        {
            Temporalite = new()
            {
                ExtraArgs = new List<string>
                {
                    // Disable search attribute cache
                    "--dynamic-config-value",
                    "system.forceSearchAttributesCacheRefreshOnRead=true",
                },
            },
        });
        // Can uncomment this and comment out the above to use an external server
        // env = new(await Temporalio.Client.TemporalClient.ConnectAsync(new("localhost:7233")));
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
