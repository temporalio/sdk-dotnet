#pragma warning disable SA1201, SA1204 // We want to have classes near their tests
namespace Temporalio.Tests.Client;

using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Worker;
using Temporalio.Workflows;
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
        var workflowId = $"workflow-{Guid.NewGuid()}";
        var arg = new KSWorkflowParams(new KSAction(Result: new(Value: "Some String")));
        var handle = await Client.StartWorkflowAsync(
            (IKitchenSinkWorkflowWithUnknownReturn wf) => wf.RunAsync(arg),
            new(id: workflowId, taskQueue: Env.KitchenSinkWorkerTaskQueue));
        Assert.Equal(workflowId, handle.Id);
        Assert.NotNull(handle.ResultRunId);
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
        var workflowId = $"workflow-{Guid.NewGuid()}";
        var arg = new KSWorkflowParams(new KSAction(Sleep: new(10000)));
        var handle = await Client.StartWorkflowAsync(
            (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
            new(id: workflowId, taskQueue: Env.KitchenSinkWorkerTaskQueue));

        // Try to start again
        var err = await Assert.ThrowsAsync<Exceptions.WorkflowAlreadyStartedException>(async () =>
        {
            var arg = new KSWorkflowParams(new KSAction(Result: new(Value: "Some String")));
            await Client.StartWorkflowAsync(
                (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
                new(id: workflowId, taskQueue: Env.KitchenSinkWorkerTaskQueue));
        });
        Assert.Equal(workflowId, err.WorkflowId);
        Assert.Equal(handle.ResultRunId, err.RunId);
    }

    [Fact]
    public async Task StartWorkflowAsync_AlreadyExistsCompletedIdReusePolicy_Throws()
    {
        // Run
        var workflowId = $"workflow-{Guid.NewGuid()}";
        var arg = new KSWorkflowParams(new KSAction(Result: new(Value: "Some String")));
        await Client.ExecuteWorkflowAsync(
            (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
            new(id: workflowId, taskQueue: Env.KitchenSinkWorkerTaskQueue));

        // Try to start again w/ ID reuse policy disallowing
        await Assert.ThrowsAsync<Exceptions.WorkflowAlreadyStartedException>(async () =>
        {
            var arg = new KSWorkflowParams(new KSAction(Result: new(Value: "Some String")));
            await Client.StartWorkflowAsync(
                (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
                new(id: workflowId, taskQueue: Env.KitchenSinkWorkerTaskQueue)
                {
                    IdReusePolicy = WorkflowIdReusePolicy.AllowDuplicateFailedOnly,
                });
        });
    }

    [Fact]
    public async Task StartWorkflowAsync_StartDelay_WaitsProperly()
    {
        var arg = new KSWorkflowParams(new KSAction(Result: new(Value: "Some String")));
        var handle = await Client.StartWorkflowAsync(
            (IKitchenSinkWorkflowWithUnknownReturn wf) => wf.RunAsync(arg),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)
            {
                StartDelay = TimeSpan.FromMinutes(45),
            });
        // Check that first event has start delay
        await using var enumerator = handle.FetchHistoryEventsAsync().GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        var attrs = enumerator.Current.WorkflowExecutionStartedEventAttributes;
        Assert.Equal(
            TimeSpan.FromMinutes(45),
            enumerator.Current.WorkflowExecutionStartedEventAttributes.FirstWorkflowTaskBackoff.ToTimeSpan());
    }

    [Fact]
    public async Task StartWorkflowAsync_SignalWithStartDelay_WaitsProperly()
    {
        var arg = new KSWorkflowParams(new KSAction(Result: new(Value: "Some String")));
        var handle = await Client.StartWorkflowAsync(
            (IKitchenSinkWorkflowWithUnknownReturn wf) => wf.RunAsync(arg),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)
            {
                StartDelay = TimeSpan.FromMinutes(45),
                StartSignal = "some-signal",
            });
        // Check that first event has start delay
        await using var enumerator = handle.FetchHistoryEventsAsync().GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        var attrs = enumerator.Current.WorkflowExecutionStartedEventAttributes;
        Assert.Equal(
            TimeSpan.FromMinutes(45),
            enumerator.Current.WorkflowExecutionStartedEventAttributes.FirstWorkflowTaskBackoff.ToTimeSpan());
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
        AssertEvent<CancelWorkflowInput>(3, "CancelWorkflow", handle.Id, i => i.Id);
        AssertEvent<TerminateWorkflowInput>(4, "TerminateWorkflow", handle.Id, i => i.Id);
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
        var workflowId = $"workflow-{Guid.NewGuid()}";
        var expectedResults = new HashSet<string>();
        for (var i = 0; i < 5; i++)
        {
            var result = $"Index {i}";
            expectedResults.Add(result);
            var arg = new KSWorkflowParams(new KSAction(Result: new(Value: result)));
            await Client.ExecuteWorkflowAsync(
                (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
                new(id: workflowId, taskQueue: Env.KitchenSinkWorkerTaskQueue));
        }

        // Now do a list and collect the actual params
        var actualResults = new HashSet<string>();
        await foreach (var wf in Client.ListWorkflowsAsync($"WorkflowId = '{workflowId}'"))
        {
            var hist = await Client.GetWorkflowHandle(wf.Id, runId: wf.RunId).FetchHistoryAsync();
            var completeEvent = hist.Events.Single(evt => evt.WorkflowExecutionCompletedEventAttributes != null);
            var result = await Client.Options.DataConverter.ToSingleValueAsync<string>(
                completeEvent.WorkflowExecutionCompletedEventAttributes.Result.Payloads_);
            actualResults.Add(result);
        }
        Assert.Equal(expectedResults, actualResults);

        // Verify limit option works
        var limitedResults = 0;
        await foreach (var wf in Client.ListWorkflowsAsync(
            $"WorkflowId = '{workflowId}'", new() { Limit = 3 }))
        {
            limitedResults++;
        }
        Assert.Equal(3, limitedResults);
    }

    [Workflow]
    public class CountableWorkflow
    {
        [WorkflowRun]
        public Task RunAsync(bool waitForever) => Workflow.WaitConditionAsync(() => !waitForever);
    }

    [Fact]
    public async Task CountWorkflowsAsync_SimpleCount_IsAccurate()
    {
        // Start 3 workflows to complete and 2 on another that don't complete
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<CountableWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            for (var i = 0; i < 3; i++)
            {
                await Client.ExecuteWorkflowAsync(
                    (CountableWorkflow wf) => wf.RunAsync(false),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            }
            for (var i = 0; i < 2; i++)
            {
                await Client.StartWorkflowAsync(
                    (CountableWorkflow wf) => wf.RunAsync(true),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            }
        });

        await AssertMore.EventuallyAsync(async () =>
        {
            // Normal count
            var resp = await Client.CountWorkflowsAsync(
                $"TaskQueue = '{worker.Options.TaskQueue}'");
            Assert.Equal(5, resp.Count);
            Assert.Empty(resp.Groups);

            // Grouped based on status
            resp = await Client.CountWorkflowsAsync(
                $"TaskQueue = '{worker.Options.TaskQueue}' GROUP BY ExecutionStatus");
            Assert.Equal(5, resp.Count);
            Assert.Equal(2, resp.Groups.Count);
            Assert.Equal(3, resp.Groups.Single(v => v.GroupValues.SequenceEqual(new[] { "Completed" })).Count);
            Assert.Equal(2, resp.Groups.Single(v => v.GroupValues.SequenceEqual(new[] { "Running" })).Count);
        });
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
