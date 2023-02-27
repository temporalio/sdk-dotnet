#pragma warning disable SA1201 // We want to have classes near their tests

namespace Temporalio.Tests.Worker;

using Temporalio.Api.Failure.V1;
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class WorkflowWorkerTests : WorkflowEnvironmentTestBase
{
    public WorkflowWorkerTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Workflow]
    public class SimpleWorkflow
    {
        public static readonly SimpleWorkflow Ref = Refs.Create<SimpleWorkflow>();

        [WorkflowRun]
        public Task<string> RunAsync(string name) => Task.FromResult($"Hello, {name}!");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_SimpleWorkflow_Succeeds()
    {
        await ExecuteWorkerAsync<SimpleWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync(
                SimpleWorkflow.Ref.RunAsync,
                "Temporal",
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Workflow]
    public class WorkflowInitWorkflow
    {
        public static readonly WorkflowInitWorkflow Ref = Refs.Create<WorkflowInitWorkflow>();
        private readonly string name;

        [WorkflowInit]
        public WorkflowInitWorkflow(string name) => this.name = name;

        [WorkflowRun]
        public Task<string> RunAsync(string _) => Task.FromResult($"Hello, {name}!");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WorkflowInitWorkflow_Succeeds()
    {
        await ExecuteWorkerAsync<WorkflowInitWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync(
                WorkflowInitWorkflow.Ref.RunAsync,
                "Temporal",
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Workflow]
    public class InvalidTaskCallsWorkflow
    {
        public static readonly InvalidTaskCallsWorkflow Ref = Refs.Create<InvalidTaskCallsWorkflow>();

        [WorkflowRun]
        public async Task RunAsync(string scenario)
        {
            switch (scenario)
            {
                case "Task.Delay":
                    await Task.Delay(5000);
                    break;
                default:
                    throw new NotImplementedException(scenario);
            }
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_InvalidTaskCallsWorkflow_FailsTask()
    {
        async Task AssertScenarioFailsTask(string scenario, string exceptionContains)
        {
            await ExecuteWorkerAsync<InvalidTaskCallsWorkflow>(async worker =>
            {
                // Start the workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    InvalidTaskCallsWorkflow.Ref.RunAsync,
                    scenario,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

                // Make sure task failure occurs in history eventually
                await AssertMore.EventuallyAsync(async () =>
                {
                    Failure? lastTaskFailure = null;
                    await foreach (var evt in handle.FetchHistoryEventsAsync())
                    {
                        if (evt.WorkflowTaskFailedEventAttributes != null)
                        {
                            lastTaskFailure = evt.WorkflowTaskFailedEventAttributes.Failure;
                        }
                    }
                    Assert.Contains(exceptionContains, lastTaskFailure?.Message);
                });
            });
        }

        await AssertScenarioFailsTask("Task.Delay", "Task.Delay cannot be used in workflows");
        // TODO(cretz): Scenarios to prevent:
        // * Task run/create/etc on different scheduler
        // * Task wait with timeout
    }

    private async Task ExecuteWorkerAsync<TWf>(Func<TemporalWorker, Task> action)
    {
        using var worker = new TemporalWorker(Client, new()
        {
            TaskQueue = $"tq-{Guid.NewGuid()}",
            Workflows = { typeof(TWf) },
        });
        await worker.ExecuteAsync(() => action(worker));
    }

    // TODO(cretz): Tests needed:
    // * Static class disallowed
    // * WorkflowInit with and without params
    // * Disallow generic methods
    // * WorkflowInfo features/values
    // * Multi-param workflows
    // * Default-param workflows
    // * Async utilities (e.g. Dataflow)
    // * Timers
    // * Continue as new
    // * Search attributes and memos
    // * Logging
    // * Stack trace query
    //   * Consider leveraging https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stacktracehiddenattribute in newer versions
    // * Deadlock detection
    // * Patching
    // * Wait conditions (including timeouts)
    // * Separate interface from impl
    // * Workflows as records with WorkflowInit
    // * Workflows as structs
    // * Custom errors
    // * Workflow not registered
    // * Cancellation
    //   * Swallowed and bubbled
    //   * Cancelled before run
    //   * Cancel while multiple aggregate async tasks running
    //   * Shielding/cleanup
    // * Signals/queries
    //   * Call single/multiple
    //   * Errors during
    // * Local/non-local activities
    //   * Call single/multiple
    //   * Cancel swallowed/bubbled
    //   * Cancel before started/task
    //   * Timeouts
    // * Child workflows
    //   * Blocking/non-blocking
    //   * Call single/multiple
    //   * Cancel swallowed/bubbled
    //   * Cancel before started/task
    //   * Signal
    //   * Failed start (including specific already-started exception)
    // * External workflows
    //   * Cancel
    //   * Signal
    // * Custom codec
    // * Interceptor
    // * Dynamic activities
    // * Dynamic workflows
    // * Tracing
    //   * CorrelationManager?
    //   * EventSource?
}