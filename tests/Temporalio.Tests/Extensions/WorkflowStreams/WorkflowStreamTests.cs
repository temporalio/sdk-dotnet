namespace Temporalio.Tests.Extensions.WorkflowStreams;

using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class WorkflowStreamTests : WorkflowEnvironmentTestBase
{
    public WorkflowStreamTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task WorkflowPublication_IsReceivedByTypedClientTopic()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<PublishingWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (PublishingWorkflow workflow) => workflow.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await using var client = new WorkflowStreamClient(Client, handle.Id);
            await using var enumerator = client.GetTopic<string>("status").SubscribeAsync().
                GetAsyncEnumerator();

            Assert.True(await enumerator.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal("started", enumerator.Current.Value);
            Assert.Equal(0, enumerator.Current.Offset);

            await handle.SignalAsync(workflow => workflow.FinishAsync());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task CaptureStateForContinueAsNewAsync_DisablesWorkflowPublication()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<StateCaptureWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (StateCaptureWorkflow workflow) => workflow.RunAsync(null),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.True(await handle.GetResultAsync());
        });
    }

    [Workflow]
    public class PublishingWorkflow
    {
        private readonly WorkflowStream stream = new();
        private bool finished;

        [WorkflowRun]
        public async Task RunAsync()
        {
            stream.GetTopic<string>("status").Publish("started");
            await Workflow.WaitConditionAsync(() => finished);
        }

        [WorkflowSignal]
        public Task FinishAsync()
        {
            finished = true;
            return Task.CompletedTask;
        }
    }

    [Workflow]
    public class StateCaptureWorkflow
    {
        private readonly WorkflowStream stream;

        [WorkflowInit]
        public StateCaptureWorkflow(WorkflowStreamState? state)
        {
            stream = new(state);
        }

        [WorkflowRun]
        public async Task<bool> RunAsync(WorkflowStreamState? state)
        {
            _ = await stream.CaptureStateForContinueAsNewAsync();
            try
            {
                stream.GetTopic("topic").Publish("too late");
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            return false;
        }
    }
}
