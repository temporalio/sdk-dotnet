namespace Temporalio.Tests.Worker;

using System.Collections.Concurrent;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Nexus;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Serialization context coverage for executions started, and updates sent, by a Nexus operation
/// handler.
/// </summary>
/// <remarks>
/// A workflow or standalone activity started by a handler, or an update it sends, returns its
/// result and failure to the Nexus caller, which decodes them with the operation's Nexus context.
/// The handler sends that context with the request, the server propagates it to the worker, and
/// clients reading the outcome decode with the context the server returns. Every test runs with
/// <see cref="SigningRecordingCodec"/>, which fails a decode under any other context.
/// </remarks>
[CloudTestExclusion(
    CloudTestExclusionReason.RequiresCloudProvisioning,
    "Requires a Cloud namespace with Standalone Nexus Operations and Activities enabled.")]
public class NexusStartedSerializationContextTests : WorkflowEnvironmentTestBase
{
    // Keyed by activity ID, since each test uses its own
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<ISerializationContext.Nexus?>> ActivityAttemptContexts = new();
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<(byte[] TaskToken, ISerializationContext.Nexus? Context)>> AsyncCompletions = new();
    // Keyed by update ID, holding the signature on the raw outcome failure the handler received
    private static readonly ConcurrentDictionary<string, string?> UpdateOutcomeFailureSignatures = new();

    public NexusStartedSerializationContextTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    public enum Outcome
    {
        Result,
        Failure,
        ContinueAsNew,
        Async,
        Retry,
        Rejection,
    }

    [NexusService]
    public interface INexusStartedService
    {
        [NexusOperation]
        string StartWorkflow(NexusStartedInput input);

        [NexusOperation]
        string StartActivity(NexusStartedInput input);

        [NexusOperation]
        string UpdateWorkflow(NexusUpdateInput input);
    }

    public record NexusStartedInput(string Id, string Value, Outcome Outcome);

    public record NexusUpdateInput(string WorkflowId, string UpdateId, Outcome Outcome);

    [Workflow]
    public class NexusStartedWorkflow
    {
        [WorkflowRun]
        public Task<string> RunAsync(NexusStartedInput input)
        {
            if (input.Outcome == Outcome.ContinueAsNew)
            {
                var next = input with { Outcome = Outcome.Result };
                return Task.FromException<string>(Workflow.CreateContinueAsNewException(
                    (NexusStartedWorkflow wf) => wf.RunAsync(next)));
            }
            if (input.Outcome == Outcome.Failure)
            {
                return Task.FromException<string>(new ApplicationFailureException(
                    "nexus-started-failure", details: new object?[] { "failure-detail" }, nonRetryable: true));
            }
            if (input.Outcome == Outcome.Retry)
            {
                if (Workflow.Info.Attempt == 1)
                {
                    return Task.FromException<string>(new ApplicationFailureException(
                        "retry", details: new object?[] { "retry-detail" }));
                }
                // The first run encoded this failure with the Nexus context
                var lastFailure = Workflow.Info.LastFailure as ApplicationFailureException;
                if (lastFailure?.Details.ElementAt<string>(0) != "retry-detail")
                {
                    return Task.FromException<string>(new ApplicationFailureException(
                        $"unexpected last failure: {Workflow.Info.LastFailure}", nonRetryable: true));
                }
            }
            return Task.FromResult(input.Value);
        }
    }

    [Workflow]
    public class NexusUpdatedWorkflow
    {
        private bool finished;

        [WorkflowRun]
        public Task RunAsync() => Workflow.WaitConditionAsync(() => finished);

        [WorkflowSignal]
        public Task FinishAsync()
        {
            finished = true;
            return Task.CompletedTask;
        }

        [WorkflowUpdate]
        public Task<string> UpdateAsync(Outcome outcome) =>
            outcome == Outcome.Failure ?
                Task.FromException<string>(new ApplicationFailureException(
                    "nexus-update-failure", details: new object?[] { "failure-detail" }, nonRetryable: true)) :
                Task.FromResult("nexus-value");

        [WorkflowUpdateValidator(nameof(UpdateAsync))]
        public void ValidateUpdate(Outcome outcome)
        {
            if (outcome == Outcome.Rejection)
            {
                throw new ApplicationFailureException(
                    "nexus-update-rejection", details: new object?[] { "rejection-detail" });
            }
        }
    }

