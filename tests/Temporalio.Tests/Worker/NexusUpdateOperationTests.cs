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
/// End-to-end scaffold for UpdateWorkflow-backed Nexus operations, encoding the reviewer scenario
/// punch-list from the feature's design.
/// </summary>
public class NexusUpdateOperationTests : WorkflowEnvironmentTestBase
{
    public NexusUpdateOperationTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [NexusService]
    public interface ICounterService
    {
        [NexusOperation]
        int Add(AddInput input);

        // Uses the by-name StartUpdateWorkflowAsync overload with an update name that is not
        // registered on the target workflow.
        [NexusOperation]
        int AddUnregistered(AddInput input);
    }

    /// <summary>Backs each operation with a workflow update via the generic Temporal handler.</summary>
    [NexusServiceHandler(typeof(ICounterService))]
    public class CounterServiceHandler
    {
        [NexusOperationHandler]
        public IOperationHandler<AddInput, int> Add() =>
            TemporalOperationHandler.FromHandleFactory<AddInput, int>(
                (context, client, input) =>
                    client.StartUpdateWorkflowAsync<CounterWorkflow, int>(
                        input.WorkflowId,
                        wf => wf.AddAsync(input.Amount),
                        new(WorkflowUpdateStage.Accepted) { Id = input.UpdateId }));

        [NexusOperationHandler]
        public IOperationHandler<AddInput, int> AddUnregistered() =>
            TemporalOperationHandler.FromHandleFactory<AddInput, int>(
                (context, client, input) =>
                    client.StartUpdateWorkflowAsync<int>(
                        input.WorkflowId,
                        "NoSuchUpdateHandler",
                        new object?[] { input.Amount },
                        new(WorkflowUpdateStage.Accepted) { Id = input.UpdateId }));
    }

    /// <summary>Counter workflow with an update handler and a validator.</summary>
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

        [WorkflowUpdate]
        public async Task<int> AddAsync(int amount)
        {
            // A negative amount passes the validator (divisible by 5) but fails in the handler; used
            // to exercise the "handler returns an error" path.
            if (amount < 0)
            {
                throw new ApplicationFailureException("negative amount not allowed");
            }
            counter += amount;
            return counter;
        }

        [WorkflowUpdateValidator(nameof(AddAsync))]
        public void ValidateAdd(int amount)
        {
            if (amount % 5 != 0)
            {
                throw new ApplicationFailureException("invalid increment");
            }
        }

