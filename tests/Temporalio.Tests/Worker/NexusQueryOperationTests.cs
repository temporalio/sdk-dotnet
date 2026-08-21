namespace Temporalio.Tests.Worker;

using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Nexus;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// End-to-end tests for Query-backed Nexus operations. A Query is always synchronous and writes
/// nothing to history, so the handler simply queries and returns the result; there is no operation
/// token and no completion callback.
/// </summary>
public class NexusQueryOperationTests : WorkflowEnvironmentTestBase
{
    public NexusQueryOperationTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [NexusService]
    public interface ICounterQueryService
    {
        [NexusOperation]
        int GetCount(QueryInput input);
    }

    /// <summary>Backs the operation with a workflow query.</summary>
    [NexusServiceHandler(typeof(ICounterQueryService))]
    public class CounterQueryServiceHandler
    {
        [NexusOperationHandler]
        public IOperationHandler<QueryInput, int> GetCount() =>
            OperationHandler.Sync<QueryInput, int>(async (ctx, input) =>
            {
                // A Query resolves immediately, so this is a plain synchronous operation: no
                // operation token, no completion callback, nothing to cancel.
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle(input.WorkflowId, input.RunId);
                return await handle.QueryAsync<int>(
                    "GetCount",
                    new object?[] { input.Fail },
                    input.RejectNotOpen ?
                        new() { RejectCondition = Api.Enums.V1.QueryRejectCondition.NotOpen } :
                        null);
            });
    }

    /// <summary>Counter workflow whose state a query reads.</summary>
    [Workflow]
    public class CounterWorkflow
    {
        private int counter;
        private bool done;

        [WorkflowRun]
        public async Task<int> RunAsync()
        {
            await Workflow.WaitConditionAsync(() => done);
            return counter;
        }

        [WorkflowQuery]
        public int GetCount(bool fail)
        {
            if (fail)
            {
                // A query handler that throws makes the server answer with a query failure, which
                // the handler surfaces to the caller as a failed operation.
                throw new InvalidOperationException("query failed (for testing)");
            }
            return counter;
        }

        [WorkflowSignal]
        public async Task BumpAsync() => counter++;

        [WorkflowSignal]
        public async Task DoneAsync() => done = true;
    }

