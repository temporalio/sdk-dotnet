namespace Temporalio.Tests.Extensions.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Extensions.Hosting;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class TemporalWorkerServiceTests : WorkflowEnvironmentTestBase
{
    public TemporalWorkerServiceTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    public class DatabaseClient
    {
        private int counter = 5;

        public async Task<string> SelectSomethingAsync() => $"something-{++counter}";
    }

    public class DatabaseActivities
    {
        private readonly DatabaseClient databaseClient;

        public DatabaseActivities(DatabaseClient databaseClient) =>
            this.databaseClient = databaseClient;

        [Activity]
        public async Task<List<string>> DoThingsAsync() => new()
        {
            await databaseClient.SelectSomethingAsync(),
            await databaseClient.SelectSomethingAsync(),
        };

        [Activity]
        public static async Task<string> DoStaticThingsAsync() => "something-static";
    }

    [Workflow]
    public class DatabaseWorkflow
    {
        [WorkflowRun]
        public async Task<List<string>> RunAsync()
        {
            Workflow.Logger.LogInformation("Running database workflow");
            var list = await Workflow.ExecuteActivityAsync(
                (DatabaseActivities act) => act.DoThingsAsync(),
                new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
            list.AddRange(await Workflow.ExecuteActivityAsync(
                (DatabaseActivities act) => act.DoThingsAsync(),
                new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) }));
            list.Add(await Workflow.ExecuteActivityAsync(
                () => DatabaseActivities.DoStaticThingsAsync(),
                new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) }));
            return list;
        }
    }

    [Fact]
    public async Task TemporalWorkerService_ExecuteAsync_SimpleWorker()
    {
        using var loggerFactory = new TestUtils.LogCaptureFactory(NullLoggerFactory.Instance);
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var host = Host.CreateDefaultBuilder().ConfigureServices(services =>
        {
            // Configure a client
            services.AddTemporalClient(
                clientTargetHost: Client.Connection.Options.TargetHost,
                clientNamespace: Client.Options.Namespace);

            // Add the rest of the services
            services.
                AddSingleton<ILoggerFactory>(loggerFactory).
                AddScoped<DatabaseClient>().
                // We are also adding the DB client as a keyed service to demonstrate keyed service
                // support for our DI logic. This used to break because newer DI library versions
                // disallowed accessing certain properties on keyed services which we access
                // internally for dupe checks.
                AddKeyedScoped<DatabaseClient>("client-keyed").
                AddHostedTemporalWorker(taskQueue).
                AddScopedActivities<DatabaseActivities>().
                AddWorkflow<DatabaseWorkflow>();
        }).Build();

        // Start the host
        using var tokenSource = new CancellationTokenSource();
        await host.StartAsync(tokenSource.Token);
        try
        {
            // Execute the workflow
            var result = await Client.ExecuteWorkflowAsync(
                (DatabaseWorkflow wf) => wf.RunAsync(),
                new($"wf-{Guid.NewGuid()}", taskQueue));
            // Single activity calls use the same client but different calls use different clients
            Assert.Equal(
                new List<string> { "something-6", "something-7", "something-6", "something-7", "something-static" },
                result);
            // Confirm the log appeared
            Assert.Contains(loggerFactory.Logs, e => e.Formatted == "Running database workflow");
        }
        finally
        {
            await host.StopAsync(tokenSource.Token);
        }
    }

    public class SingletonCounter
    {
        public int Counter { get; set; } = 5;

        [Activity("singleton-increment-and-get")]
        public string IncrementAndGet() =>
            $"tq: {ActivityExecutionContext.Current.Info.TaskQueue}, counter: {++Counter}";
    }

    public class ScopedCounter
    {
        public int Counter { get; set; } = 5;

        [Activity("scoped-increment-and-get")]
        public string IncrementAndGet() =>
            $"tq: {ActivityExecutionContext.Current.Info.TaskQueue}, counter: {++Counter}";
    }

    [Workflow]
    public class MultiTaskQueueWorkflow
    {
        [WorkflowRun]
        public async Task<Dictionary<string, string>> RunAsync(string otherTaskQueue)
        {
            var opts = new ActivityOptions() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) };
            var otherOpts = new ActivityOptions()
            {
                TaskQueue = otherTaskQueue,
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
            };
            return new Dictionary<string, string>()
            {
                ["singleton1"] = await Workflow.ExecuteActivityAsync(
                    (SingletonCounter act) => act.IncrementAndGet(), opts),
                ["singleton2"] = await Workflow.ExecuteActivityAsync(
                    (SingletonCounter act) => act.IncrementAndGet(), opts),
                ["scoped1"] = await Workflow.ExecuteActivityAsync(
                    (ScopedCounter act) => act.IncrementAndGet(), opts),
                ["scoped2"] = await Workflow.ExecuteActivityAsync(
                    (ScopedCounter act) => act.IncrementAndGet(), opts),
                ["singleton-other1"] = await Workflow.ExecuteActivityAsync(
                    (SingletonCounter act) => act.IncrementAndGet(), otherOpts),
                ["singleton-other2"] = await Workflow.ExecuteActivityAsync(
                    (SingletonCounter act) => act.IncrementAndGet(), otherOpts),
                ["scoped-other1"] = await Workflow.ExecuteActivityAsync(
                    (ScopedCounter act) => act.IncrementAndGet(), otherOpts),
                ["scoped-other2"] = await Workflow.ExecuteActivityAsync(
                    (ScopedCounter act) => act.IncrementAndGet(), otherOpts),
            };
        }
    }

    [Fact]
    public async Task TemporalWorkerService_ExecuteAsync_MultipleWorkers()
    {
        var taskQueue1 = $"tq-{Guid.NewGuid()}";
        var taskQueue2 = $"tq-{Guid.NewGuid()}";

        var bld = Host.CreateApplicationBuilder();
        // Add the first worker with the workflow and client already DI'd
        bld.Services.
            AddSingleton(Client).
            AddHostedTemporalWorker(taskQueue1).
            AddSingletonActivities<SingletonCounter>().
            AddScopedActivities<ScopedCounter>().
            AddWorkflow<MultiTaskQueueWorkflow>();

        // Add another worker with the client target info and only activities
        bld.Services.
            AddHostedTemporalWorker(
                clientTargetHost: Client.Connection.Options.TargetHost!,
                clientNamespace: Client.Options.Namespace,
                taskQueue: taskQueue2).
            AddSingletonActivities<SingletonCounter>().
            AddScopedActivities<ScopedCounter>();

        // Start the host
        using var tokenSource = new CancellationTokenSource();
        using var host = bld.Build();
        await host.StartAsync(tokenSource.Token);
        try
        {
            // Execute the workflow
            var result = await Client.ExecuteWorkflowAsync(
                (MultiTaskQueueWorkflow wf) => wf.RunAsync(taskQueue2),
                new($"wf-{Guid.NewGuid()}", taskQueue1));
            Assert.Equal(
                new Dictionary<string, string>()
                {
                    // Singletons share
                    ["singleton1"] = $"tq: {taskQueue1}, counter: 6",
                    ["singleton2"] = $"tq: {taskQueue1}, counter: 7",
                    ["singleton-other1"] = $"tq: {taskQueue2}, counter: 8",
                    ["singleton-other2"] = $"tq: {taskQueue2}, counter: 9",
                    // Scoped do not share
                    ["scoped1"] = $"tq: {taskQueue1}, counter: 6",
                    ["scoped2"] = $"tq: {taskQueue1}, counter: 6",
                    ["scoped-other1"] = $"tq: {taskQueue2}, counter: 6",
                    ["scoped-other2"] = $"tq: {taskQueue2}, counter: 6",
                },
                result);
        }
        finally
        {
            await host.StopAsync(tokenSource.Token);
        }
    }

    [Workflow]
    public class TickingWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            // Just tick every 100ms for 10s
            for (var i = 0; i < 100; i++)
            {
                await Workflow.DelayAsync(100);
            }
        }
    }

    [Fact]
    public async Task TemporalWorkerService_WorkerClientReplacement_UsesNewClient()
    {
        // We are going to start a second ephemeral server and then replace the client. So we will
        // start a no-cache ticking workflow with the current client and confirm it has accomplished
        // at least one task. Then we will start another on the other client, and confirm it gets
        // started too. Then we will terminate both. We have to use a ticking workflow with only one
        // poller to force a quick re-poll to recognize our client change quickly (as opposed to
        // just waiting the minute for poll timeout).
        await using var otherEnv = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync();

        // Start both workflows on different servers
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var handle1 = await Client.StartWorkflowAsync(
            (TickingWorkflow wf) => wf.RunAsync(),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue));
        var handle2 = await otherEnv.Client.StartWorkflowAsync(
            (TickingWorkflow wf) => wf.RunAsync(),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue));

        var bld = Host.CreateApplicationBuilder();

        TemporalWorkerClientUpdater workerClientUpdater = new TemporalWorkerClientUpdater();

        // Register the worker client updater.
        bld.Services.AddSingleton<TemporalWorkerClientUpdater>(workerClientUpdater);

        // Add the first worker with the workflow and client already DI'd, and add the worker client updater.
        bld.Services.
            AddSingleton(Client).
            AddHostedTemporalWorker(taskQueue).
            AddWorkflow<TickingWorkflow>()
            .ConfigureOptions()
            .Configure<TemporalWorkerClientUpdater>((options, updater) =>
            {
                options.WorkerClientUpdater = updater;
                options.MaxCachedWorkflows = 0;
                options.MaxConcurrentWorkflowTaskPolls = 1;
            });

        // Start the host
        using var tokenSource = new CancellationTokenSource();
        using var host = bld.Build();
        await host.StartAsync(tokenSource.Token);
        try
        {
            // Confirm the first ticking workflow has completed a task but not the second workflow
            await AssertMore.HasEventEventuallyAsync(handle1, e => e.WorkflowTaskCompletedEventAttributes != null);
            await foreach (var evt in handle2.FetchHistoryEventsAsync())
            {
                Assert.Null(evt.WorkflowTaskCompletedEventAttributes);
            }

            // Now replace the client, which should be used fairly quickly because we should have
            // timer-done poll completions every 100ms
            workerClientUpdater.UpdateClient(otherEnv.Client);

            // Now confirm the other workflow has started
            await AssertMore.HasEventEventuallyAsync(handle2, e => e.WorkflowTaskCompletedEventAttributes != null);

            // Terminate both
            await handle1.TerminateAsync();
            await handle2.TerminateAsync();
        }
        finally
        {
            await host.StopAsync(tokenSource.Token);
        }
    }

    [Workflow("DeploymentVersioningWorkflow", VersioningBehavior = VersioningBehavior.Pinned)]
    public class WorkflowV1
    {
        [WorkflowRun]
        public async Task<string> RunAsync() => "done-v1";
    }

    [Workflow("DeploymentVersioningWorkflow", VersioningBehavior = VersioningBehavior.Pinned)]
    public class WorkflowV2
    {
        [WorkflowRun]
        public async Task<string> RunAsync() => "done-v2";
    }

    [SkippableFact]
    public async Task TemporalWorkerService_ExecuteAsync_MultipleVersionsSameQueue()
    {
        // This only applies to legacy versioning and therefore is ok to skip (and remove in the
        // future)
        throw new SkipException("Since 1.24 this test is slow because legacy versioning has a known slowdown");
#pragma warning disable IDE0035, CS0162 // We know the below is now dead code
        var taskQueue = $"tq-{Guid.NewGuid()}";
        // Build with two workers on same queue but different versions
        var bld = Host.CreateApplicationBuilder();
        bld.Services.AddSingleton(Client);
#pragma warning disable CS0618 // Testing obsolete APIs
        bld.Services.
            AddHostedTemporalWorker(taskQueue, "1.0").
            AddWorkflow<WorkflowV1>();
        bld.Services.
            AddHostedTemporalWorker(taskQueue, "2.0").
            AddWorkflow<WorkflowV2>();

        // Start the host
        using var tokenSource = new CancellationTokenSource();
        using var host = bld.Build();
        await host.StartAsync(tokenSource.Token);
        try
        {
            // Set 1.0 as default and run
            await Env.Client.UpdateWorkerBuildIdCompatibilityAsync(
                taskQueue, new BuildIdOp.AddNewDefault("1.0"));
            var res = await Client.ExecuteWorkflowAsync(
                (WorkflowV1 wf) => wf.RunAsync(),
                new($"wf-{Guid.NewGuid()}", taskQueue));
            Assert.Equal("done-v1", res);

            // Update default and run again
            await Env.Client.UpdateWorkerBuildIdCompatibilityAsync(
                taskQueue, new BuildIdOp.AddNewDefault("2.0"));
            res = await Client.ExecuteWorkflowAsync(
                (WorkflowV1 wf) => wf.RunAsync(),
                new($"wf-{Guid.NewGuid()}", taskQueue));
            Assert.Equal("done-v2", res);
        }
        finally
        {
            await host.StopAsync(tokenSource.Token);
        }
#pragma warning restore CS0618
    }

    [Fact]
    public async Task TemporalWorkerService_ExecuteAsync_MultipleDeploymentVersionsSameQueue()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var workerV1 = new WorkerDeploymentVersion($"deployment-{taskQueue}", "1.0");
        var workerV2 = new WorkerDeploymentVersion($"deployment-{taskQueue}", "2.0");
        // Build with two workers on same queue but different versions
        var bld = Host.CreateApplicationBuilder();
        bld.Services.AddSingleton(Client);
        bld.Services.
            AddHostedTemporalWorker(taskQueue, new WorkerDeploymentOptions(workerV1, true)).
            AddWorkflow<WorkflowV1>();
        bld.Services.
            AddHostedTemporalWorker(taskQueue, new WorkerDeploymentOptions(workerV2, true)).
            AddWorkflow<WorkflowV2>();

        // Start the host
        using var tokenSource = new CancellationTokenSource();
        using var host = bld.Build();
        await host.StartAsync(tokenSource.Token);
        try
        {
            // Set 1.0 as default and run
            var describe1 = await TestUtils.WaitUntilWorkerDeploymentVisibleAsync(Client, workerV1);
            await TestUtils.SetCurrentDeploymentVersionAsync(Client, describe1.ConflictToken, workerV1);
            var res = await Client.ExecuteWorkflowAsync(
                (WorkflowV1 wf) => wf.RunAsync(),
                new($"wf-{Guid.NewGuid()}", taskQueue));
            Assert.Equal("done-v1", res);

            // Update default and run again
            var describe2 = await TestUtils.WaitUntilWorkerDeploymentVisibleAsync(Client, workerV2);
            await TestUtils.SetCurrentDeploymentVersionAsync(Client, describe2.ConflictToken, workerV2);
            res = await Client.ExecuteWorkflowAsync(
                (WorkflowV1 wf) => wf.RunAsync(),
                new($"wf-{Guid.NewGuid()}", taskQueue));
            Assert.Equal("done-v2", res);
        }
        finally
        {
            await host.StopAsync(tokenSource.Token);
        }
    }

    [Fact]
    public async Task TemporalWorkerService_ExecuteAsync_DuplicateQueue()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        // Build with two workers on same queue but different versions
        var bld = Host.CreateApplicationBuilder();
        bld.Services.AddSingleton(Client);
        bld.Services.
            AddHostedTemporalWorker(taskQueue).
            AddWorkflow<WorkflowV1>();
        var exc = Assert.Throws<InvalidOperationException>(() =>
            bld.Services.
                AddHostedTemporalWorker(taskQueue).
                AddWorkflow<WorkflowV2>());
        Assert.StartsWith("Worker service", exc.Message);
        Assert.EndsWith("already on collection", exc.Message);
    }
}