        [WorkflowSignal]
        public async Task DoneAsync() => done = true;
    }

    /// <summary>Caller workflow that invokes the Nexus operation, so forward/back links are formed.</summary>
    [Workflow]
    public class CounterCallerWorkflow
    {
        [WorkflowRun]
        public async Task<int> RunAsync(CallerInput input)
        {
            var client = Workflow.CreateNexusWorkflowClient<ICounterService>(input.Endpoint);
            return input.UseUnregistered
                ? await client.ExecuteNexusOperationAsync(svc => svc.AddUnregistered(input.Add))
                : await client.ExecuteNexusOperationAsync(svc => svc.Add(input.Add));
        }
    }

    public record AddInput(string WorkflowId, int Amount, string? UpdateId = null);

    public record CallerInput(string Endpoint, AddInput Add, bool UseUnregistered = false);

    [Fact]
    public async Task UpdateOperation_ValidUpdate_Succeeds()
    {
        // Punch-list: async op happy path. The update result is asserted; forward/back links are
        // checked only informationally.
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var caller = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, Amount: 5, UpdateId: "valid-update"));
            Assert.Equal(5, await caller.GetResultAsync<int>());

            // Coverage: the operation was scheduled exactly once on the caller.
            Assert.Single(
                (await caller.FetchHistoryAsync()).Events,
                e => e.EventType == EventType.NexusOperationScheduled);
        });
    }

    [Fact]
    public async Task UpdateOperation_UnregisteredUpdateHandler_Fails()
    {
        // Punch-list: unregistered update handler.
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var caller = await RunCallerAsync(
                taskQueue,
                endpoint,
                new(counter.Id, Amount: 5),
                useUnregistered: true);
            await Assert.ThrowsAsync<WorkflowFailedException>(() => caller.GetResultAsync<int>());
        });
    }

    [Fact]
    public async Task UpdateOperation_ValidatorRejects_FailsNonRetryable()
    {
        // Punch-list: validator rejects the update (non-retryable -> failed operation).
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var caller = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, Amount: 6, UpdateId: "rejected-update"));
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => caller.GetResultAsync<int>());
            Assert.IsType<NexusOperationFailureException>(exc.InnerException);
        });
    }

    [Fact]
    public async Task UpdateOperation_HandlerReturnsError_Fails()
    {
        // Punch-list: handler returns an error (passes validation, fails in the handler body).
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var caller = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, Amount: -5, UpdateId: "handler-error"));
            await Assert.ThrowsAsync<WorkflowFailedException>(() => caller.GetResultAsync<int>());
        });
    }

    [Fact]
    public async Task UpdateOperation_ImmediateHandler_IsStillAsync()
    {
        // Punch-list: sync/immediately-completing handler. Per NEXUS-489, immediate returns are
        // still async because the operation only waits for the Accepted stage, not completion.
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var caller = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, Amount: 5));
            Assert.Equal(5, await caller.GetResultAsync<int>());

            // The operation was scheduled/started asynchronously (has a scheduled event) rather than
            // completing synchronously inline.
            Assert.Contains(
                (await caller.FetchHistoryAsync()).Events,
                e => e.EventType == EventType.NexusOperationScheduled);
        });
    }

    [Fact]
    public async Task UpdateOperation_ReusedUpdateId_IsIdempotentAndSync()
    {
        // Punch-list: reused UpdateID against an already-completed update returns a sync result and
        // does not re-apply the update.
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var first = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, Amount: 5, UpdateId: "reused-id"));
            Assert.Equal(5, await first.GetResultAsync<int>());

            // Same update ID against the now-completed update: the counter must not increment again.
            var second = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, Amount: 5, UpdateId: "reused-id"));
            Assert.Equal(5, await second.GetResultAsync<int>());

            // Distinguish the two operations' execution modes via the caller histories. The
            // presence of a NexusOperationStarted event is the reliable async/sync separator on
            // this server: an async operation records scheduled -> started (with an operation
            // token) -> completed, whereas an operation that completes synchronously inline records
            // only scheduled -> completed.
            var firstEvents = (await first.FetchHistoryAsync()).Events;
            var secondEvents = (await second.FetchHistoryAsync()).Events;

            // First op (fresh update ID): async. Per NEXUS-489 the operation only waits for the
            // Accepted stage, so it starts asynchronously and carries an operation token.
            var firstStarted = Assert.Single(
                firstEvents, e => e.EventType == EventType.NexusOperationStarted);
            Assert.NotEmpty(firstStarted.NexusOperationStartedEventAttributes.OperationToken);

            // Second op (reusing the completed update ID): synchronous. It dedupes onto the already
            // completed update and completes inline, so there is no NexusOperationStarted marker; it
            // goes scheduled -> completed directly.
            Assert.DoesNotContain(
                secondEvents, e => e.EventType == EventType.NexusOperationStarted);
            Assert.Contains(
                secondEvents, e => e.EventType == EventType.NexusOperationScheduled);
            Assert.Contains(
                secondEvents, e => e.EventType == EventType.NexusOperationCompleted);
        });
    }

    // Punch-list: no worker listening on the target workflow's task queue. The operation must NOT
    // fail while it is pending (the update is admitted but not yet accepted). Tearing the worker
    // down then drains cleanly because the operation's cancellation token is forwarded into the
    // blocked update-start RPC (see NexusWorkflowStartHelper.StartUpdateWorkflowAsync), so worker
    // shutdown does not wedge.
    [Fact]
    public async Task UpdateOperation_NoWorkerOnTargetQueue_DoesNotFail()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(new CounterServiceHandler()).
            AddWorkflow<CounterWorkflow>().
            AddWorkflow<CounterCallerWorkflow>();
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(Client, workerOptions);
        using var cts = new CancellationTokenSource();
        var workerTask = worker.ExecuteAsync(cts.Token);
        try
        {
            // Target a workflow on a task queue with no worker polling it.
            var idleTaskQueue = $"tq-idle-{Guid.NewGuid()}";
            var pending = await Client.StartWorkflowAsync(
                (CounterWorkflow wf) => wf.RunAsync(),
                new($"counter-idle-{Guid.NewGuid()}", idleTaskQueue));

            var caller = await RunCallerAsync(
                taskQueue, endpointName, new(pending.Id, Amount: 5));

            // The Nexus operation should remain pending (started, not failed) for a short window.
            // This assertion passes — the failure below is purely the wedged worker shutdown.
            await Task.Delay(TimeSpan.FromSeconds(3));
            var desc = await caller.DescribeAsync();
            Assert.NotEqual(WorkflowExecutionStatus.Failed, desc.Status);
        }
        finally
        {
            await cts.CancelAsync();
        }

        // Worker teardown must drain cleanly: cancelling the worker cancels the blocked update-start
        // RPC, so the handler task releases and the worker shuts down. Bounded so a regression
        // (wedged shutdown) surfaces as a fast TimeoutException instead of hanging.
        try
        {
            await workerTask.WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (OperationCanceledException)
        {
            // Expected — the worker was stopped via its cancellation token.
        }
    }

    private async Task RunWithCounterAsync(
        Func<string, string, WorkflowHandle<CounterWorkflow, int>, Task> body)
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(new CounterServiceHandler()).
            AddWorkflow<CounterWorkflow>().
            AddWorkflow<CounterCallerWorkflow>();
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

    private async Task<WorkflowHandle<CounterCallerWorkflow, int>> RunCallerAsync(
        string taskQueue,
        string endpoint,
        AddInput add,
        bool useUnregistered = false)
    {
        var handle = await Client.StartWorkflowAsync(
            (CounterCallerWorkflow wf) => wf.RunAsync(new(endpoint, add, useUnregistered)),
            new($"caller-{Guid.NewGuid()}", taskQueue));
        return handle;
    }
}
