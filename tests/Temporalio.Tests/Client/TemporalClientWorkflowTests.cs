namespace Temporalio.Tests.Client;

using Xunit;
using Xunit.Abstractions;

public class TemporalClientWorkflowTests : WorkflowEnvironmentTestBase
{
    public TemporalClientWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task StartWorkflow_ManualReturnType_Succeeds()
    {
        var workflowID = $"workflow-{Guid.NewGuid()}";
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(new KSAction(Result: new(Value: "Some String"))),
            new(id: workflowID, taskQueue: Env.KitchenSinkWorkerTaskQueue));
        Assert.Equal(workflowID, handle.ID);
        Assert.NotNull(handle.ResultRunID);
        Assert.Equal("Some String", await handle.GetResultAsync<string>());
    }

    [Fact]
    public async Task StartWorkflow_KnownReturnType_Succeeds()
    {
        var workflowID = $"workflow-{Guid.NewGuid()}";
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflowWithReturnObject.Ref.RunAsync,
            new KSWorkflowParams(new KSAction(Result: new(Value: new KSWorkflowResult("Some String")))),
            new(id: workflowID, taskQueue: Env.KitchenSinkWorkerTaskQueue));
        Assert.Equal(workflowID, handle.ID);
        Assert.NotNull(handle.ResultRunID);
        Assert.Equal(new KSWorkflowResult("Some String"), await handle.GetResultAsync());
    }

    [Fact]
    public async Task StartWorkflow_AlreadyExists_Throws()
    {
        // Start
        var workflowID = $"workflow-{Guid.NewGuid()}";
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(new KSAction(Sleep: new(10000))),
            new(id: workflowID, taskQueue: Env.KitchenSinkWorkerTaskQueue));

        // Try to start again
        var err = await Assert.ThrowsAsync<Exceptions.WorkflowAlreadyStartedException>(async () =>
        {
            await Client.StartWorkflowAsync(
                IKitchenSinkWorkflow.Ref.RunAsync,
                new KSWorkflowParams(new KSAction(Result: new(Value: "Some String"))),
                new(id: workflowID, taskQueue: Env.KitchenSinkWorkerTaskQueue));
        });
        Assert.Equal(workflowID, err.WorkflowID);
        Assert.Equal(handle.ResultRunID, err.RunID);
    }

    // TODO(cretz): tests/features:
    // * ID reuse policy
    // * Start with signal
    // * Continue as new follow
    // * Workflow failure
    // * Cancel
    // * Cancel not found
    // * Terminate
    // * Signal
    // * Query
    // * Query rejected
    // * Describe
    // * Retry policy
    // * Interceptor
    // * List
    // * Fetch history
    // * Search attributes
}
