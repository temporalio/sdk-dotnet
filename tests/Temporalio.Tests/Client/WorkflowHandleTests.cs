namespace Temporalio.Tests.Client;

using System;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Xunit;
using Xunit.Abstractions;

public class WorkflowHandleTests : WorkflowEnvironmentTestBase
{
    public WorkflowHandleTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task GetResultAsync_ContinueAsNew_ProperlyFollowed()
    {
        // Start with a single round of continue as new
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(
                new KSAction(ContinueAsNew: new(WhileAboveZero: 1, Result: "Some String"))),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));

        // Check result with and without following
        Assert.Equal("Some String", await handle.GetResultAsync());
        await Assert.ThrowsAsync<Exceptions.WorkflowContinuedAsNewException>(
            async () => await handle.GetResultAsync(followRuns: false));
    }

    [Fact]
    public async Task GetResultAsync_Failure_Throws()
    {
        var err = await Assert.ThrowsAsync<Exceptions.WorkflowFailedException>(async () =>
        {
            await Client.ExecuteWorkflowAsync(
                IKitchenSinkWorkflow.Ref.RunAsync,
                new KSWorkflowParams(new KSAction(
                    Error: new(Message: "Some Message", Details: "Some Details"))),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));
        });
        var appErr = Assert.IsType<Exceptions.ApplicationFailureException>(err.InnerException);
        Assert.Equal("Some Message", appErr.Message);
        Assert.Equal("Some Details", appErr.Details.ElementAt<string>(0));
    }

    [Fact]
    public async Task GetResultAsync_NotFoundWorkflow_Throws()
    {
        var err = await Assert.ThrowsAsync<Exceptions.RpcException>(async () =>
            await Client.GetWorkflowHandle($"workflow-{Guid.NewGuid()}").GetResultAsync());
        Assert.Equal(Exceptions.RpcException.StatusCode.NotFound, err.Code);
    }

    [Fact]
    public async Task GetResultAsync_Timeout_Throws()
    {
        var err = await Assert.ThrowsAsync<Exceptions.RpcException>(async () =>
            await Client.ExecuteWorkflowAsync(
                IKitchenSinkWorkflow.Ref.RunAsync,
                new KSWorkflowParams(new KSAction(Sleep: new(Millis: 50000))),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)
                {
                    Rpc = new() { Timeout = TimeSpan.FromSeconds(2) },
                }));
        Assert.Equal(Exceptions.RpcException.StatusCode.Cancelled, err.Code);
    }

    [Fact]
    public async Task GetResultAsync_CancellationToken_Throws()
    {
        var cancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Client.ExecuteWorkflowAsync(
                IKitchenSinkWorkflow.Ref.RunAsync,
                new KSWorkflowParams(new KSAction(Sleep: new(Millis: 50000))),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)
                {
                    Rpc = new() { CancellationToken = cancelSource.Token },
                }));
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
    public async Task QueryAsync_WorkerFailure_Throws()
    {
        // Start
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(
                new KSAction(QueryHandler: new(Name: "SomeQuery", Error: "SomeError"))),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));

        // Ensure proper failure
        var err = await Assert.ThrowsAsync<Exceptions.WorkflowQueryFailedException>(async () =>
            await handle.QueryAsync(IKitchenSinkWorkflow.Ref.SomeQuery, "Some String"));
        Assert.Equal("SomeError", err.Message);
    }

    [Fact]
    public async Task QueryAsync_Rejection_Throws()
    {
        // Start and confirm complete
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(
                new KSAction(QueryHandler: new(Name: "SomeQuery")),
                new KSAction(Result: new(Value: "Some String"))),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));
        await handle.GetResultAsync();

        // Ensure proper failure if we have a reject condition
        var err = await Assert.ThrowsAsync<Exceptions.WorkflowQueryRejectedException>(async () =>
        {
            await handle.QueryAsync(IKitchenSinkWorkflow.Ref.SomeQuery, "Some String", new()
            {
                RejectCondition = QueryRejectCondition.NotOpen,
            });
        });
        Assert.Equal(WorkflowExecutionStatus.Completed, err.WorkflowStatus);
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
        var exc = await Assert.ThrowsAsync<Exceptions.WorkflowFailedException>(
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
        var exc = await Assert.ThrowsAsync<Exceptions.WorkflowFailedException>(
            async () => await handle.GetResultAsync());
        var inner = Assert.IsType<Exceptions.TerminatedFailureException>(exc.InnerException);
        Assert.Equal("Some reason", inner.Message);
        Assert.Equal("Some details", inner.Details?.ElementAt<string>(0));
    }
}