namespace Temporalio.Tests.Client;

using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Common;
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
        var arg = new KSWorkflowParams(new KSAction(Result: new(Value: "Some String")));
        var handle = await Client.StartWorkflowAsync(
            (IKitchenSinkWorkflowWithUnknownReturn wf) => wf.RunAsync(arg),
            new(id: workflowID, taskQueue: Env.KitchenSinkWorkerTaskQueue));
        Assert.Equal(workflowID, handle.ID);
        Assert.NotNull(handle.ResultRunID);
        Assert.Equal("Some String", await handle.GetResultAsync<string>());
    }

    [Fact]
    public async Task StartWorkflowAsync_ReturnObject_Succeeds()
    {
        var arg = new KSWorkflowParams(new KSAction(Result: new(Value: new KSWorkflowResult("Some String"))));
        var result = await Client.ExecuteWorkflowAsync(
            (IKitchenSinkWorkflowWithReturnObject wf) => wf.RunAsync(arg),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));
        Assert.Equal(new KSWorkflowResult("Some String"), result);
    }

    [Fact]
    public async Task StartWorkflowAsync_AlreadyExists_Throws()
    {
        // Start
        var workflowID = $"workflow-{Guid.NewGuid()}";
        var arg = new KSWorkflowParams(new KSAction(Sleep: new(10000)));
        var handle = await Client.StartWorkflowAsync(
            (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
            new(id: workflowID, taskQueue: Env.KitchenSinkWorkerTaskQueue));

        // Try to start again
        var err = await Assert.ThrowsAsync<Exceptions.WorkflowAlreadyStartedException>(async () =>
        {
            var arg = new KSWorkflowParams(new KSAction(Result: new(Value: "Some String")));
            await Client.StartWorkflowAsync(
                (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
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
        var arg = new KSWorkflowParams(new KSAction(Result: new(Value: "Some String")));
        await Client.ExecuteWorkflowAsync(
            (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
            new(id: workflowID, taskQueue: Env.KitchenSinkWorkerTaskQueue));

        // Try to start again w/ ID reuse policy disallowing
        await Assert.ThrowsAsync<Exceptions.WorkflowAlreadyStartedException>(async () =>
        {
            var arg = new KSWorkflowParams(new KSAction(Result: new(Value: "Some String")));
            await Client.StartWorkflowAsync(
                (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
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
        var arg = new KSWorkflowParams(ActionSignal: "SomeActionSignal");
        var result = await Client.ExecuteWorkflowAsync(
            (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
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
            var arg = new KSWorkflowParams(new KSAction(Error: new(Attempt: true)));
            await Client.ExecuteWorkflowAsync(
                (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
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
        var arg = new KSWorkflowParams(
            new KSAction(QueryHandler: new(Name: "SomeQuery")),
            new KSAction(Signal: new("SomeSignal")),
            new KSAction(Result: new("Some String")));
        var handle = await client.StartWorkflowAsync(
            (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));
        await handle.QueryAsync(wf => wf.SomeQuery("Some Query"));
        await handle.SignalAsync(wf => wf.SomeSignalAsync("Some Signal"));
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
        await EnsureSearchAttributesPresentAsync();
        // Run workflow with search attributes and memo set
        // Use local here to confirm accurate conversion
        var dateTime = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var memoVals = new List<string> { "MemoVal1", "MemoVal2" };
        var arg = new KSWorkflowParams(new KSAction(Result: new(Value: "Some String")));
        var handle = await Client.StartWorkflowAsync(
            (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)
            {
                TypedSearchAttributes = new SearchAttributeCollection.Builder().
                    Set(AttrBool, true).
                    Set(AttrDateTime, dateTime).
                    Set(AttrDouble, 123.45).
                    Set(AttrKeyword, "SomeKeyword").
                    // TODO(cretz): Fix after Temporal dev server upgraded
                    // Set(AttrKeywordList, new[] { "SomeKeyword1", "SomeKeyword2" }).
                    Set(AttrLong, 678).
                    Set(AttrText, "SomeText").
                    ToSearchAttributeCollection(),
                Memo = new Dictionary<string, object>
                {
                    ["MemoKey"] = memoVals,
                },
            });
        await handle.GetResultAsync();

        // Now describe and check search attributes and memos
        var desc = await handle.DescribeAsync();
        Assert.True(desc.TypedSearchAttributes.Get(AttrBool));
        Assert.Equal(dateTime, desc.TypedSearchAttributes.Get(AttrDateTime));
        Assert.Equal(123.45, desc.TypedSearchAttributes.Get(AttrDouble));
        Assert.Equal("SomeKeyword", desc.TypedSearchAttributes.Get(AttrKeyword));
        // TODO(cretz): Fix after Temporal dev server upgraded
        // Assert.Equal(new[] { "SomeKeyword1", "SomeKeyword2" }, desc.TypedSearchAttributes.Get(AttrKeywordList));
        Assert.Equal(678, desc.TypedSearchAttributes.Get(AttrLong));
        Assert.Equal("SomeText", desc.TypedSearchAttributes.Get(AttrText));
        Assert.Equal(memoVals, await desc.Memo["MemoKey"].ToValueAsync<List<string>>());
    }

    [Fact]
    public async Task ListWorkflowsAsync_RunWithHistoryFetch_IsAccurate()
    {
        // Run 5 workflows. Use the same workflow ID over and over to make sure we don't clash with
        // other tests.
        var workflowID = $"workflow-{Guid.NewGuid()}";
        var expectedResults = new HashSet<string>();
        for (var i = 0; i < 5; i++)
        {
            var result = $"Index {i}";
            expectedResults.Add(result);
            var arg = new KSWorkflowParams(new KSAction(Result: new(Value: result)));
            await Client.ExecuteWorkflowAsync(
                (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
                new(id: workflowID, taskQueue: Env.KitchenSinkWorkerTaskQueue));
        }

        // Now do a list and collect the actual params
        var actualResults = new HashSet<string>();
        await foreach (var wf in Client.ListWorkflowsAsync($"WorkflowId = '{workflowID}'"))
        {
            var hist = await Client.GetWorkflowHandle(wf.ID, runID: wf.RunID).FetchHistoryAsync();
            var completeEvent = hist.Events.Single(evt => evt.WorkflowExecutionCompletedEventAttributes != null);
            var result = await Client.Options.DataConverter.ToSingleValueAsync<string>(
                completeEvent.WorkflowExecutionCompletedEventAttributes.Result.Payloads_);
            actualResults.Add(result);
        }
        Assert.Equal(expectedResults, actualResults);
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

        public override Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            StartWorkflowInput input)
        {
            Events.Add(new("StartWorkflow", input));
            return base.StartWorkflowAsync<TWorkflow, TResult>(input);
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
}
