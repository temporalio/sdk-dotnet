namespace Temporalio.Tests.Client;

using System;
using Temporalio.Api.Enums.V1;
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

    [Fact]
    public async Task DescribeAsync_Simple_HasProperValues()
    {
        // Run
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(new KSAction(Result: new(Value: "Some String"))),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));
        Assert.Equal("Some String", await handle.GetResultAsync());

        // Describe
        var desc = await handle.DescribeAsync();

        // Check values
        var beforeNow = DateTime.UtcNow.AddSeconds(-30);
        var afterNow = DateTime.UtcNow.AddSeconds(30);
        var temp = desc.CloseTime!.Value;
        Assert.InRange(desc.CloseTime!.Value, beforeNow, afterNow);
        Assert.InRange(desc.ExecutionTime!.Value, beforeNow, afterNow);
        Assert.Null(desc.ParentID);
        Assert.Null(desc.ParentRunID);
        Assert.Equal(handle.FirstExecutionRunID, desc.RunID);
        Assert.InRange(desc.StartTime, beforeNow, afterNow);
        Assert.Equal(WorkflowExecutionStatus.Completed, desc.Status);
        Assert.Equal(Env.KitchenSinkWorkerTaskQueue, desc.TaskQueue);
        Assert.Equal("kitchen_sink", desc.WorkflowType);
    }

    [Fact]
    public async Task CancelAsync_Simple_ThrowsProperException()
    {
        // Start
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(new KSAction(Sleep: new(Millis: 50000))),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));

        // Cancel
        await handle.CancelAsync();

        // Wait for result
        var exc = await Assert.ThrowsAsync<Exceptions.WorkflowFailureException>(
            async () => await handle.GetResultAsync());
        Assert.IsType<Exceptions.CancelledFailureException>(exc.InnerException);
    }

    [Fact]
    public async Task TerminateAsync_Simple_ThrowsProperException()
    {
        // Start
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(new KSAction(Sleep: new(Millis: 50000))),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));

        // Terminate
        await handle.TerminateAsync(
            "Some reason", new() { Details = new object[] { "Some details" } });

        // Wait for result
        var exc = await Assert.ThrowsAsync<Exceptions.WorkflowFailureException>(
            async () => await handle.GetResultAsync());
        var inner = Assert.IsType<Exceptions.TerminatedFailureException>(exc.InnerException);
        Assert.Equal("Some reason", inner.Message);
        Assert.Equal("Some details", inner.Details?.ElementAt<string>(0));
    }

    // TODO(cretz): tests/features:
    // * Cancel
    // * Cancel not found
    // * Terminate
    // * Signal (more)
    // * Query (more)
    // * Describe
    // * Fetch history
    // * Wait result timeout
    // * Wait result abandon via cancel token
}