    public static class NexusStartedActivities
    {
        [Activity]
        public static async Task<string> RunAsync(NexusStartedInput input)
        {
            var info = ActivityExecutionContext.Current.Info;
            ActivityAttemptContexts.GetOrAdd(input.Id, _ => new()).Enqueue(info.NexusSerializationContext);
            if (input.Outcome == Outcome.Failure)
            {
                throw new ApplicationFailureException(
                    "nexus-started-failure", details: new object?[] { "failure-detail" }, nonRetryable: true);
            }
            if (input.Outcome == Outcome.Async)
            {
                AsyncCompletions.GetOrAdd(input.Id, _ => new()).SetResult(
                    (info.TaskToken, info.NexusSerializationContext));
                throw new CompleteAsyncException();
            }
            if (info.Attempt == 1)
            {
                // The next attempt decodes these details with the same context
                ActivityExecutionContext.Current.Heartbeat("heartbeat-detail");
                throw new ApplicationFailureException("retry");
            }
            var details = await info.HeartbeatDetailAtAsync<string>(0);
            if (details != "heartbeat-detail")
            {
                throw new ApplicationFailureException(
                    $"unexpected heartbeat details: {details}", nonRetryable: true);
            }
            return input.Value;
        }
    }

    [NexusServiceHandler(typeof(INexusStartedService))]
    public class NexusStartedServiceHandler
    {
        [TemporalOperation]
        public Task<TemporalOperationResult<string>> StartWorkflow(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, NexusStartedInput input) =>
            client.StartWorkflowAsync(
                (NexusStartedWorkflow wf) => wf.RunAsync(input),
                new()
                {
                    Id = input.Id,
                    RetryPolicy = input.Outcome == Outcome.Retry ?
                        new() { InitialInterval = TimeSpan.FromMilliseconds(100), MaximumAttempts = 2 } : null,
                });

        [TemporalOperation]
        public Task<TemporalOperationResult<string>> StartActivity(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, NexusStartedInput input) =>
            client.StartActivityAsync(
                () => NexusStartedActivities.RunAsync(input),
                new()
                {
                    Id = input.Id,
                    StartToCloseTimeout = TimeSpan.FromSeconds(10),
                    RetryPolicy = new() { InitialInterval = TimeSpan.FromMilliseconds(100), MaximumAttempts = 2 },
                });

        [TemporalOperation]
        public Task<TemporalOperationResult<string>> UpdateWorkflow(
            TemporalOperationStartContext ctx, ITemporalNexusClient client, NexusUpdateInput input) =>
            client.StartWorkflowUpdateAsync(
                input.WorkflowId,
                (NexusUpdatedWorkflow wf) => wf.UpdateAsync(input.Outcome),
                new(input.UpdateId, WorkflowUpdateStage.Accepted));
    }

