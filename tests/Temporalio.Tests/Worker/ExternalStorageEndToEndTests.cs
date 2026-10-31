namespace Temporalio.Tests.Worker;

using System.Collections.Concurrent;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Activities;
using Temporalio.Api.Common.V1;
using Temporalio.Api.History.V1;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class ExternalStorageEndToEndTests : WorkflowEnvironmentTestBase
{
    public ExternalStorageEndToEndTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Workflow]
    public class ResultWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string input) => $"result: {input}";
    }

    [Workflow]
    public class ActivityWorkflow
    {
        [Activity]
        public static string Echo(string input) => $"activity: {input}";

        [WorkflowRun]
        public async Task<string> RunAsync(string input) =>
            await Workflow.ExecuteActivityAsync(
                () => Echo(input),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
    }

    [Workflow]
    public class SignalQueryWorkflow
    {
        private string? received;

        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            await Workflow.WaitConditionAsync(() => received != null);
            return received!;
        }

        [WorkflowSignal]
        public async Task SubmitAsync(string value) => received = value;

        [WorkflowQuery]
        public string Echo(string input) => $"query: {input}";
    }

    [Workflow]
    public class ChildWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string input) => $"child: {input}";
    }

    [Workflow]
    public class ParentWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string input) =>
            await Workflow.ExecuteChildWorkflowAsync((ChildWorkflow wf) => wf.RunAsync(input));
    }

    [Workflow]
    public class ContinueWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string input)
        {
            if (Workflow.Info.ContinuedRunId != null)
            {
                return $"continued: {input}";
            }

            throw Workflow.CreateContinueAsNewException((ContinueWorkflow wf) => wf.RunAsync(input));
        }
    }

    [Workflow]
    public class FailureWorkflow
    {
        [WorkflowRun]
        public Task RunAsync(string detail) =>
            throw new ApplicationFailureException(
                "boom", errorType: "MyError", details: new object?[] { detail });
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
        [WorkflowRun]
        public async Task<string> RunAsync(string endpoint, string input) =>
            await Workflow.CreateNexusWorkflowClient<IEchoService>(endpoint)
                .ExecuteNexusOperationAsync(svc => svc.Echo(input));
    }

    [Fact]
    public async Task CompletionResult_IsOffloadedAndRetrieved()
    {
        var client = ClientWithExternalStorage();
        await ExecuteWorkerAsync<ResultWorkflow>(client, async worker =>
        {
            var handle = await client.StartWorkflowAsync(
                (ResultWorkflow wf) => wf.RunAsync("hi"), NewOptions(worker));
            Assert.Equal("result: hi", await handle.GetResultAsync());

            var completed = await FirstEventAsync(
                handle, e => e.WorkflowExecutionCompletedEventAttributes != null);
            AssertReference(completed.WorkflowExecutionCompletedEventAttributes.Result);
        });
    }

    [Fact]
    public async Task ActivityArgsAndResult_AreOffloadedAndRetrieved()
    {
        var client = ClientWithExternalStorage();
        await ExecuteWorkerAsync<ActivityWorkflow>(
            client,
            async worker =>
            {
                var handle = await client.StartWorkflowAsync(
                    (ActivityWorkflow wf) => wf.RunAsync("hi"), NewOptions(worker));
                Assert.Equal("activity: hi", await handle.GetResultAsync());

                // The arg is offloaded on the scheduling completion; the result on the activity side.
                var scheduled = await FirstEventAsync(
                    handle, e => e.ActivityTaskScheduledEventAttributes != null);
                AssertReference(scheduled.ActivityTaskScheduledEventAttributes.Input);
                var completed = await FirstEventAsync(
                    handle, e => e.ActivityTaskCompletedEventAttributes != null);
                AssertReference(completed.ActivityTaskCompletedEventAttributes.Result);
            },
            new TemporalWorkerOptions().AddActivity(ActivityWorkflow.Echo));
    }

    [Fact]
    public async Task SignalAndQuery_ArgsAndResultsRoundTrip()
    {
        var client = ClientWithExternalStorage();
        await ExecuteWorkerAsync<SignalQueryWorkflow>(client, async worker =>
        {
            var handle = await client.StartWorkflowAsync(
                (SignalQueryWorkflow wf) => wf.RunAsync(), NewOptions(worker));

            // The query arg is retrieved inbound and its result round-trips back.
            Assert.Equal("query: q", await handle.QueryAsync(wf => wf.Echo("q")));

            await handle.SignalAsync(wf => wf.SubmitAsync("signaled"));
            Assert.Equal("signaled", await handle.GetResultAsync());

            var signaled = await FirstEventAsync(
                handle, e => e.WorkflowExecutionSignaledEventAttributes != null);
            AssertReference(signaled.WorkflowExecutionSignaledEventAttributes.Input);
        });
    }

    [Fact]
    public async Task ChildWorkflowArgsAndResult_RoundTrip()
    {
        var client = ClientWithExternalStorage();
        await ExecuteWorkerAsync<ParentWorkflow>(
            client,
            async worker =>
            {
                var handle = await client.StartWorkflowAsync(
                    (ParentWorkflow wf) => wf.RunAsync("hi"), NewOptions(worker));
                Assert.Equal("child: hi", await handle.GetResultAsync());

                var initiated = await FirstEventAsync(
                    handle, e => e.StartChildWorkflowExecutionInitiatedEventAttributes != null);
                AssertReference(initiated.StartChildWorkflowExecutionInitiatedEventAttributes.Input);
            },
            new TemporalWorkerOptions().AddWorkflow<ChildWorkflow>());
    }

    [Fact]
    public async Task ContinueAsNewArg_IsOffloadedAndRetrieved()
    {
        var client = ClientWithExternalStorage();
        await ExecuteWorkerAsync<ContinueWorkflow>(client, async worker =>
        {
            var handle = await client.StartWorkflowAsync(
                (ContinueWorkflow wf) => wf.RunAsync("hi"), NewOptions(worker));
            Assert.Equal("continued: hi", await handle.GetResultAsync());

            var firstRun = handle with { RunId = handle.FirstExecutionRunId };
            var continued = await FirstEventAsync(
                firstRun, e => e.WorkflowExecutionContinuedAsNewEventAttributes != null);
            AssertReference(continued.WorkflowExecutionContinuedAsNewEventAttributes.Input);
        });
    }

    [Fact]
    public async Task FailureDetail_IsOffloadedAndRetrieved()
    {
        var client = ClientWithExternalStorage();
        await ExecuteWorkerAsync<FailureWorkflow>(client, async worker =>
        {
            var handle = await client.StartWorkflowAsync(
                (FailureWorkflow wf) => wf.RunAsync("detail"), NewOptions(worker));

            var exception = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => handle.GetResultAsync());
            var application = Assert.IsType<ApplicationFailureException>(exception.InnerException);
            Assert.Equal("detail", application.Details.ElementAt<string>(0));

            var failed = await FirstEventAsync(
                handle, e => e.WorkflowExecutionFailedEventAttributes != null);
            AssertReference(
                failed.WorkflowExecutionFailedEventAttributes.Failure.ApplicationFailureInfo.Details);
        });
    }

    [Fact]
    [CloudTestExclusion(
        CloudTestExclusionReason.NeedsCloudAdaptation,
        "Requires Cloud Nexus endpoint setup and cleanup.")]
    public async Task NexusOperationArgAndResult_AreOffloadedAndRetrieved()
    {
        var client = ClientWithExternalStorage();
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var endpoint = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpoint, taskQueue);

        var options = new TemporalWorkerOptions(taskQueue)
            .AddNexusService(new EchoService())
            .AddWorkflow<NexusCallerWorkflow>();
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(client, options);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await client.StartWorkflowAsync(
                (NexusCallerWorkflow wf) => wf.RunAsync(endpoint, "hi"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: taskQueue));
            Assert.Equal("nexus: hi", await handle.GetResultAsync());

            // The arg is offloaded on the caller's completion; the result on the handler side.
            var scheduled = await FirstEventAsync(
                handle, e => e.NexusOperationScheduledEventAttributes != null);
            AssertReference(scheduled.NexusOperationScheduledEventAttributes.Input);
            var completed = await FirstEventAsync(
                handle, e => e.NexusOperationCompletedEventAttributes != null);
            AssertReference(completed.NexusOperationCompletedEventAttributes.Result);
        });
    }

    [Fact]
    public async Task ClientStartInput_IsOffloadedByClient()
    {
        var client = ClientWithExternalStorage();

        // No worker polls this task queue, so only the client could have offloaded the input.
        var handle = await client.StartWorkflowAsync(
            (ResultWorkflow wf) => wf.RunAsync("hi"),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: $"tq-{Guid.NewGuid()}"));

        // No worker will complete this, so terminate it even if an assertion fails.
        try
        {
            var started = await FirstEventAsync(
                handle, e => e.WorkflowExecutionStartedEventAttributes != null);
            AssertReference(started.WorkflowExecutionStartedEventAttributes.Input);
        }
        finally
        {
            await handle.TerminateAsync();
        }
    }

    [Fact]
    public async Task ClientSignalInput_IsOffloadedByClient()
    {
        var client = ClientWithExternalStorage();
        var handle = await client.StartWorkflowAsync(
            (SignalQueryWorkflow wf) => wf.RunAsync(),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: $"tq-{Guid.NewGuid()}"));

        try
        {
            await handle.SignalAsync(wf => wf.SubmitAsync("signaled"));

            var signaled = await FirstEventAsync(
                handle, e => e.WorkflowExecutionSignaledEventAttributes != null);
            AssertReference(signaled.WorkflowExecutionSignaledEventAttributes.Input);
        }
        finally
        {
            await handle.TerminateAsync();
        }
    }

    [Fact]
    public async Task ClientMemo_IsOffloadedAndRetrievedByClient()
    {
        var client = ClientWithExternalStorage();
        var handle = await client.StartWorkflowAsync(
            (ResultWorkflow wf) => wf.RunAsync("hi"),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: $"tq-{Guid.NewGuid()}")
            {
                Memo = new Dictionary<string, object> { ["key"] = "memo-value" },
            });

        try
        {
            // Memos are offloaded like any other payload above the threshold.
            var started = await FirstEventAsync(
                handle, e => e.WorkflowExecutionStartedEventAttributes != null);
            AssertReference(started.WorkflowExecutionStartedEventAttributes.Memo.Fields["key"]);

            // Describe retrieves the memo back through the client.
            var description = await handle.DescribeAsync();
            Assert.Equal("memo-value", await description.Memo["key"].ToValueAsync<string>());
        }
        finally
        {
            await handle.TerminateAsync();
        }
    }

    private static WorkflowOptions NewOptions(TemporalWorker worker) =>
        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!);

    private static async Task<HistoryEvent> FirstEventAsync(
        WorkflowHandle handle, Func<HistoryEvent, bool> predicate)
    {
        await foreach (var evt in handle.FetchHistoryEventsAsync())
        {
            if (predicate(evt))
            {
                return evt;
            }
        }

        throw new InvalidOperationException("No matching history event found.");
    }

    private static void AssertReference(Payloads payloads) =>
        Assert.True(ExternalStorageReferences.IsPlainReference(Assert.Single(payloads.Payloads_)));

    private static void AssertReference(Payload payload) =>
        Assert.True(ExternalStorageReferences.IsPlainReference(payload));

    private TemporalClient ClientWithExternalStorage()
    {
        var options = (TemporalClientOptions)Client.Options.Clone();
        options.DataConverter = DataConverter.Default with
        {
            ExternalStorage =
                new ExternalStorage(new InMemoryStorageDriver("mem")) { PayloadSizeThreshold = 1 },
        };
        return new TemporalClient(Client.Connection, options);
    }

    private async Task ExecuteWorkerAsync<TWorkflow>(
        ITemporalClient client,
        Func<TemporalWorker, Task> action,
        TemporalWorkerOptions? options = null)
    {
        options ??= new();
        options.TaskQueue ??= $"tq-{Guid.NewGuid()}";
        options.AddWorkflow<TWorkflow>();
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(client, options);
        await worker.ExecuteAsync(() => action(worker));
    }

    private sealed class InMemoryStorageDriver : IStorageDriver
    {
        // Worker and client reach this concurrently, so it is thread-safe.
        private readonly ConcurrentDictionary<string, Payload> stored = new();

        public InMemoryStorageDriver(string name) => Name = name;

        public string Name { get; }

        public string Type => "test.inmemory";

        public Task<IReadOnlyCollection<StorageDriverClaim>> StoreAsync(
            StorageDriverStoreContext context,
            IReadOnlyCollection<Payload> payloads,
            CancellationToken cancellationToken)
        {
            var claims = new List<StorageDriverClaim>();
            foreach (var payload in payloads)
            {
                var key = Guid.NewGuid().ToString();
                stored[key] = payload;
                claims.Add(new StorageDriverClaim(new Dictionary<string, string> { ["key"] = key }));
            }

            return Task.FromResult<IReadOnlyCollection<StorageDriverClaim>>(claims);
        }

        public Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            StorageDriverRetrieveContext context,
            IReadOnlyCollection<StorageDriverClaim> claims,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(
                claims.Select(c => stored[c.ClaimData["key"]]).ToList());
    }
}
