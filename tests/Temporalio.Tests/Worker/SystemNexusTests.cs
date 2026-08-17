namespace Temporalio.Tests.Worker;

using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Tests.Converters;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class SystemNexusTests : WorkflowEnvironmentTestBase
{
    public SystemNexusTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_SignalWithStartFromWorkflow_SucceedsAndReplays()
    {
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.DataConverter = DataConverter.Default with
        {
            PayloadCodec = new Base64PayloadCodec(),
        };
        var codecClient = new TemporalClient(Client.Connection, newOptions);
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddWorkflow<SystemNexusSignalWithStartTargetWorkflow>();
        await ExecuteWorkerAsync<SystemNexusSignalWithStartCallerWorkflow>(
            async worker =>
            {
                var targetWorkflowId = $"workflow-{Guid.NewGuid()}";
                var callerHandle = await codecClient.StartWorkflowAsync(
                    (SystemNexusSignalWithStartCallerWorkflow workflow) =>
                        workflow.RunAsync(targetWorkflowId, worker.Options.TaskQueue!),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                var resultWorkflowId = await callerHandle.GetResultAsync();
                Assert.Equal(targetWorkflowId, resultWorkflowId);

                var targetHandle = codecClient.GetWorkflowHandle<
                    SystemNexusSignalWithStartTargetWorkflow,
                    IReadOnlyCollection<string>>(targetWorkflowId);
                var events = await targetHandle.GetResultAsync();
                Assert.Equal(3, events.Count);
                Assert.Contains("Started: start-value", events);
                Assert.Contains("Signal: signal-one", events);
                Assert.Contains("Signal: signal-two", events);

                var replayer = new WorkflowReplayer(
                    new WorkflowReplayerOptions
                    {
                        DataConverter = newOptions.DataConverter,
                    }.AddWorkflow<SystemNexusSignalWithStartCallerWorkflow>());
                var replay = await replayer.ReplayWorkflowAsync(await callerHandle.FetchHistoryAsync());
                Assert.Null(replay.ReplayFailure);
            },
            workerOptions,
            codecClient);
    }

    [Workflow]
    public class SystemNexusSignalWithStartTargetWorkflow
    {
        private readonly List<string> events = new();

        [WorkflowRun]
        public async Task<IReadOnlyCollection<string>> RunAsync(string value)
        {
            events.Add($"Started: {value}");
            await Workflow.WaitConditionAsync(() => events.Count >= 3);
            return events;
        }

        [WorkflowSignal]
        public Task SignalAsync(string value)
        {
            events.Add($"Signal: {value}");
            return Task.CompletedTask;
        }
    }

    [Workflow]
    public class SystemNexusSignalWithStartCallerWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string workflowId, string taskQueue)
        {
            var handle = await Workflow.SignalWithStartWorkflowAsync(
                    (SystemNexusSignalWithStartTargetWorkflow workflow) =>
                        workflow.RunAsync("start-value"),
                    workflow => workflow.SignalAsync("signal-one"),
                    new(workflowId, taskQueue)
                    {
                        IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
                    });

            await Workflow.SignalWithStartWorkflowAsync(
                (SystemNexusSignalWithStartTargetWorkflow workflow) =>
                    workflow.RunAsync("unused-start-value"),
                workflow => workflow.SignalAsync("signal-two"),
                new(workflowId, taskQueue)
                {
                    IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
                });

            return handle.Id;
        }
    }

    private static async Task ExecuteWorkerAsync<TWorkflow>(
        Func<TemporalWorker, Task> action,
        TemporalWorkerOptions options,
        IWorkerClient client)
    {
        options = (TemporalWorkerOptions)options.Clone();
        options.AddWorkflow<TWorkflow>();
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(client, options);
        await worker.ExecuteAsync(() => action(worker));
    }
}
