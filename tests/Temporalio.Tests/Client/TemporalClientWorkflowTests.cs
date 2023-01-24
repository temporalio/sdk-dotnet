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
        var workflowID = $"workflow1-{Guid.NewGuid()}";
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
        var workflowID = $"workflow1-{Guid.NewGuid()}";
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflowWithReturnObject.Ref.RunAsync,
            new KSWorkflowParams(new KSAction(Result: new(Value: new KSWorkflowResult("Some String")))),
            new(id: workflowID, taskQueue: Env.KitchenSinkWorkerTaskQueue));
        Assert.Equal(workflowID, handle.ID);
        Assert.NotNull(handle.ResultRunID);
        Assert.Equal(new KSWorkflowResult("Some String"), await handle.GetResultAsync());
    }

    // TODO(cretz): tests/features:
    // * ID reuse policy
    // * Start with signal
    // * Continue as new follow
    // * Workflow failure
    // * Cancel
    // * Cancel not found
    // * Terminate
    // * ID already started
    // * Signal
    // * Query
    // * Query rejected
    // * Describe
    // * Retry policy
    // * Interceptor
    // * List
    // * Fetch history
}
