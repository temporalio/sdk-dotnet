#pragma warning disable SA1201, SA1204 // We want to have classes near their tests
namespace Temporalio.Tests.Extensions.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Extensions.Hosting;
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
                AddHostedTemporalWorker(taskQueue).
                AddScopedActivities<DatabaseActivities>().
                AddWorkflow<DatabaseWorkflow>();
        }).Build();

        // Start the host
        using var tokenSource = new CancellationTokenSource();
        var hostTask = Task.Run(() => host.RunAsync(tokenSource.Token));

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
        var hostTask = Task.Run(() => host.RunAsync(tokenSource.Token));

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
}