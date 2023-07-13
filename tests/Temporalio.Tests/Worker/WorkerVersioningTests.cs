#pragma warning disable SA1201, SA1204 // We want to have classes near their tests

using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

namespace Temporalio.Tests.Worker;

public class WorkerVersioningTests : WorkflowEnvironmentTestBase
{
    public WorkerVersioningTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Workflow]
    public class WaitForFinishSignal
    {
        private string? lastSignal;

        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            await Workflow.WaitConditionAsync(() => lastSignal == "finish");
            return lastSignal!;
        }

        [WorkflowSignal]
        public async Task SendSignal(string val) => lastSignal = val;

        [WorkflowQuery]
        public string LastSignal() => lastSignal ?? string.Empty;
    }

    [Fact]
    public async Task TestVersionedWorkers_Succeed()
    {
        await ExecuteWorkerAsync<WaitForFinishSignal>(
            async worker =>
            {
                var taskQueue = worker.Options.TaskQueue!;
                // Add 1.0 to queue
                await Env.Client.UpdateWorkerBuildIdCompatibilityAsync(taskQueue, new BuildIdOp.AddNewDefault("1.0"));
                // Start the workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    (WaitForFinishSignal wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: taskQueue));
                await handle.SignalAsync(wf => wf.SendSignal("hola"));
                await AssertMore.EqualEventuallyAsync(
                    "hola",
                    () => handle.QueryAsync(wf => wf.LastSignal()));

                // Add 2.0 as default
                await Env.Client.UpdateWorkerBuildIdCompatibilityAsync(taskQueue, new BuildIdOp.AddNewDefault("2.0"));
                // Start 2.0 worker
                await ExecuteWorkerAsync<WaitForFinishSignal>(
                    async _ =>
                    {
                        // Start another workflow
                        var handle2 = await Env.Client.StartWorkflowAsync(
                            (WaitForFinishSignal wf) => wf.RunAsync(),
                            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: taskQueue));
                        await handle2.SignalAsync(wf => wf.SendSignal("hola 2"));
                        await AssertMore.EqualEventuallyAsync(
                            "hola 2",
                            () => handle2.QueryAsync(wf => wf.LastSignal()));

                        // finish both
                        await handle.SignalAsync(wf => wf.SendSignal("finish"));
                        await handle2.SignalAsync(wf => wf.SendSignal("finish"));

                        // Wait for completion
                        await handle.GetResultAsync();
                        await handle2.GetResultAsync();

                        // Confirm task completion events have correct IDs
                        await ConfirmTaskCompletionBuildIDs(handle, "1.0");
                        await ConfirmTaskCompletionBuildIDs(handle2, "2.0");
                    },
                    new(taskQueue: taskQueue) { BuildID = "2.0", UseWorkerVersioning = true });
            },
            new() { BuildID = "1.0", UseWorkerVersioning = true });
    }

    private static async Task ConfirmTaskCompletionBuildIDs(WorkflowHandle handle, string expectedBuildId)
    {
        await foreach (var evt in handle.FetchHistoryEventsAsync())
        {
            var attr = evt.WorkflowTaskCompletedEventAttributes;
            if (attr != null)
            {
                Assert.Equal(expectedBuildId, attr.WorkerVersion.BuildId);
            }
        }
    }

    private async Task ExecuteWorkerAsync<TWf>(
        Func<TemporalWorker, Task> action,
        TemporalWorkerOptions? options = null,
        IWorkerClient? client = null)
    {
        options ??= new();
        options = (TemporalWorkerOptions)options.Clone();
        options.TaskQueue ??= $"tq-{Guid.NewGuid()}";
        options.AddWorkflow<TWf>();
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(client ?? Client, options);
        await worker.ExecuteAsync(() => action(worker));
    }
}