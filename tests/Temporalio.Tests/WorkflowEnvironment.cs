namespace Temporalio.Tests;

using System;
using System.Diagnostics;
using Xunit;

public class WorkflowEnvironment : IAsyncLifetime
{
    private readonly Lazy<KitchenSinkWorker> kitchenSinkWorker;
    private Testing.WorkflowEnvironment? env;

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
        env = await Testing.WorkflowEnvironment.StartLocalAsync();
    }

    public async Task DisposeAsync()
    {
        if (kitchenSinkWorker.IsValueCreated)
        {
            kitchenSinkWorker.Value.Process.Kill();
            Assert.True(kitchenSinkWorker.Value.Process.WaitForExit(5000));
        }
        if (env != null)
        {
            await env.ShutdownAsync();
        }
    }

    private KitchenSinkWorker StartKitchenSinkWorker()
    {
        // Build
        var workerDir = Path.Join(TestUtils.CallerFilePath(), "../../golangworker");
        var exePath = Path.Join(workerDir, "golangworker");
        var proc = Process.Start(new ProcessStartInfo("go")
        {
            ArgumentList = { "build", "-o", exePath, "." },
            WorkingDirectory = workerDir,
        });
        proc!.WaitForExit();
        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException("Go build failed");
        }

        // Start
        var taskQueue = Guid.NewGuid().ToString();
        proc = Process.Start(
            exePath,
            new string[]
            {
                Client.Connection.Options.TargetHost!,
                Client.Options.Namespace,
                taskQueue,
            });
        return new(proc, taskQueue);
    }

    private record KitchenSinkWorker(Process Process, string TaskQueue);
}