    [Workflow]
    public class CounterQueryCallerWorkflow
    {
        [WorkflowRun]
        public async Task<int> RunAsync(CallerInput input) =>
            // Bounded so a regression that makes a terminal failure retryable surfaces as a timeout
            // rather than hanging the test.
            await Workflow.CreateNexusWorkflowClient<ICounterQueryService>(input.Endpoint).
                ExecuteNexusOperationAsync(
                    svc => svc.GetCount(input.Query),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromSeconds(20) });
    }

    public record QueryInput(
        string WorkflowId,
        string? RunId = null,
        bool Fail = false,
        bool RejectNotOpen = false);

    public record CallerInput(string Endpoint, QueryInput Query);

    [Fact]
    public async Task QueryOperation_ReturnsResult()
    {
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            await counter.SignalAsync(wf => wf.BumpAsync());
            await counter.SignalAsync(wf => wf.BumpAsync());

            var caller = await RunCallerAsync(taskQueue, endpoint, new(counter.Id));
            Assert.Equal(2, await caller.GetResultAsync<int>());
        });
    }

    [Fact]
    public async Task QueryOperation_CapturesResponseLink()
    {
        // End-to-end response link check: the server attaches a link to QueryWorkflowResponse, the
        // client hands it to the Nexus operation context, and the SDK puts it on the caller's
        // NexusOperationCompleted event.
        //
        // Only the response direction is asserted. A Query writes nothing to the queried workflow's
        // history, so there is no event on the callee side to carry a forward link, unlike signal.
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            await counter.SignalAsync(wf => wf.BumpAsync());
            await counter.SignalAsync(wf => wf.BumpAsync());

            var caller = await RunCallerAsync(taskQueue, endpoint, new(counter.Id));
            Assert.Equal(2, await caller.GetResultAsync<int>());

            var completed = Assert.Single(
                (await caller.FetchHistoryAsync()).Events,
                e => e.EventType == EventType.NexusOperationCompleted);
            AssertQueryResponseLink(completed, counter.Id);
        });
    }

    [Fact]
    public async Task QueryOperation_UnknownWorkflow_FailsOperation()
    {
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var caller = await RunCallerAsync(
                taskQueue, endpoint, new($"unknown-wid-{Guid.NewGuid()}"));
            await AssertOperationFailedWithAsync(HandlerErrorType.NotFound, caller);
        });
    }

    [Fact]
    public async Task QueryOperation_UnknownRun_FailsOperation()
    {
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var caller = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, RunId: Guid.NewGuid().ToString()));
            await AssertOperationFailedWithAsync(HandlerErrorType.NotFound, caller);
        });
    }

    [Fact]
    public async Task QueryOperation_FailedQuery_FailsOperation()
    {
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var caller = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, Fail: true));
            // The query handler faulted, not the Nexus request, so this is Internal and explicitly
            // non-retryable rather than BadRequest.
            await AssertOperationFailedWithAsync(HandlerErrorType.Internal, caller);
        });
    }

    [Fact]
    public async Task QueryOperation_RejectedQuery_FailsOperation()
    {
        // The reject condition is NotOpen, so querying a workflow that has already closed is
        // rejected and must surface as an operation failure.
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(new CounterQueryServiceHandler()).
            AddWorkflow<CounterWorkflow>().
            AddWorkflow<CounterQueryCallerWorkflow>();
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(Client, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var counter = await Client.StartWorkflowAsync(
                (CounterWorkflow wf) => wf.RunAsync(),
                new($"counter-{Guid.NewGuid()}", taskQueue));
            // Close the workflow before querying so NotOpen rejects.
            await counter.SignalAsync(wf => wf.DoneAsync());
            await counter.GetResultAsync();

            var caller = await RunCallerAsync(
                taskQueue, endpointName, new(counter.Id, RejectNotOpen: true));
            await AssertOperationFailedWithAsync(HandlerErrorType.Internal, caller);
        });
    }

    /// <summary>
    /// Asserts the caller's operation failed with the specific handler error type the SDK is supposed
    /// to derive from what the handler threw. Asserting only NexusOperationFailureException would
    /// also pass for a schedule-to-close timeout, which is what a wrongly-retryable failure looks
    /// like, so the classification is pinned here.
    /// </summary>
    private static async Task AssertOperationFailedWithAsync(
        HandlerErrorType expected, WorkflowHandle<CounterQueryCallerWorkflow, int> caller)
    {
        var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
            () => caller.GetResultAsync<int>());
        var nexusExc = Assert.IsType<NexusOperationFailureException>(exc.InnerException);
        var handlerExc = Assert.IsType<HandlerException>(nexusExc.InnerException);
        Assert.Equal(expected, handlerExc.ErrorType);
        Assert.False(handlerExc.IsRetryable);
    }

    /// <summary>
    /// Assert that a caller-side event carries a response link naming the queried workflow. A Query
    /// produces no history event, so the server answers with a <c>Link.Workflow</c> identifying the
    /// execution that processed the Query rather than the <c>Link.WorkflowEvent</c> the signal and
    /// update paths use.
    /// </summary>
    private static void AssertQueryResponseLink(
        Api.History.V1.HistoryEvent evt, string queriedWorkflowId)
    {
        Assert.NotEmpty(evt.Links);
        var link = evt.Links[0];
        // A Query link must use the Workflow variant, not WorkflowEvent, because a Query writes
        // nothing to history.
        Assert.Equal(
            Api.Common.V1.Link.VariantOneofCase.Workflow, link.VariantCase);
        Assert.Equal(queriedWorkflowId, link.Workflow.WorkflowId);
        // The link should name the run that processed the Query.
        Assert.NotEqual(string.Empty, link.Workflow.RunId);
    }

    private async Task RunWithCounterAsync(
        Func<string, string, WorkflowHandle<CounterWorkflow, int>, Task> body)
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(new CounterQueryServiceHandler()).
            AddWorkflow<CounterWorkflow>().
            AddWorkflow<CounterQueryCallerWorkflow>();
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(Client, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var counter = await Client.StartWorkflowAsync(
                (CounterWorkflow wf) => wf.RunAsync(),
                new($"counter-{Guid.NewGuid()}", taskQueue));
            try
            {
                await body(endpointName, taskQueue, counter);
            }
            finally
            {
                await counter.SignalAsync(wf => wf.DoneAsync());
            }
        });
    }

    private async Task<WorkflowHandle<CounterQueryCallerWorkflow, int>> RunCallerAsync(
        string taskQueue, string endpoint, QueryInput query) =>
        await Client.StartWorkflowAsync(
            (CounterQueryCallerWorkflow wf) => wf.RunAsync(new(endpoint, query)),
            new($"caller-{Guid.NewGuid()}", taskQueue));
}
