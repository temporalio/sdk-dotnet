namespace Temporalio.Tests.Worker;

using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Activities;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

// A failing driver makes every store and retrieve throw, so the worker must report the failure as a
// retryable task failure with the external storage cause rather than failing the execution.
public class ExternalStorageFailureTests : WorkflowEnvironmentTestBase
{
    public ExternalStorageFailureTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Workflow]
    public class ResultWorkflow
    {
        // No arguments, so the client offloads nothing on start; only the worker's completion, which
        // carries the result, reaches the failing driver.
        [WorkflowRun]
        public async Task<string> RunAsync() => "result";
    }

    [Workflow]
    public class ActivityWorkflow
    {
        [Activity]
        public static string Echo() => "activity-result";

        // One attempt, so the activity closes out as failed rather than retrying forever when its
        // result cannot be stored.
        [WorkflowRun]
        public async Task<string> RunAsync() =>
            await Workflow.ExecuteActivityAsync(
                () => Echo(),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(10),
                    RetryPolicy = new() { MaximumAttempts = 1 },
                });
    }

    [NexusService]
    public interface IEchoService
    {
        [NexusOperation]
        string Echo(string input);
    }

    [NexusServiceHandler(typeof(IEchoService))]
    public class EchoService
    {
        [NexusOperationHandler]
        public IOperationHandler<string, string> Echo() =>
            OperationHandler.Sync<string, string>((context, input) => $"nexus: {input}");
    }

    [Workflow]
    public class NexusCallerWorkflow
    {
        // A long schedule-to-close so the operation stays pending and keeps retrying while the test
        // reads its last-attempt failure, rather than timing out and erasing the cause.
        [WorkflowRun]
        public async Task<string> RunAsync(string endpoint) =>
            await Workflow.CreateNexusWorkflowClient<IEchoService>(endpoint)
                .ExecuteNexusOperationAsync(
                    svc => svc.Echo("hi"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    [Fact]
    public async Task CompletionStoreFailure_FailsWorkflowTaskWithExternalStorageCause()
    {
        var client = ClientWithFailingStorage();
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var options = new TemporalWorkerOptions(taskQueue).AddWorkflow<ResultWorkflow>();
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(client, options);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await client.StartWorkflowAsync(
                (ResultWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: taskQueue));
            try
            {
                // The completion carrying the result cannot be stored, so the task fails and is
                // retried under the external storage cause rather than failing the workflow.
                var failed = await WaitForEventAsync(
                    handle, e => e.WorkflowTaskFailedEventAttributes != null);
                Assert.Equal(
                    WorkflowTaskFailedCause.ExternalStorageFailure,
                    failed.WorkflowTaskFailedEventAttributes.Cause);
            }
            finally
            {
                await TerminateQuietlyAsync(handle);
            }
        });
    }

    [Fact]
    public async Task ActivityResultStoreFailure_FailsActivityTaskWithExternalStorageCause()
    {
        var client = ClientWithFailingStorage();
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var options = new TemporalWorkerOptions(taskQueue)
            .AddWorkflow<ActivityWorkflow>()
            .AddActivity(ActivityWorkflow.Echo);
        using var worker = new TemporalWorker(client, options);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await client.StartWorkflowAsync(
                (ActivityWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: taskQueue));
            try
            {
                // The activity runs, but its result cannot be stored, so the task fails and gives up
                // after its single attempt. The server records the failure the worker sent, which
                // carries the external storage exception type. The terminal event does not surface
                // the per-attempt bridge cause, so the failure type is what proves the storage path.
                var failed = await WaitForEventAsync(
                    handle, e => e.ActivityTaskFailedEventAttributes != null);
                Assert.Equal(
                    nameof(Temporalio.Exceptions.ExternalStorageException),
                    failed.ActivityTaskFailedEventAttributes.Failure.ApplicationFailureInfo.Type);
            }
            finally
            {
                await TerminateQuietlyAsync(handle);
            }
        });
    }

    [Fact]
    [CloudTestExclusion(
        CloudTestExclusionReason.NeedsCloudAdaptation,
        "Creates a local Nexus endpoint bound to the handler worker's task queue.")]
    public async Task NexusResultStoreFailure_FailsOperationHandler()
    {
        // The handler runs on a worker whose storage fails; the caller workflow runs on a separate
        // worker with working storage. So only the handler's result offload fails, exercising the
        // Nexus handler-error path without the caller's own completion also failing to store.
        var handlerClient = ClientWithFailingStorage();
        var handlerTaskQueue = $"tq-handler-{Guid.NewGuid()}";
        var callerTaskQueue = $"tq-caller-{Guid.NewGuid()}";
        var endpoint = $"nexus-endpoint-{handlerTaskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpoint, handlerTaskQueue);

        var handlerOptions = new TemporalWorkerOptions(handlerTaskQueue)
            .AddNexusService(new EchoService());
        var callerOptions = new TemporalWorkerOptions(callerTaskQueue)
            .AddWorkflow<NexusCallerWorkflow>();
        callerOptions.Interceptors ??= new[] { new XunitExceptionInterceptor() };

        using var handlerWorker = new TemporalWorker(handlerClient, handlerOptions);
        using var callerWorker = new TemporalWorker((TemporalClient)Client, callerOptions);
        await handlerWorker.ExecuteAsync(() => callerWorker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (NexusCallerWorkflow wf) => wf.RunAsync(endpoint),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: callerTaskQueue));
            try
            {
                // Each attempt fails because the result cannot be stored, so the operation stays
                // pending and retrying; its last-attempt failure names external storage as the cause.
                var failure = await WaitForNexusOperationFailureAsync(handle);
                Assert.Equal("External storage failure", failure.Message);
                Assert.Equal(
                    nameof(Temporalio.Exceptions.ExternalStorageException),
                    failure.Cause.ApplicationFailureInfo.Type);
            }
            finally
            {
                await TerminateQuietlyAsync(handle);
            }
        }));
    }

    // Cleanup for a workflow that may still be retrying its unstorable completion, or may have
    // already closed out on its own; either way a leftover workflow should not fail the test.
    private static async Task TerminateQuietlyAsync(WorkflowHandle handle)
    {
        try
        {
            await handle.TerminateAsync();
        }
        catch (Temporalio.Exceptions.RpcException)
        {
        }
    }

    // Polls the workflow's pending Nexus operations for a recorded last-attempt failure, which the
    // server keeps while the operation retries. RawDescription carries the pending-operation info the
    // typed description does not surface.
    private static async Task<Api.Failure.V1.Failure> WaitForNexusOperationFailureAsync(
        WorkflowHandle handle)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var description = await handle.DescribeAsync();
            foreach (var op in description.RawDescription.PendingNexusOperations)
            {
                if (op.LastAttemptFailure != null)
                {
                    return op.LastAttemptFailure;
                }
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException("Nexus operation failure did not appear in time.");
    }

    private static async Task<HistoryEvent> WaitForEventAsync(
        WorkflowHandle handle, Func<HistoryEvent, bool> predicate)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                if (predicate(evt))
                {
                    return evt;
                }
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException("Expected history event did not appear in time.");
    }

    private TemporalClient ClientWithFailingStorage()
    {
        var options = (TemporalClientOptions)Client.Options.Clone();
        options.DataConverter = DataConverter.Default with
        {
            ExternalStorage =
                new ExternalStorage(new FailingStorageDriver("mem")) { PayloadSizeThreshold = 1 },
        };
        return new TemporalClient(Client.Connection, options);
    }

    private sealed class FailingStorageDriver : IStorageDriver
    {
        public FailingStorageDriver(string name) => Name = name;

        public string Name { get; }

        public string Type => "test.failing";

        public Task<IReadOnlyCollection<StorageDriverClaim>> StoreAsync(
            StorageDriverStoreContext context,
            IReadOnlyCollection<Payload> payloads,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Storage is unavailable.");

        public Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            StorageDriverRetrieveContext context,
            IReadOnlyCollection<StorageDriverClaim> claims,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Storage is unavailable.");
    }
}
