namespace Temporalio.Tests.Client;

using Xunit;
using Xunit.Abstractions;

public class WorkflowHandleTests : WorkflowEnvironmentTestBase
{
    public WorkflowHandleTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task SignalAsync_Simple_Succeeds()
    {
        // Start
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(ActionSignal: "SomeActionSignal"),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));

        // Send signal
        await handle.SignalAsync(
            IKitchenSinkWorkflow.Ref.SomeActionSignalAsync,
            new KSAction(Result: new(Value: "Some String")));

        // Confirm result
        Assert.Equal("Some String", await handle.GetResultAsync());
    }

    [Fact]
    public async Task QueryAsync_Simple_Succeeds()
    {
        // Start
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(new KSAction(QueryHandler: new(Name: "SomeQuery"))),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));

        // Issue query
        var res = await handle.QueryAsync(IKitchenSinkWorkflow.Ref.SomeQuery, "Some String");

        // Confirm result
        Assert.Equal("Some String", res);
    }

    // TODO(cretz): tests/features:
    // * Cancel
    // * Cancel not found
    // * Terminate
    // * Signal (more)
    // * Query (more)
    // * Describe
    // * Fetch history
}