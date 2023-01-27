namespace Temporalio.Tests.Client;

using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Converters;
using Xunit;
using Xunit.Abstractions;

public class TemporalClientWorkflowTests : WorkflowEnvironmentTestBase
{
    public TemporalClientWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task StartWorkflowAsync_ManualReturnType_Succeeds()
    {
        var workflowID = $"workflow-{Guid.NewGuid()}";
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflowWithUnknownReturn.Ref.RunAsync,
            new KSWorkflowParams(new KSAction(Result: new(Value: "Some String"))),
            new(id: workflowID, taskQueue: Env.KitchenSinkWorkerTaskQueue));
        Assert.Equal(workflowID, handle.ID);
        Assert.NotNull(handle.ResultRunID);
        Assert.Equal("Some String", await handle.GetResultAsync<string>());
    }

    [Fact]
    public async Task StartWorkflowAsync_ReturnObject_Succeeds()
    {
        var result = await Client.ExecuteWorkflowAsync(
            IKitchenSinkWorkflowWithReturnObject.Ref.RunAsync,
            new KSWorkflowParams(new KSAction(Result: new(Value: new KSWorkflowResult("Some String")))),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));
        Assert.Equal(new KSWorkflowResult("Some String"), result);
    }

    [Fact]
    public async Task StartWorkflowAsync_AlreadyExists_Throws()
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

    [Fact]
    public async Task StartWorkflowAsync_AlreadyExistsCompletedIDReusePolicy_Throws()
    {
        // Run
        var workflowID = $"workflow-{Guid.NewGuid()}";
        await Client.ExecuteWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(new KSAction(Result: new(Value: "Some String"))),
            new(id: workflowID, taskQueue: Env.KitchenSinkWorkerTaskQueue));

        // Try to start again w/ ID reuse policy disallowing
        await Assert.ThrowsAsync<Exceptions.WorkflowAlreadyStartedException>(async () =>
        {
            await Client.StartWorkflowAsync(
                IKitchenSinkWorkflow.Ref.RunAsync,
                new KSWorkflowParams(new KSAction(Result: new(Value: "Some String"))),
                new(id: workflowID, taskQueue: Env.KitchenSinkWorkerTaskQueue)
                {
                    IDReusePolicy = WorkflowIdReusePolicy.AllowDuplicateFailedOnly,
                });
        });
    }

    [Fact]
    public async Task StartWorkflowAsync_StartSignal_Succeeds()
    {
        // Run by passing result via signal
        var result = await Client.ExecuteWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(ActionSignal: "SomeActionSignal"),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)
            {
                StartSignal = "SomeActionSignal",
                StartSignalArgs = new KSAction[] { new(Result: new(Value: "Some String")) },
            });
        Assert.Equal("Some String", result);
    }

    [Fact]
    public async Task StartWorkflowAsync_RetryPolicy_IsUsed()
    {
        // Retry 3 times, erroring with the attempt number
        var err = await Assert.ThrowsAsync<Exceptions.WorkflowFailedException>(async () =>
        {
            await Client.ExecuteWorkflowAsync(
                IKitchenSinkWorkflow.Ref.RunAsync,
                new KSWorkflowParams(new KSAction(Error: new(Attempt: true))),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)
                {
                    RetryPolicy = new()
                    {
                        InitialInterval = TimeSpan.FromMilliseconds(1),
                        MaximumAttempts = 3,
                    },
                });
        });
        Assert.Equal("attempt 3", err.InnerException?.Message);
    }

    [Fact]
    public async Task StartWorkflowAsync_Interceptors_AreCalledProperly()
    {
        // Create a client with interceptor
        var interceptor = new TracingClientInterceptor();
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Interceptors = new IClientInterceptor[] { interceptor };
        var client = new TemporalClient(Client.Connection, newOptions);

        // Do things to trigger interceptors
        var handle = await client.StartWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(
                new KSAction(QueryHandler: new(Name: "SomeQuery")),
                new KSAction(Signal: new("SomeSignal")),
                new KSAction(Result: new("Some String"))),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));
        await handle.QueryAsync(IKitchenSinkWorkflow.Ref.SomeQuery, "Some Query");
        await handle.SignalAsync(IKitchenSinkWorkflow.Ref.SomeSignalAsync, "Some Signal");
        await handle.GetResultAsync();
        // Does nothing
        await handle.CancelAsync();
        // Ignore terminate error (already completed)
        await Assert.ThrowsAsync<Exceptions.RpcException>(
            async () => await handle.TerminateAsync());

        // Check events
        void AssertEvent<TInput>(
            int index, string name, string inputValue, Func<TInput, string> inputGetter)
        {
            Assert.Equal(name, interceptor.Events[index].Name);
            Assert.Equal(inputValue, inputGetter.Invoke((TInput)interceptor.Events[index].Input));
        }
        AssertEvent<StartWorkflowInput>(0, "StartWorkflow", "kitchen_sink", i => i.Workflow);
        AssertEvent<QueryWorkflowInput>(1, "QueryWorkflow", "SomeQuery", i => i.Query);
        AssertEvent<SignalWorkflowInput>(2, "SignalWorkflow", "SomeSignal", i => i.Signal);
        AssertEvent<CancelWorkflowInput>(3, "CancelWorkflow", handle.ID, i => i.ID);
        AssertEvent<TerminateWorkflowInput>(4, "TerminateWorkflow", handle.ID, i => i.ID);
    }

    [Fact]
    public async Task StartWorkflowAsync_SearchAttributesAndMemo_AreSetProperly()
    {
        // Only add search attributes if not present
        var resp = await Client.Connection.WorkflowService.GetSearchAttributesAsync(new());
        if (!resp.Keys.ContainsKey("DotNetTemporalTestKeyword"))
        {
            await Client.Connection.OperatorService.AddSearchAttributesAsync(new()
            {
                SearchAttributes =
                {
                    new Dictionary<string, IndexedValueType>
                    {
                        ["DotNetTemporalTestBool"] = IndexedValueType.Bool,
                        ["DotNetTemporalTestDateTime"] = IndexedValueType.Datetime,
                        ["DotNetTemporalTestDouble"] = IndexedValueType.Double,
                        ["DotNetTemporalTestInt"] = IndexedValueType.Int,
                        ["DotNetTemporalTestKeyword"] = IndexedValueType.Keyword,
                        ["DotNetTemporalTestText"] = IndexedValueType.Text,
                    },
                },
            });
            resp = await Client.Connection.WorkflowService.GetSearchAttributesAsync(new());
            Assert.Contains("DotNetTemporalTestKeyword", resp.Keys.Keys);
        }

        // Run workflow with search attributes and memo set
        var keywords = new string[] { "SomeKeyword1", "SomeKeyword2" };
        // Use local here to confirm accurate conversion
        var dateTime = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var memoVals = new List<string> { "MemoVal1", "MemoVal2" };
        var handle = await Client.StartWorkflowAsync(
            IKitchenSinkWorkflow.Ref.RunAsync,
            new KSWorkflowParams(new KSAction(Result: new(Value: "Some String"))),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)
            {
                SearchAttributes = new Dictionary<string, object>
                {
                    ["DotNetTemporalTestBool"] = true,
                    ["DotNetTemporalTestDateTime"] = dateTime,
                    ["DotNetTemporalTestDouble"] = 123.45,
                    ["DotNetTemporalTestInt"] = 678,
                    ["DotNetTemporalTestKeyword"] = keywords,
                    ["DotNetTemporalTestText"] = "SomeText",
                },
                Memo = new Dictionary<string, object>
                {
                    ["MemoKey"] = memoVals,
                },
            });
        await handle.GetResultAsync();

        // Now describe and check search attributes and memos
        var desc = await handle.DescribeAsync();
        Assert.Equal(true, desc.SearchAttributes!["DotNetTemporalTestBool"]);
        Assert.Equal(dateTime, desc.SearchAttributes!["DotNetTemporalTestDateTime"]);
        Assert.Equal(123.45, desc.SearchAttributes!["DotNetTemporalTestDouble"]);
        Assert.Equal(678L, desc.SearchAttributes!["DotNetTemporalTestInt"]);
        Assert.Equal(keywords, desc.SearchAttributes!["DotNetTemporalTestKeyword"]);
        Assert.Equal("SomeText", desc.SearchAttributes!["DotNetTemporalTestText"]);
        var actualMemoVals = await Client.Options.DataConverter.ToValueAsync<List<string>>(
            desc.RawMemo!["MemoKey"]);
        Assert.Equal(memoVals, actualMemoVals);
    }

    internal record TracingEvent(string Name, object Input);

    internal class TracingClientInterceptor : IClientInterceptor
    {
        public List<TracingEvent> Events { get; } = new();

        public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor next)
        {
            return new TracingClientOutboundInterceptor(next, Events);
        }
    }

    internal class TracingClientOutboundInterceptor : ClientOutboundInterceptor
    {
        public TracingClientOutboundInterceptor(
            ClientOutboundInterceptor next, List<TracingEvent> events)
            : base(next)
        {
            Events = events;
        }

        public List<TracingEvent> Events { get; private init; }

        public override Task<WorkflowHandle<TResult>> StartWorkflowAsync<TResult>(
            StartWorkflowInput input)
        {
            Events.Add(new("StartWorkflow", input));
            return base.StartWorkflowAsync<TResult>(input);
        }

        public override Task SignalWorkflowAsync(SignalWorkflowInput input)
        {
            Events.Add(new("SignalWorkflow", input));
            return base.SignalWorkflowAsync(input);
        }

        public override Task<TResult> QueryWorkflowAsync<TResult>(QueryWorkflowInput input)
        {
            Events.Add(new("QueryWorkflow", input));
            return base.QueryWorkflowAsync<TResult>(input);
        }

        public override Task CancelWorkflowAsync(CancelWorkflowInput input)
        {
            Events.Add(new("CancelWorkflow", input));
            return base.CancelWorkflowAsync(input);
        }

        public override Task TerminateWorkflowAsync(TerminateWorkflowInput input)
        {
            Events.Add(new("TerminateWorkflow", input));
            return base.TerminateWorkflowAsync(input);
        }
    }

    // TODO(cretz): tests/features:
    // * List
}
