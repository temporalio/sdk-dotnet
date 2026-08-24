namespace Temporalio.Tests.Extensions.WorkflowStreams;

using System.Threading.Tasks;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Workflows;

[Workflow]
public class StreamHostWorkflow
{
    private readonly WorkflowStream stream;
    private bool finished;
    private bool rollover;

    [WorkflowInit]
    public StreamHostWorkflow(WorkflowStreamState? priorState) => stream = new(priorState);

    [WorkflowRun]
    public async Task RunAsync(WorkflowStreamState? priorState)
    {
        await Workflow.WaitConditionAsync(() => finished || rollover);
        if (rollover)
        {
            await stream.ContinueAsNewAsync(state => new object?[] { state });
        }
    }

    [WorkflowSignal]
    public Task FinishAsync()
    {
        finished = true;
        return Task.CompletedTask;
    }

    [WorkflowSignal]
    public Task RolloverAsync()
    {
        rollover = true;
        return Task.CompletedTask;
    }

    [WorkflowSignal]
    public Task PublishLocalAsync(string topic, string value)
    {
        stream.Topic(topic).Publish(value);
        return Task.CompletedTask;
    }

    [WorkflowUpdate]
    public Task TruncateAsync(long upToOffset)
    {
        stream.Truncate(upToOffset);
        return Task.CompletedTask;
    }

    [WorkflowUpdate]
    public Task PublishLocalAndTruncateAsync(string topic, string value, long upToOffset)
    {
        stream.Topic(topic).Publish(value);
        stream.Truncate(upToOffset);
        return Task.CompletedTask;
    }
}
