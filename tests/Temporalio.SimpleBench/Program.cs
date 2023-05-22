using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.SimpleBench;
using Temporalio.Testing;
using Temporalio.Worker;
using Temporalio.Workflows;

// Build command
var cmd = new RootCommand("Simple bench runner");
var workflowCountOption = new Option<int>("--workflow-count", "Number of workflows")
{
    IsRequired = true,
};
cmd.AddOption(workflowCountOption);
var maxCachedWorkflowsOption = new Option<int>("--max-cached-workflows", "Number of workflows cached")
{
    IsRequired = true,
};
cmd.AddOption(maxCachedWorkflowsOption);
var maxConcurrentOption = new Option<int>("--max-concurrent", "Number of concurrent workflows/activities")
{
    IsRequired = true,
};
cmd.AddOption(maxConcurrentOption);
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddSimpleConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

// Set handler
cmd.SetHandler(async ctx =>
{
    var workflowCount = ctx.ParseResult.GetValueForOption(workflowCountOption);
    var maxCachedWorkflows = ctx.ParseResult.GetValueForOption(maxCachedWorkflowsOption);
    var maxConcurrent = ctx.ParseResult.GetValueForOption(maxConcurrentOption);

    // Start server
    logger.LogInformation("Starting local environment");
    await using var env = await WorkflowEnvironment.StartLocalAsync(
        new() { LoggerFactory = loggerFactory });
    var taskQueue = $"task-queue-{Guid.NewGuid()}";

    // Create a bunch of workflows
    logger.LogInformation("Starting {WorkflowCount} workflows", workflowCount);
    var startWatch = new Stopwatch();
    startWatch.Start();
    var handles = await Task.WhenAll(Enumerable.Range(0, workflowCount).Select(index =>
        env.Client.StartWorkflowAsync(
            (BenchWorkflow wf) => wf.RunAsync(),
            $"user-{index}",
            new($"workflow-{index}-{Guid.NewGuid()}", taskQueue))));
    startWatch.Stop();

    // Start a worker to run them
    logger.LogInformation("Starting worker");
    using var worker = new TemporalWorker(
        env.Client,
        new TemporalWorkerOptions(taskQueue)
        {
            MaxCachedWorkflows = maxCachedWorkflows,
            MaxConcurrentWorkflowTasks = maxConcurrent,
            MaxConcurrentActivities = maxConcurrent,
        }.
            AddActivity(BenchActivities.BenchActivity).
            AddWorkflow(typeof(BenchWorkflow)));
    using var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(ctx.GetCancellationToken());
    var workerTask = Task.Run(() => worker.ExecuteAsync(cancelSource.Token));

    // Wait for all workflows
    var resultWatch = new Stopwatch();
    var memoryTask = Task.Run(() => MemoryTracker.TrackMaxMemoryBytesAsync(cancelSource.Token));
    resultWatch.Start();
    foreach (var handle in handles)
    {
        await handle.GetResultAsync();
    }
    resultWatch.Stop();

    // Cancel worker and wait for cancelled
    cancelSource.Cancel();
    try
    {
        await workerTask;
    }
    catch (OperationCanceledException)
    {
    }
    var maxMem = await memoryTask;

    // Dump results
    logger.LogInformation("Results: {Results}", new Results(
        WorkflowCount: workflowCount,
        MaxCachedWorkflows: maxCachedWorkflows,
        MaxConcurrent: maxConcurrent,
        MaxMemoryMib: (long)Math.Round(maxMem / Math.Pow(1024, 2)),
        StartDuration: startWatch.Elapsed,
        ResultDuration: resultWatch.Elapsed,
        WorkflowsPerSecond: Math.Round(workflowCount / (decimal)resultWatch.Elapsed.TotalSeconds, 2)));
});

// Run command
await cmd.InvokeAsync(args);

namespace Temporalio.SimpleBench
{
    public static class MemoryTracker
    {
        public static async Task<long> TrackMaxMemoryBytesAsync(CancellationToken cancel)
        {
            // Get the memory every 800ms
            var process = Process.GetCurrentProcess();
            var max = -1L;
            while (!cancel.IsCancellationRequested)
            {
                // We don't want to cancel delay ever, always let it finish
                await Task.Delay(800, CancellationToken.None);
                var curr = process.WorkingSet64;
                if (curr > max)
                {
                    max = curr;
                }
            }
            return max;
        }
    }

    public static class BenchActivities
    {
        [Activity]
        public static string BenchActivity(string name) => $"Hello, {name}";
    }

    [Workflow]
    public class BenchWorkflow
    {
        public static readonly BenchWorkflow Ref = WorkflowRefs.Create<BenchWorkflow>();

        [WorkflowRun]
        public async Task<string> RunAsync(string name)
        {
            return await Workflow.ExecuteActivityAsync(
                BenchActivities.BenchActivity, name, new() { StartToCloseTimeout = TimeSpan.FromSeconds(30) });
        }
    }

    public record Results(
        int WorkflowCount,
        int MaxCachedWorkflows,
        int MaxConcurrent,
        long MaxMemoryMib,
        TimeSpan StartDuration,
        TimeSpan ResultDuration,
        decimal WorkflowsPerSecond);
}