    [Theory]
    [InlineData(Outcome.Result)]
    [InlineData(Outcome.Failure)]
    [InlineData(Outcome.ContinueAsNew)]
    public async Task Workflow_StartedByNexusHandler_UsesTheNexusContext(Outcome outcome)
    {
        await RunAsync(nameof(INexusStartedService.StartWorkflow), async (client, nexusClient, context, _) =>
        {
            var workflowId = $"nexus-started-workflow-{Guid.NewGuid()}";
            var input = new NexusStartedInput(workflowId, "nexus-value", outcome);
            // This handle never started the workflow, so it relies on the context the server
            // returns, including for the run after continue-as-new
            var handle = client.GetWorkflowHandle<NexusStartedWorkflow, string>(workflowId);
            if (outcome == Outcome.Failure)
            {
                await Assert.ThrowsAsync<NexusOperationFailedException>(() =>
                    nexusClient.ExecuteNexusOperationAsync(svc => svc.StartWorkflow(input), OperationOptions()));
                var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() => handle.GetResultAsync());
                var appFailure = Assert.IsType<ApplicationFailureException>(exc.InnerException);
                Assert.Equal("failure-detail", appFailure.Details.ElementAt<string>(0));
            }
            else
            {
                Assert.Equal(
                    "nexus-value",
                    await nexusClient.ExecuteNexusOperationAsync(
                        svc => svc.StartWorkflow(input), OperationOptions()));
                Assert.Equal("nexus-value", await handle.GetResultAsync());
            }

            // Read the raw events rather than decoding them: the signature identifies the context
            // each payload was encoded under, and decoding would strip it.
            var history = await client.Connection.WorkflowService.GetWorkflowExecutionHistoryAsync(new()
            {
                Namespace = client.Options.Namespace,
                Execution = new() { WorkflowId = workflowId },
            });
            var events = history.History.Events;
            var started = events.First().WorkflowExecutionStartedEventAttributes;
            Assert.Equal(context, ISerializationContext.Nexus.FromProto(started.PropagatedNexusSerializationContext));
            // The input keeps the workflow context
            Assert.All(started.Input.Payloads_, p => Assert.Null(SigningRecordingCodec.SignatureOf(p)));
            var closeEvent = events.Last();
            var closePayload = outcome == Outcome.Failure ?
                closeEvent.WorkflowExecutionFailedEventAttributes.Failure.ApplicationFailureInfo.Details.Payloads_.First() :
                closeEvent.WorkflowExecutionCompletedEventAttributes.Result.Payloads_.First();
            Assert.Equal(SigningRecordingCodec.Signature(context), SigningRecordingCodec.SignatureOf(closePayload));
        });
    }

    [Fact]
    public async Task Workflow_StartedByNexusHandler_RetryDecodesTheLastFailureWithTheNexusContext()
    {
        await RunAsync(nameof(INexusStartedService.StartWorkflow), async (client, nexusClient, context, _) =>
        {
            var workflowId = $"nexus-started-workflow-{Guid.NewGuid()}";
            // Decoding the last failure under the wrong context fails every task of the second run,
            // so this times out instead of completing
            Assert.Equal(
                "nexus-value",
                await nexusClient.ExecuteNexusOperationAsync(
                    svc => svc.StartWorkflow(new(workflowId, "nexus-value", Outcome.Retry)),
                    new($"op-{Guid.NewGuid()}") { ScheduleToCloseTimeout = TimeSpan.FromSeconds(20) }));
        });
    }

    [Fact]
    public async Task Activity_StartedByNexusHandler_UsesTheNexusContext()
    {
        await RunAsync(nameof(INexusStartedService.StartActivity), async (client, nexusClient, context, _) =>
        {
            var activityId = $"nexus-started-activity-{Guid.NewGuid()}";
            Assert.Equal(
                "nexus-value",
                await nexusClient.ExecuteNexusOperationAsync(
                    svc => svc.StartActivity(new(activityId, "nexus-value", Outcome.Result)),
                    OperationOptions()));
            // Both attempts saw the context, and the second decoded the first's heartbeat details
            Assert.Equal(new[] { context, context }, ActivityAttemptContexts[activityId]);

            var handle = client.GetActivityHandle<string>(activityId);
            Assert.Equal("nexus-value", await handle.GetResultAsync());
            var description = await handle.DescribeAsync(new()
            {
                IncludeOutcome = true,
                IncludeHeartbeatDetails = true,
                IncludeLastFailure = true,
            });
            Assert.Equal("nexus-value", await description.GetResultAsync<string>());
            Assert.IsType<ApplicationFailureException>(await description.GetLastFailureAsync());
            Assert.Equal(
                SigningRecordingCodec.Signature(context),
                SigningRecordingCodec.SignatureOf(description.RawInfo.HeartbeatDetails.Payloads_.First()));

            var raw = await client.Connection.WorkflowService.DescribeActivityExecutionAsync(new()
            {
                Namespace = client.Options.Namespace,
                ActivityId = activityId,
                IncludeInput = true,
                IncludeOutcome = true,
            });
            Assert.Equal(
                SigningRecordingCodec.Signature(context),
                SigningRecordingCodec.SignatureOf(raw.Outcome.Result.Payloads_.First()));
            // The input keeps the activity context
            Assert.All(raw.Input.Payloads_, p => Assert.Null(SigningRecordingCodec.SignatureOf(p)));
        });
    }

    [Fact]
    public async Task Activity_StartedByNexusHandler_FailsWithTheNexusContext()
    {
        await RunAsync(nameof(INexusStartedService.StartActivity), async (client, nexusClient, context, _) =>
        {
            var activityId = $"nexus-started-activity-{Guid.NewGuid()}";
            await Assert.ThrowsAsync<NexusOperationFailedException>(() =>
                nexusClient.ExecuteNexusOperationAsync(
                    svc => svc.StartActivity(new(activityId, string.Empty, Outcome.Failure)),
                    OperationOptions()));

            var handle = client.GetActivityHandle<string>(activityId);
            var exc = await Assert.ThrowsAsync<ActivityFailedException>(() => handle.GetResultAsync());
            var appFailure = Assert.IsType<ApplicationFailureException>(exc.InnerException);
            Assert.Equal("failure-detail", appFailure.Details.ElementAt<string>(0));
            var description = await handle.DescribeAsync(new() { IncludeOutcome = true });
            var outcomeFailure = Assert.IsType<ApplicationFailureException>(
                await description.GetOutcomeFailureAsync());
            Assert.Equal("failure-detail", outcomeFailure.Details.ElementAt<string>(0));
        });
    }

    [Fact]
    public async Task Activity_StartedByNexusHandler_CompletesAsynchronouslyWithTheNexusContext()
    {
        await RunAsync(nameof(INexusStartedService.StartActivity), async (client, nexusClient, context, _) =>
        {
            var activityId = $"nexus-started-activity-{Guid.NewGuid()}";
            var operation = await nexusClient.StartNexusOperationAsync(
                svc => svc.StartActivity(new(activityId, string.Empty, Outcome.Async)),
                OperationOptions());
            var (taskToken, infoContext) = await AsyncCompletions.GetOrAdd(activityId, _ => new()).Task;
            Assert.Equal(context, infoContext);

            var asyncHandle = client.GetAsyncActivityHandle(taskToken).WithSerializationContext(infoContext!);
            await asyncHandle.HeartbeatAsync(new() { Details = new[] { "client-heartbeat" } });
            var description = await client.GetActivityHandle(activityId).DescribeAsync(
                new() { IncludeHeartbeatDetails = true });
            Assert.Equal(
                SigningRecordingCodec.Signature(context),
                SigningRecordingCodec.SignatureOf(description.RawInfo.HeartbeatDetails.Payloads_.First()));

            await asyncHandle.CompleteAsync("async-value");
            Assert.Equal("async-value", await operation.GetResultAsync());
            Assert.Equal("async-value", await client.GetActivityHandle<string>(activityId).GetResultAsync());
        });
    }

    [Theory]
    [InlineData(Outcome.Result)]
    [InlineData(Outcome.Failure)]
    [InlineData(Outcome.Rejection)]
    public async Task Update_SentByNexusHandler_UsesTheNexusContext(Outcome outcome)
    {
        await RunAsync(nameof(INexusStartedService.UpdateWorkflow), async (client, nexusClient, context, taskQueue) =>
        {
            var handle = await client.StartWorkflowAsync(
                (NexusUpdatedWorkflow wf) => wf.RunAsync(),
                new($"nexus-updated-workflow-{Guid.NewGuid()}", taskQueue));
            var updateId = Guid.NewGuid().ToString();
            var input = new NexusUpdateInput(handle.Id, updateId, outcome);
            if (outcome == Outcome.Result)
            {
                Assert.Equal(
                    "nexus-value",
                    await nexusClient.ExecuteNexusOperationAsync(
                        svc => svc.UpdateWorkflow(input), OperationOptions()));
            }
            else
            {
                await Assert.ThrowsAsync<NexusOperationFailedException>(() =>
                    nexusClient.ExecuteNexusOperationAsync(svc => svc.UpdateWorkflow(input), OperationOptions()));
            }

            if (outcome == Outcome.Rejection)
            {
                // Rejections are not stored in history, so check the one the handler received
                Assert.Equal(SigningRecordingCodec.Signature(context), UpdateOutcomeFailureSignatures[updateId]);
            }
            else
            {
                // This handle never sent the update, so it relies on the context the server returns
                var updateHandle = handle.GetUpdateHandle<string>(updateId);
                if (outcome == Outcome.Result)
                {
                    Assert.Equal("nexus-value", await updateHandle.GetResultAsync());
                }
                else
                {
                    var exc = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                        () => updateHandle.GetResultAsync());
                    var appFailure = Assert.IsType<ApplicationFailureException>(exc.InnerException);
                    Assert.Equal("failure-detail", appFailure.Details.ElementAt<string>(0));
                }
            }

            await handle.SignalAsync(wf => wf.FinishAsync());
            await handle.GetResultAsync();
            var history = await handle.FetchHistoryAsync();
            if (outcome != Outcome.Rejection)
            {
                var accepted = history.Events.Single(
                    e => e.WorkflowExecutionUpdateAcceptedEventAttributes != null).
                    WorkflowExecutionUpdateAcceptedEventAttributes;
                Assert.Equal(
                    context,
                    ISerializationContext.Nexus.FromProto(
                        accepted.AcceptedRequest.PropagatedNexusSerializationContext));
                // The input keeps the workflow context
                Assert.All(
                    accepted.AcceptedRequest.Input.Args.Payloads_,
                    p => Assert.Null(SigningRecordingCodec.SignatureOf(p)));
                var completed = history.Events.Single(
                    e => e.WorkflowExecutionUpdateCompletedEventAttributes != null).
                    WorkflowExecutionUpdateCompletedEventAttributes;
                var outcomePayload = outcome == Outcome.Result ?
                    completed.Outcome.Success.Payloads_.First() :
                    completed.Outcome.Failure.ApplicationFailureInfo.Details.Payloads_.First();
                Assert.Equal(
                    SigningRecordingCodec.Signature(context), SigningRecordingCodec.SignatureOf(outcomePayload));
            }

            // Replaying re-runs the update with the context from history
            var replay = await new WorkflowReplayer(
                new WorkflowReplayerOptions() { DataConverter = client.Options.DataConverter }.
                    AddWorkflow<NexusUpdatedWorkflow>()).ReplayWorkflowAsync(history);
            Assert.Null(replay.ReplayFailure);
        });
    }

    private static NexusOperationOptions OperationOptions() =>
        new($"op-{Guid.NewGuid()}") { ScheduleToCloseTimeout = TimeSpan.FromMinutes(1) };

    private async Task RunAsync(
        string operation,
        Func<ITemporalClient, NexusClient<INexusStartedService>, ISerializationContext.Nexus, string, Task> testFunc)
    {
        // The worker and the clients reading the outcome share the codec, so a payload decoded
        // under a different context than encoded it fails, the way a codec keyed on the context
        // would
        var clientOptions = (TemporalClientOptions)Client.Options.Clone();
        clientOptions.DataConverter = DataConverter.Default with { PayloadCodec = new SigningRecordingCodec() };
        clientOptions.Interceptors = new[] { new UpdateOutcomeRecorder() };
        var client = new TemporalClient(Client.Connection, clientOptions);

        var taskQueue = $"tq-{Guid.NewGuid()}";
        var endpoint = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpoint, taskQueue);

        using var worker = new TemporalWorker(
            client,
            new TemporalWorkerOptions(taskQueue).
                AddNexusService(new NexusStartedServiceHandler()).
                AddWorkflow<NexusStartedWorkflow>().
                AddWorkflow<NexusUpdatedWorkflow>().
                AddActivity(NexusStartedActivities.RunAsync));
        await worker.ExecuteAsync(() => testFunc(
            client,
            client.CreateNexusClient<INexusStartedService>(endpoint),
            new(endpoint, "NexusStartedService", operation),
            taskQueue));
    }

    private sealed class UpdateOutcomeRecorder : IClientInterceptor
    {
        public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor nextInterceptor) =>
            new Outbound(nextInterceptor);

        private sealed class Outbound : ClientOutboundInterceptor
        {
            internal Outbound(ClientOutboundInterceptor next)
                : base(next)
            {
            }

            public override async Task<WorkflowUpdateHandle<TResult>> StartWorkflowUpdateAsync<TResult>(
                StartWorkflowUpdateInput input)
            {
                var handle = await base.StartWorkflowUpdateAsync<TResult>(input);
                if (handle.KnownOutcome?.Failure is { } failure)
                {
                    UpdateOutcomeFailureSignatures[handle.Id] = SigningRecordingCodec.SignatureOf(
                        failure.ApplicationFailureInfo?.Details?.Payloads_.FirstOrDefault());
                }
                return handle;
            }
        }
    }
}
