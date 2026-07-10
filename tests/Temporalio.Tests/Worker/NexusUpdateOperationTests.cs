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
/// <para>
/// Every test here is gated on <see cref="WorkflowEnvironment.SupportsNexusUpdateCallbacks"/> and is
/// skipped unless the environment can deliver update-completion callbacks
/// (<c>history.enableUpdateCallbacks</c> plus a Nexus callback-endpoint template). The pinned local
/// dev server does not support these yet, so these tests are ALL currently blocked purely on that
/// server-capability gap. When the dev server is upgraded and the flags are added in
/// <see cref="WorkflowEnvironment"/>, the gate flips and these tests auto-run — no code change here
/// required.
/// </para>
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

    [SkippableFact]
    public async Task UpdateOperation_ValidUpdate_SucceedsWithLinks()
    {
        // Punch-list: async op happy path with forward + back links asserted.
        SkipIfUpdateCallbacksUnsupported();
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var caller = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, Amount: 5, UpdateId: "valid-update"));
            Assert.Equal(5, await caller.GetResultAsync<int>());

            // Forward link: caller workflow's Nexus scheduled event references the operation.
            var callerScheduled = Assert.Single(
                (await caller.FetchHistoryAsync()).Events,
                e => e.EventType == EventType.NexusOperationScheduled);
            Assert.NotEmpty(callerScheduled.Links);

            // Back link: handler workflow's update-accepted event points back to the caller.
            var counterEvents = (await counter.FetchHistoryAsync()).Events;
            Assert.Contains(
                counterEvents,
                e => e.EventType == EventType.WorkflowExecutionUpdateAccepted &&
                    e.Links.Count > 0);
        });
    }

    [SkippableFact]
    public async Task UpdateOperation_UnregisteredUpdateHandler_Fails()
    {
        // Punch-list: unregistered update handler.
        SkipIfUpdateCallbacksUnsupported();
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

    [SkippableFact]
    public async Task UpdateOperation_ValidatorRejects_FailsNonRetryable()
    {
        // Punch-list: validator rejects the update (non-retryable -> failed operation).
        SkipIfUpdateCallbacksUnsupported();
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var caller = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, Amount: 6, UpdateId: "rejected-update"));
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => caller.GetResultAsync<int>());
            Assert.IsType<NexusOperationFailureException>(exc.InnerException);
        });
    }

    [SkippableFact]
    public async Task UpdateOperation_HandlerReturnsError_Fails()
    {
        // Punch-list: handler returns an error (passes validation, fails in the handler body).
        SkipIfUpdateCallbacksUnsupported();
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var caller = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, Amount: -5, UpdateId: "handler-error"));
            await Assert.ThrowsAsync<WorkflowFailedException>(() => caller.GetResultAsync<int>());
        });
    }

    [SkippableFact]
    public async Task UpdateOperation_ImmediateHandler_IsStillAsync()
    {
        // Punch-list: sync/immediately-completing handler. Per NEXUS-489, immediate returns are
        // still async because the operation only waits for the Accepted stage, not completion.
        SkipIfUpdateCallbacksUnsupported();
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

    [SkippableFact]
    public async Task UpdateOperation_ReusedUpdateId_IsIdempotentAndSync()
    {
        // Punch-list: reused UpdateID against an already-completed update returns a sync result and
        // does not re-apply the update.
        SkipIfUpdateCallbacksUnsupported();
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            var first = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, Amount: 5, UpdateId: "reused-id"));
            Assert.Equal(5, await first.GetResultAsync<int>());

            // Same update ID: the counter must not increment again.
            var second = await RunCallerAsync(
                taskQueue, endpoint, new(counter.Id, Amount: 5, UpdateId: "reused-id"));
            Assert.Equal(5, await second.GetResultAsync<int>());
        });
    }

    [SkippableFact]
    public async Task UpdateOperation_NoWorkerOnTargetQueue_DoesNotFail()
    {
        // Punch-list: no worker listening on the target workflow's task queue — the operation must
        // NOT fail (the update is admitted and stays pending until a worker handles it).
        SkipIfUpdateCallbacksUnsupported();
        await RunWithCounterAsync(async (endpoint, taskQueue, counter) =>
        {
            // Target a workflow on a task queue with no worker polling it.
            var idleTaskQueue = $"tq-idle-{Guid.NewGuid()}";
            var pending = await Client.StartWorkflowAsync(
                (CounterWorkflow wf) => wf.RunAsync(),
                new($"counter-idle-{Guid.NewGuid()}", idleTaskQueue));

            var caller = await RunCallerAsync(
                taskQueue, endpoint, new(pending.Id, Amount: 5));

            // The Nexus operation should remain pending (started, not failed) for a short window.
            await Task.Delay(TimeSpan.FromSeconds(3));
            var desc = await caller.DescribeAsync();
            Assert.NotEqual(WorkflowExecutionStatus.Failed, desc.Status);
        });
    }

    private void SkipIfUpdateCallbacksUnsupported()
    {
        if (!Env.SupportsNexusUpdateCallbacks)
        {
            throw new SkipException(
                "Environment does not support UpdateWorkflow-backed Nexus operations. Requires the " +
                "dev server to deliver update-completion callbacks (history.enableUpdateCallbacks) " +
                "with a Nexus callback-endpoint template; the pinned dev server " +
                "(v1.7.1-standalone-nexus-operations) does not yet support these. Set " +
                "TEMPORAL_TEST_NEXUS_UPDATE_CALLBACKS=true when pointing at a capable server.");
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
