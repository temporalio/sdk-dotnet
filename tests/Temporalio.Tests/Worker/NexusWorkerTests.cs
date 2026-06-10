namespace Temporalio.Tests.Worker;

using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Nexus;
using Temporalio.Worker;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class NexusWorkerTests : WorkflowEnvironmentTestBase
{
    public NexusWorkerTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [NexusService]
    public interface IStringService
    {
        [NexusOperation]
        string DoSomething(string name);
    }

    [NexusServiceHandler(typeof(IStringService))]
    public class HandlerFactoryStringService
    {
        private readonly Func<IOperationHandler<string, string>> handlerFactory;

        public HandlerFactoryStringService(Func<IOperationHandler<string, string>> handlerFactory) =>
            this.handlerFactory = handlerFactory;

        [NexusOperationHandler]
        public IOperationHandler<string, string> DoSomething() => handlerFactory();
    }

    [NexusServiceHandler(typeof(IStringService))]
    public class AsyncFuncStringService
    {
        private readonly AsyncFuncOperationHandler handler;

        public AsyncFuncStringService(
            Func<OperationStartContext, string, Task<OperationStartResult<string>>> start,
            Func<OperationCancelContext, Task>? cancel = null) =>
            handler = new(start, cancel);

        [NexusOperationHandler]
        public IOperationHandler<string, string> DoSomething() => handler;

        private class AsyncFuncOperationHandler : IOperationHandler<string, string>
        {
            private readonly Func<OperationStartContext, string, Task<OperationStartResult<string>>> start;
            private readonly Func<OperationCancelContext, Task>? cancel;

            public AsyncFuncOperationHandler(
                Func<OperationStartContext, string, Task<OperationStartResult<string>>> start,
                Func<OperationCancelContext, Task>? cancel)
            {
                this.start = start;
                this.cancel = cancel;
            }

            public Task<OperationStartResult<string>> StartAsync(OperationStartContext context, string input) =>
                start(context, input);

            public Task CancelAsync(OperationCancelContext context) =>
                cancel is { } cancelFunc ? cancelFunc(context) : throw new NotImplementedException();
        }
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_SimpleService_Succeeds()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>((ctx, name) => $"Hello, {name}")));
        var endpointName = $"nexus-endpoint-{workerOptions.TaskQueue}";
        var endpoint = await Env.TestEnv.CreateNexusEndpointAsync(
            endpointName, workerOptions.TaskQueue!);
        try
        {
            await RunInWorkflowAsync(workerOptions, async () =>
            {
                var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpointName).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
                Assert.Equal("Hello, some-name", result);
            });
        }
        finally
        {
            // We'll delete the endpoint on just this test to confirm that works
            await Env.TestEnv.DeleteNexusEndpointAsync(endpoint);
        }
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_ContextHasEndpoint()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>((ctx, name) =>
                    NexusOperationExecutionContext.Current.Info.Endpoint)));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        await RunInWorkflowAsync(workerOptions, async () =>
        {
            var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
            Assert.Equal(endpoint, result);
        });
    }

    [Workflow]
    public class SimpleWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string name) => $"Hello from workflow, {name}";
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_SimpleWorkflow_Succeeds()
    {
        WorkflowRunOperationContext? capturedContext = null;
        NexusWorkflowRunHandle<string>? capturedRunHandle = null;
        // Build the worker options w/ the nexus service
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                WorkflowRunOperationHandler.FromHandleFactory(
                    async (WorkflowRunOperationContext context, string input) =>
                    {
                        capturedContext = context;
                        capturedRunHandle = await context.StartWorkflowAsync(
                            (SimpleWorkflow wf) => wf.RunAsync(input),
                            new() { Id = $"wf-{Guid.NewGuid()}" });
                        return capturedRunHandle;
                    }))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        // Run the Nexus client code in workflow
        var handle = await RunInWorkflowAsync(workerOptions, async () =>
        {
            var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
            Assert.Equal("Hello from workflow, some-name", result);
        });

        // Check the Nexus context is as expected
        Assert.Equal("StringService", capturedContext!.HandlerContext.Service);
        Assert.Equal("DoSomething", capturedContext.HandlerContext.Operation);
        Assert.True(Guid.TryParse(capturedContext.HandlerContext.RequestId, out _));
        Assert.False(string.IsNullOrEmpty(capturedContext.HandlerContext.CallbackUrl));
        var wfEvent = Assert.Single(capturedContext.HandlerContext.InboundLinks).ToWorkflowEvent();
        Assert.Equal(handle.Id, wfEvent.WorkflowId);
        Assert.Equal(handle.ResultRunId, wfEvent.RunId);
        Assert.Equal(Api.Enums.V1.EventType.NexusOperationScheduled, wfEvent.EventRef.EventType);

        // Get the operation started event and check the link points to the started workflow
        var startEvent = Assert.Single(
            (await handle.FetchHistoryAsync()).Events,
            evt => evt.NexusOperationStartedEventAttributes != null);
        var link = Assert.Single(startEvent.Links);
        Assert.Equal(1, link.WorkflowEvent.EventRef.EventId);
        Assert.Equal(Api.Enums.V1.EventType.WorkflowExecutionStarted, link.WorkflowEvent.EventRef.EventType);
        Assert.Equal(capturedRunHandle!.WorkflowId, link.WorkflowEvent.WorkflowId);
    }

    [Workflow]
    public class WaitForeverWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string name)
        {
            await Workflow.WaitConditionAsync(() => false);
            return "never-reached";
        }
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_WaitForeverWorkflow_CanBeCanceled()
    {
        // Build the worker options w/ the nexus service
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                WorkflowRunOperationHandler.FromHandleFactory(
                    (WorkflowRunOperationContext context, string input) =>
                        context.StartWorkflowAsync(
                            (WaitForeverWorkflow wf) => wf.RunAsync(input),
                            new() { Id = $"wf-{Guid.NewGuid()}" })))).
            AddWorkflow<WaitForeverWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        // Run the Nexus client code in workflow, then cancel the whole workflow and confirm it
        // has expected exceptions
        var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() => RunInWorkflowAsync(
            workerOptions,
            async () =>
            {
                var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
                Assert.Equal("Hello from workflow, some-name", result);
            },
            beforeGetResultFunc: async handle =>
            {
                // Wait for Nexus operation to get started
                await AssertMore.HasEventEventuallyAsync(
                    handle, evt => evt.NexusOperationStartedEventAttributes != null);
                // Now cancel entire workflow
                await handle.CancelAsync();
            }));
        Assert.IsType<CanceledFailureException>(wfExc.InnerException);

        // Do the same thing, but instead of cancelling whole workflow, we will cancel just the
        // operation
        wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() => RunInWorkflowAsync(
            workerOptions,
            async () =>
            {
                // Start with cancel token
                using var cancelSource = new CancellationTokenSource();
                var handle = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    StartNexusOperationAsync(
                        svc => svc.DoSomething("some-name"),
                        new() { CancellationToken = cancelSource.Token });
                // Cancel and wait for result to bubble out cancel
#pragma warning disable CA1849 // Call async methods when in an async method
#pragma warning disable VSTHRD103 // Call async methods when in an async method
                // https://github.com/temporalio/sdk-dotnet/issues/327
                cancelSource.Cancel();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
#pragma warning restore CA1849 // Call async methods when in an async method
                await handle.GetResultAsync();
            }));
        var inner = Assert.IsType<NexusOperationFailureException>(wfExc.InnerException);
        Assert.Equal(endpoint, inner.Endpoint);
        Assert.Equal("StringService", inner.Service);
        Assert.Equal("DoSomething", inner.Operation);
        Assert.IsType<CanceledFailureException>(inner.InnerException);

        // Also check the token
        var token = NexusWorkflowRunHandle<string>.FromToken(inner.OperationToken);
        Assert.Equal(Env.Client.Options.Namespace, token.Namespace);
        Assert.IsType<CanceledFailureException>(
            (await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                Client.GetWorkflowHandle(token.WorkflowId).GetResultAsync())).InnerException);
    }

    [NexusService]
    public interface IBadService
    {
        [NexusOperation]
        int DoSomething(string name);
    }

    [NexusServiceHandler(typeof(IBadService))]
    public class BadService
    {
        [NexusOperationHandler]
        public IOperationHandler<string, string> DoSomething() =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_BadService_FailsRegistration()
    {
        var exc = Assert.Throws<ArgumentException>(() => new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddNexusService(new BadService())));
        Assert.Equal("Failed obtaining operation handler from DoSomething", exc.Message);
        Assert.Equal(
            "Expected return type of IOperationHandler<String, Int32>",
            Assert.IsType<ArgumentException>(exc.InnerException).Message);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_SyncTimeout_FailsAsExpected()
    {
        var cancellationReasonSource = new TaskCompletionSource<string?>();
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (ctx, name) =>
                {
                    try
                    {
                        await Task.Delay(40000, ctx.CancellationToken);
                        cancellationReasonSource.SetResult("none");
                        return "done";
                    }
                    catch (TaskCanceledException)
                    {
                        cancellationReasonSource.SetResult(ctx.CancellationReason);
                        return "canceled";
                    }
                    catch (Exception)
                    {
                        cancellationReasonSource.SetResult("other exception");
                        throw;
                    }
                })));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // Confirm the workflow fails with the timeout
        var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, async () =>
            {
                await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(
                        svc => svc.DoSomething("some-name"),
                        new() { ScheduleToCloseTimeout = TimeSpan.FromSeconds(4) });
            }));
        var timeoutExc = Assert.IsType<TimeoutFailureException>(
            Assert.IsType<NexusOperationFailureException>(exc.InnerException).InnerException);
        Assert.Equal(TimeoutType.ScheduleToClose, timeoutExc.TimeoutType);
        // Check that it is cancelled for the proper reason
        using CancellationTokenSource timeoutSource = new(TimeSpan.FromSeconds(5));
        var reason = await cancellationReasonSource.Task.WaitAsync(timeoutSource.Token);
        Assert.Equal("timed out", reason);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_RequestDeadline_SetOnContext()
    {
        var requestDeadlineSource = new TaskCompletionSource<DateTime?>();
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new AsyncFuncStringService(
                start: (ctx, input) =>
                {
                    requestDeadlineSource.SetResult(ctx.RequestDeadline);
                    return Task.FromResult(OperationStartResult.SyncResult($"Hello, {input}"));
                }));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        await RunInWorkflowAsync(workerOptions, async () =>
        {
            var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
            Assert.Equal("Hello, some-name", result);
        });
        // Server always sends a request timeout header, so the deadline should be set
        using CancellationTokenSource timeoutSource = new(TimeSpan.FromSeconds(5));
        Assert.NotNull(await requestDeadlineSource.Task.WaitAsync(timeoutSource.Token));
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_OperationSummary_FoundInHistory()
    {
        string expectedSummary = "custom operation summary";

        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (ctx, name) => name)));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        var handle = await RunInWorkflowAsync(workerOptions, async () =>
            {
                await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(
                        svc => svc.DoSomething("some-name"),
                        new() { Summary = expectedSummary });
            });

        HistoryEvent? nexusOperationScheduledEvent = null;
        await foreach (HistoryEvent historyEvent in handle.FetchHistoryEventsAsync())
        {
            if (historyEvent.NexusOperationScheduledEventAttributes is not null)
            {
                nexusOperationScheduledEvent = historyEvent;
            }
        }

        Assert.NotNull(nexusOperationScheduledEvent);
        string actualSummary = Client.Options.DataConverter.PayloadConverter.ToValue<string>(
            nexusOperationScheduledEvent.UserMetadata.Summary);
        Assert.Equal(expectedSummary, actualSummary);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_ScheduleToStartTimeout_FailsAsExpected()
    {
        var cancellationReasonSource = new TaskCompletionSource<string?>();
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (ctx, name) =>
                {
                    try
                    {
                        await Task.Delay(40000, ctx.CancellationToken);
                        cancellationReasonSource.SetResult("none");
                        return "done";
                    }
                    catch (TaskCanceledException)
                    {
                        cancellationReasonSource.SetResult(ctx.CancellationReason);
                        return "canceled";
                    }
                    catch (Exception)
                    {
                        cancellationReasonSource.SetResult("other exception");
                        throw;
                    }
                })));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // Confirm the workflow fails with the timeout
        var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, async () =>
            {
                await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(
                        svc => svc.DoSomething("some-name"),
                        new() { ScheduleToStartTimeout = TimeSpan.FromSeconds(5) });
            }));
        var timeoutExc = Assert.IsType<TimeoutFailureException>(
            Assert.IsType<NexusOperationFailureException>(exc.InnerException).InnerException);
        Assert.Equal(TimeoutType.ScheduleToStart, timeoutExc.TimeoutType);
        // Check that it is cancelled for the proper reason
        using CancellationTokenSource timeoutSource = new(TimeSpan.FromSeconds(5));
        var reason = await cancellationReasonSource.Task.WaitAsync(timeoutSource.Token);
        Assert.Equal("timed out", reason);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_StartToCloseTimeout_FailsAsExpected()
    {
        // Build a workflow-backed async operation that will never complete
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                WorkflowRunOperationHandler.FromHandleFactory(
                    (WorkflowRunOperationContext context, string input) =>
                        context.StartWorkflowAsync(
                            (WaitForeverWorkflow wf) => wf.RunAsync(input),
                            new() { Id = $"wf-{Guid.NewGuid()}" })))).
            AddWorkflow<WaitForeverWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // Confirm the workflow fails with the timeout
        var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, async () =>
            {
                await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(
                        svc => svc.DoSomething("some-name"),
                        new()
                        {
                            ScheduleToStartTimeout = TimeSpan.FromSeconds(30),
                            StartToCloseTimeout = TimeSpan.FromSeconds(2),
                        });
            }));
        var timeoutExc = Assert.IsType<TimeoutFailureException>(
            Assert.IsType<NexusOperationFailureException>(exc.InnerException).InnerException);
        Assert.Equal(TimeoutType.StartToClose, timeoutExc.TimeoutType);
    }

    [Workflow]
    public class WaitForSignalWorkflow
    {
        private bool signalReached;

        [WorkflowRun]
        public async Task<string> RunAsync(string name)
        {
            await Workflow.WaitConditionAsync(() => signalReached);
            return $"Hello, {name}!";
        }

        [WorkflowSignal]
        public async Task SignalAsync() => signalReached = true;
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_SimpleWorkflow_ConflictPolicy()
    {
        // Example of a Nexus service that creates a second operation with the same ID and
        // therefore will fail with conflict
        var workflowId = $"wf-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                WorkflowRunOperationHandler.FromHandleFactory(
                    (WorkflowRunOperationContext context, string input) =>
                        context.StartWorkflowAsync(
                            (WaitForSignalWorkflow wf) => wf.RunAsync(input),
                            new() { Id = workflowId })))).
            AddWorkflow<WaitForSignalWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // Run workflow, and check all the exception levels
        var exc = await Assert.ThrowsAnyAsync<Exception>(() => RunInWorkflowAsync(
            workerOptions,
            async () =>
            {
                // Start one Nexus operation which will succeed and another which will fail because
                // the second tries to start with the same ID
                var client = Workflow.CreateNexusWorkflowClient<IStringService>(endpoint);
                await client.StartNexusOperationAsync(svc => svc.DoSomething("some-name1"));
                await client.StartNexusOperationAsync(svc => svc.DoSomething("some-name2"));
            }));
        Assert.IsType<WorkflowFailedException>(exc);
        exc = exc.InnerException;
        Assert.IsType<NexusOperationFailureException>(exc);
        exc = exc.InnerException;
        Assert.IsType<HandlerException>(exc);
        exc = exc.InnerException;
        Assert.IsType<ApplicationFailureException>(exc);
        Assert.StartsWith("Workflow execution is already running", exc!.Message);

        // Example of a Nexus service that creates a second operation with the same ID, but this
        // time we set a conflict policy, so it will succeed
        workflowId = $"wf-{Guid.NewGuid()}";
        workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                WorkflowRunOperationHandler.FromHandleFactory(
                    (WorkflowRunOperationContext context, string input) =>
                        context.StartWorkflowAsync(
                            (WaitForSignalWorkflow wf) => wf.RunAsync(input),
                            new() { Id = workflowId, IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting })))).
            AddWorkflow<WaitForSignalWorkflow>();
        endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // Run workflow
        List<string> results = new();
        await RunInWorkflowAsync(
            workerOptions,
            async () =>
            {
                // Start both Nexus operations which will both succeed and be backed by the same
                // operation
                var client = Workflow.CreateNexusWorkflowClient<IStringService>(endpoint);
                var handle1 = await client.StartNexusOperationAsync(svc => svc.DoSomething("some-name1"));
                var handle2 = await client.StartNexusOperationAsync(svc => svc.DoSomething("some-name2"));

                // Signal the workflow to complete
                await Workflow.GetExternalWorkflowHandle<WaitForSignalWorkflow>(workflowId).
                    SignalAsync(wf => wf.SignalAsync());

                // Set both results
                results.Add(await handle1.GetResultAsync());
                results.Add(await handle2.GetResultAsync());
            });
        // Confirm results are for only the first param sets
        Assert.Equal(new List<string> { "Hello, some-name1!", "Hello, some-name1!" }, results);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_HandlerSignal_ForwardsInboundLinks()
    {
        // A target workflow that the handler will signal from inside the operation. The signal it
        // issues must carry the inbound Nexus task links so the WorkflowExecutionSignaled event on
        // the target links back to the caller workflow.
        var targetTaskQueue = $"tq-target-{Guid.NewGuid()}";
        var targetWorkflowId = $"wf-target-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (ctx, name) =>
                {
                    await NexusOperationExecutionContext.Current.TemporalClient.
                        GetWorkflowHandle<WaitForSignalWorkflow>(targetWorkflowId).
                        SignalAsync(wf => wf.SignalAsync());
                    return $"signaled {name}";
                })));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        // Run the target workflow on its own worker/task queue so the handler can signal it.
        using var targetWorker = new TemporalWorker(
            Client, new TemporalWorkerOptions(targetTaskQueue).
                AddWorkflow<WaitForSignalWorkflow>());
        await targetWorker.ExecuteAsync(async () =>
        {
            var targetHandle = await Client.StartWorkflowAsync(
                (WaitForSignalWorkflow wf) => wf.RunAsync("target"),
                new(targetWorkflowId, targetTaskQueue));

            var callerHandle = await RunInWorkflowAsync(workerOptions, async () =>
            {
                var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
                Assert.Equal("signaled some-name", result);
            });

            // The target workflow's signal event should carry a link pointing back at the caller.
            var signalEvent = Assert.Single(
                (await targetHandle.FetchHistoryAsync()).Events,
                evt => evt.WorkflowExecutionSignaledEventAttributes != null);
            var link = Assert.Single(signalEvent.Links);
            Assert.Equal(callerHandle.Id, link.WorkflowEvent.WorkflowId);
            Assert.Equal(
                Api.Enums.V1.EventType.NexusOperationScheduled,
                link.WorkflowEvent.EventRef.EventType);
        });
    }

    [SkippableFact]
    public async Task ExecuteNexusOperationAsync_HandlerSignal_PropagatesBacklink()
    {
        // The backward direction is gated by the server's history.enableCHASMSignalBacklinks dynamic
        // config (which requires history.enableChasm); older servers leave the response link unset.
        // Run against such a server with ENABLE_SIGNAL_BACKLINK_TESTS=1 to exercise this end to end.
        Skip.IfNot(
            Environment.GetEnvironmentVariable("ENABLE_SIGNAL_BACKLINK_TESTS") == "1",
            "Set ENABLE_SIGNAL_BACKLINK_TESTS=1 and run against a server with " +
                "history.enableCHASMSignalBacklinks=true");

        // A target workflow the handler signals from inside the operation. The server returns a
        // backlink on the signal response pointing at the target's WorkflowExecutionSignaled event;
        // the SDK must propagate it onto the caller's NexusOperationCompleted event.
        var targetTaskQueue = $"tq-target-{Guid.NewGuid()}";
        var targetWorkflowId = $"wf-target-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (ctx, name) =>
                {
                    await NexusOperationExecutionContext.Current.TemporalClient.
                        GetWorkflowHandle<WaitForSignalWorkflow>(targetWorkflowId).
                        SignalAsync(wf => wf.SignalAsync());
                    return $"signaled {name}";
                })));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        using var targetWorker = new TemporalWorker(
            Client, new TemporalWorkerOptions(targetTaskQueue).
                AddWorkflow<WaitForSignalWorkflow>());
        await targetWorker.ExecuteAsync(async () =>
        {
            await Client.StartWorkflowAsync(
                (WaitForSignalWorkflow wf) => wf.RunAsync("target"),
                new(targetWorkflowId, targetTaskQueue));

            var callerHandle = await RunInWorkflowAsync(workerOptions, async () =>
            {
                var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
                Assert.Equal("signaled some-name", result);
            });

            // The caller's NexusOperationCompleted event should carry a backlink pointing at the
            // target's WorkflowExecutionSignaled event.
            var completedEvent = Assert.Single(
                (await callerHandle.FetchHistoryAsync()).Events,
                evt => evt.NexusOperationCompletedEventAttributes != null);
            var backlink = Assert.Single(completedEvent.Links);
            Assert.Equal(targetWorkflowId, backlink.WorkflowEvent.WorkflowId);
            // Server PR temporalio/temporal#9897 keys these backlinks via RequestIdReference rather
            // than EventReference, so accept either oneof variant (matches the Java/Go/Python tests).
            var backlinkEventType = backlink.WorkflowEvent.RequestIdRef != null
                ? backlink.WorkflowEvent.RequestIdRef.EventType
                : backlink.WorkflowEvent.EventRef.EventType;
            Assert.Equal(Api.Enums.V1.EventType.WorkflowExecutionSignaled, backlinkEventType);
        });
    }

    [SkippableFact]
    public async Task ExecuteNexusOperationAsync_HandlerSignalsMultiple_PropagatesAllBacklinks()
    {
        // Integration-level counterpart to the unit-level accumulation tests: a single operation
        // handler signals several workflows and the server returns one backlink per signal, all of
        // which must land on the caller's single NexusOperationCompleted event (mirrors the
        // Java/Go/TypeScript multi-callee tests).
        Skip.IfNot(
            Environment.GetEnvironmentVariable("ENABLE_SIGNAL_BACKLINK_TESTS") == "1",
            "Set ENABLE_SIGNAL_BACKLINK_TESTS=1 and run against a server with " +
                "history.enableCHASMSignalBacklinks=true");

        var targetTaskQueue = $"tq-target-{Guid.NewGuid()}";
        var targetWorkflowIds = new List<string>
        {
            $"wf-target-a-{Guid.NewGuid()}",
            $"wf-target-b-{Guid.NewGuid()}",
            $"wf-target-c-{Guid.NewGuid()}",
        };
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (ctx, name) =>
                {
                    foreach (var targetWorkflowId in targetWorkflowIds)
                    {
                        await NexusOperationExecutionContext.Current.TemporalClient.
                            GetWorkflowHandle<WaitForSignalWorkflow>(targetWorkflowId).
                            SignalAsync(wf => wf.SignalAsync());
                    }
                    return $"signaled {name}";
                })));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        using var targetWorker = new TemporalWorker(
            Client, new TemporalWorkerOptions(targetTaskQueue).
                AddWorkflow<WaitForSignalWorkflow>());
        await targetWorker.ExecuteAsync(async () =>
        {
            foreach (var targetWorkflowId in targetWorkflowIds)
            {
                await Client.StartWorkflowAsync(
                    (WaitForSignalWorkflow wf) => wf.RunAsync("target"),
                    new(targetWorkflowId, targetTaskQueue));
            }

            var callerHandle = await RunInWorkflowAsync(workerOptions, async () =>
            {
                var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
                Assert.Equal("signaled some-name", result);
            });

            // The caller's NexusOperationCompleted event should carry one backlink per signalled
            // target, each pointing at that target's WorkflowExecutionSignaled event.
            var completedEvent = Assert.Single(
                (await callerHandle.FetchHistoryAsync()).Events,
                evt => evt.NexusOperationCompletedEventAttributes != null);
            Assert.Equal(targetWorkflowIds.Count, completedEvent.Links.Count);
            var linkedWorkflowIds = new HashSet<string>();
            foreach (var backlink in completedEvent.Links)
            {
                linkedWorkflowIds.Add(backlink.WorkflowEvent.WorkflowId);
                // Server PR temporalio/temporal#9897 keys these backlinks via RequestIdReference
                // rather than EventReference, so accept either oneof variant.
                var backlinkEventType = backlink.WorkflowEvent.RequestIdRef != null
                    ? backlink.WorkflowEvent.RequestIdRef.EventType
                    : backlink.WorkflowEvent.EventRef.EventType;
                Assert.Equal(Api.Enums.V1.EventType.WorkflowExecutionSignaled, backlinkEventType);
            }
            Assert.Equal(new HashSet<string>(targetWorkflowIds), linkedWorkflowIds);
        });
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_BadArgs_FailsOperation()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (ctx, name) => "never reached")));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // Converter failure is non-retryable BAD_REQUEST, so workflow should fail
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(
                workerOptions,
                // Use int as arg
                async () => await Workflow.CreateNexusWorkflowClient("StringService", endpoint).
                    ExecuteNexusOperationAsync<string>("DoSomething", 1234)));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<HandlerException>(exc2.InnerException);
        Assert.Equal(HandlerErrorType.BadRequest, exc3.ErrorType);
        Assert.Equal(
            HandlerErrorRetryBehavior.NonRetryable, exc3.ErrorRetryBehavior);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_Untyped_Succeeds()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (ctx, name) => $"Hello, {name}")));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        await RunInWorkflowAsync(
            workerOptions,
            async () => await Workflow.CreateNexusWorkflowClient("StringService", endpoint).
                ExecuteNexusOperationAsync<string>("DoSomething", "some-name"),
            checkResultFunc: async handle =>
                Assert.Equal("Hello, some-name", await handle.GetResultAsync()));
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_InputManip_Succeeds()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                WorkflowRunOperationHandler.FromHandleFactory(
                    async (WorkflowRunOperationContext context, string input) =>
                        await context.StartWorkflowAsync(
                            (SimpleWorkflow wf) => wf.RunAsync($"{input}-suffixed"),
                            new() { Id = $"wf-{Guid.NewGuid()}" })))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        await RunInWorkflowAsync(workerOptions, async () =>
        {
            var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
            Assert.Equal("Hello from workflow, some-name-suffixed", result);
        });
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_ApplicationFailure_NonRetryable()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (context, input) =>
                    throw new ApplicationFailureException("Intentional failure", nonRetryable: true)))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // How the exceptions come out with the Temporal Failure proto format:
        // Temporalio.Exceptions.WorkflowFailedException : Workflow failed
        // ---- Temporalio.Exceptions.NexusOperationFailureException : nexus operation completed unsuccessfully
        // -------- NexusRpc.Handlers.HandlerException : handler error (INTERNAL): Handler failed with non-retryable application error
        // ------------ Temporalio.Exceptions.ApplicationFailureException : Intentional failure
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"))));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<HandlerException>(exc2.InnerException);
        Assert.Equal(HandlerErrorType.Internal, exc3.ErrorType);
        var exc4 = Assert.IsType<ApplicationFailureException>(exc3.InnerException);
        Assert.Equal("Intentional failure", exc4.Message);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_OperationException_ProperlyFails()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (context, input) =>
                    throw OperationException.CreateFailed("Intentional failure")))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // How the exceptions come out:
        // Temporalio.Exceptions.WorkflowFailedException : Workflow failed
        // ---- Temporalio.Exceptions.NexusOperationFailureException : nexus operation completed unsuccessfully
        // -------- Temporalio.Exceptions.ApplicationFailureException : Intentional failure
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"))));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<ApplicationFailureException>(exc2.InnerException);
        Assert.Equal("Intentional failure", exc3.Message);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_OperationExceptionWithCause_PreservesCauseChain()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (context, input) =>
                    throw OperationException.CreateFailed(
                        "Operation failed",
                        new ApplicationFailureException("Root cause", errorType: "CustomError"))))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // How the exceptions come out:
        // Temporalio.Exceptions.WorkflowFailedException : Workflow failed
        // ---- Temporalio.Exceptions.NexusOperationFailureException : nexus operation completed unsuccessfully
        // -------- Temporalio.Exceptions.ApplicationFailureException : Operation failed
        // ------------ Temporalio.Exceptions.ApplicationFailureException : Root cause
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"))));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<ApplicationFailureException>(exc2.InnerException);
        Assert.Equal("Operation failed", exc3.Message);
        Assert.Equal("OperationError", exc3.ErrorType);
        Assert.True(exc3.NonRetryable);
        var exc4 = Assert.IsType<ApplicationFailureException>(exc3.InnerException);
        Assert.Equal("Root cause", exc4.Message);
        Assert.Equal("CustomError", exc4.ErrorType);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_OperationExceptionCanceled_ProperlyFails()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (context, input) =>
                    throw OperationException.CreateCanceled("Operation was canceled")))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // How the exceptions come out:
        // Temporalio.Exceptions.WorkflowFailedException : Workflow failed
        // ---- Temporalio.Exceptions.NexusOperationFailureException : nexus operation completed unsuccessfully
        // -------- Temporalio.Exceptions.CanceledFailureException : Operation was canceled
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"))));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<CanceledFailureException>(exc2.InnerException);
        Assert.Equal("Operation was canceled", exc3.Message);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_OperationExceptionCanceledWithCause_PreservesCauseChain()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (context, input) =>
                    throw OperationException.CreateCanceled(
                        "Operation was canceled",
                        new ApplicationFailureException("Root cause", errorType: "CustomError"))))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // How the exceptions come out:
        // Temporalio.Exceptions.WorkflowFailedException : Workflow failed
        // ---- Temporalio.Exceptions.NexusOperationFailureException : nexus operation completed unsuccessfully
        // -------- Temporalio.Exceptions.CanceledFailureException : Operation was canceled
        // ------------ Temporalio.Exceptions.ApplicationFailureException : Root cause
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"))));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<CanceledFailureException>(exc2.InnerException);
        Assert.Equal("Operation was canceled", exc3.Message);
        var exc4 = Assert.IsType<ApplicationFailureException>(exc3.InnerException);
        Assert.Equal("Root cause", exc4.Message);
        Assert.Equal("CustomError", exc4.ErrorType);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_HandlerException_ProperlyFails()
    {
        // Non-retryable
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (context, input) =>
                    throw new HandlerException(HandlerErrorType.BadRequest, "Intentional failure")))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // How the exceptions come out with Temporal Failure proto format:
        // Temporalio.Exceptions.WorkflowFailedException : Workflow failed
        // ---- Temporalio.Exceptions.NexusOperationFailureException : nexus operation completed unsuccessfully
        // -------- NexusRpc.Handlers.HandlerException : handler error (BAD_REQUEST): Intentional failure
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"))));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<HandlerException>(exc2.InnerException);
        Assert.Equal(HandlerErrorType.BadRequest, exc3.ErrorType);
        Assert.Equal("Intentional failure", exc3.Message);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_ManualDefinition_Succeeds()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddWorkflow<SimpleWorkflow>();
        var svcDefn = new ServiceDefinition(
            "my-service",
            new Dictionary<string, OperationDefinition>
            {
                ["my-operation"] = new OperationDefinition("my-operation", typeof(string), typeof(string)),
            });
        workerOptions.NexusServices.Add(new ServiceHandlerInstance(
            new ServiceDefinition(
                "my-service",
                new Dictionary<string, OperationDefinition>
                {
                    ["my-operation"] = new OperationDefinition("my-operation", typeof(string), typeof(string)),
                }),
            new Dictionary<string, IOperationHandler<object?, object?>>
            {
                ["my-operation"] = OperationHandler.Sync<object?, object?>(
                    (context, input) => $"manual-handler, param: {input}"),
            }));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        await RunInWorkflowAsync(workerOptions, async () =>
        {
            var result = await Workflow.CreateNexusWorkflowClient("my-service", endpoint).
                ExecuteNexusOperationAsync<string>("my-operation", "some-param");
            Assert.Equal("manual-handler, param: some-param", result);
        });
    }

    public class EventCaptureWorkerInterceptor : IWorkerInterceptor
    {
        public List<string> Events { get; } = new();

        public NexusOperationInboundInterceptor InterceptNexusOperation(
            NexusOperationInboundInterceptor nextInterceptor) =>
            new NexusInbound(Events, nextInterceptor);

        private class NexusInbound : NexusOperationInboundInterceptor
        {
            private readonly List<string> events;

            public NexusInbound(List<string> events, NexusOperationInboundInterceptor next)
                : base(next) => this.events = events;

            public override Task<OperationStartResult<object?>> ExecuteNexusOperationStartAsync(
                ExecuteNexusOperationStartInput input)
            {
                events.Add($"start-operation: {input.Context.Operation}");
                return base.ExecuteNexusOperationStartAsync(input);
            }

            public override Task ExecuteNexusOperationCancelAsync(ExecuteNexusOperationCancelInput input)
            {
                events.Add($"cancel-operation: {input.Context.Operation}");
                return base.ExecuteNexusOperationCancelAsync(input);
            }
        }
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_Interceptor_Reached()
    {
        var workflowId = $"wf-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                WorkflowRunOperationHandler.FromHandleFactory(
                    (WorkflowRunOperationContext context, string input) =>
                        context.StartWorkflowAsync(
                            (WaitForSignalWorkflow wf) => wf.RunAsync(input),
                            new() { Id = workflowId })))).
            AddWorkflow<WaitForSignalWorkflow>();
        var interceptor = new EventCaptureWorkerInterceptor();
        workerOptions.Interceptors = new IWorkerInterceptor[] { interceptor };
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(
                workerOptions,
                () => Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name")),
                beforeGetResultFunc: async handle =>
                {
                    // Wait for Nexus operation to get started
                    await AssertMore.HasEventEventuallyAsync(
                        handle, evt => evt.NexusOperationStartedEventAttributes != null);
                    // Now cancel entire workflow
                    await handle.CancelAsync();
                }));
        Assert.IsType<CanceledFailureException>(exc.InnerException);
        Assert.Equal(
            new List<string> { "start-operation: DoSomething", "cancel-operation: DoSomething" },
            interceptor.Events);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_ServiceNotFound_ProperlyFails()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (context, input) => "never reached"))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // How the exceptions come out:
        // Temporalio.Exceptions.WorkflowFailedException : Workflow failed
        // ---- Temporalio.Exceptions.NexusOperationFailureException : nexus operation completed unsuccessfully
        // -------- NexusRpc.Handlers.HandlerException : handler error (NOT_FOUND): Unrecognized service missing-service or operation unknown-operation
        // ------------ Temporalio.Exceptions.ApplicationFailureException : Unrecognized service missing-service or operation unknown-operation
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusWorkflowClient("missing-service", endpoint).
                    ExecuteNexusOperationAsync("unknown-operation", "some-param")));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<HandlerException>(exc2.InnerException);
        Assert.Equal(HandlerErrorType.NotFound, exc3.ErrorType);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_OperationNotFound_ProperlyFails()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (context, input) => "never reached"))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // How the exceptions come out:
        // Temporalio.Exceptions.WorkflowFailedException : Workflow failed
        // ---- Temporalio.Exceptions.NexusOperationFailureException : nexus operation completed unsuccessfully
        // -------- NexusRpc.Handlers.HandlerException : handler error (NOT_FOUND): Unrecognized service StringService or operation unknown-operation
        // ------------ Temporalio.Exceptions.ApplicationFailureException : Unrecognized service StringService or operation unknown-operation
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusWorkflowClient("StringService", endpoint).
                    ExecuteNexusOperationAsync("unknown-operation", "some-param")));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<HandlerException>(exc2.InnerException);
        Assert.Equal(HandlerErrorType.NotFound, exc3.ErrorType);
    }

    [Workflow]
    public class NoReturnWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync(string param)
        {
            // Do nothing
        }
    }

    [Workflow]
    public class NoParamWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync() => "done";
    }

    [Workflow]
    public class NoReturnOrParamWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            // Do nothing
        }
    }

    [NexusService]
    public interface IVoidService
    {
        [NexusOperation]
        void NoReturn(string param);

        [NexusOperation]
        string NoParam();

        [NexusOperation]
        void NoReturnOrParam();
    }

    [NexusServiceHandler(typeof(IVoidService))]
    public class VoidService
    {
        [NexusOperationHandler]
        public IOperationHandler<string, NoValue> NoReturn() =>
            WorkflowRunOperationHandler.FromHandleFactory<string>((context, param) =>
                context.StartWorkflowAsync(
                    (NoReturnWorkflow wf) => wf.RunAsync(param),
                    new() { Id = $"wf-{Guid.NewGuid()}" }));

        [NexusOperationHandler]
        public IOperationHandler<NoValue, string> NoParam() =>
            WorkflowRunOperationHandler.FromHandleFactory(context =>
                context.StartWorkflowAsync(
                    (NoParamWorkflow wf) => wf.RunAsync(),
                    new() { Id = $"wf-{Guid.NewGuid()}" }));

        [NexusOperationHandler]
        public IOperationHandler<NoValue, NoValue> NoReturnOrParam() =>
            WorkflowRunOperationHandler.FromHandleFactory(context =>
                context.StartWorkflowAsync(
                    (NoReturnOrParamWorkflow wf) => wf.RunAsync(),
                    new() { Id = $"wf-{Guid.NewGuid()}" }));
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_VoidTypes_Succeeds()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new VoidService()).
            AddWorkflow<NoReturnWorkflow>().
            AddWorkflow<NoParamWorkflow>().
            AddWorkflow<NoReturnOrParamWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        var handle = await RunInWorkflowAsync(workerOptions, async () =>
        {
            var client = Workflow.CreateNexusWorkflowClient<IVoidService>(endpoint);
            await client.ExecuteNexusOperationAsync(svc => svc.NoReturn("some-param"));
            var result = await client.ExecuteNexusOperationAsync(svc => svc.NoParam());
            Assert.Equal("done", result);
            await client.ExecuteNexusOperationAsync(svc => svc.NoReturnOrParam());
        });
        // Collect the linked Nexus workflows
        var workflowIds = (await handle.FetchHistoryAsync()).Events.SelectMany(evt =>
            evt.Links.Select(link => link.WorkflowEvent?.WorkflowId).OfType<string>()).ToList();
        Assert.Equal(3, workflowIds.Count);
        // Check each is the proper type
        Assert.Equal(
            "NoReturnWorkflow",
            (await Client.GetWorkflowHandle(workflowIds[0]).DescribeAsync()).WorkflowType);
        Assert.Equal(
            "NoParamWorkflow",
            (await Client.GetWorkflowHandle(workflowIds[1]).DescribeAsync()).WorkflowType);
        Assert.Equal(
            "NoReturnOrParamWorkflow",
            (await Client.GetWorkflowHandle(workflowIds[2]).DescribeAsync()).WorkflowType);
    }

    [NexusService]
    public interface ICancelTypeService
    {
        [NexusOperation]
        void SomeOperation();

        public enum Scenario
        {
            WaitRequestedTargetHang,
            WaitRequestedHandlerFail,
            WaitCompletedTargetTimerAndRethrow,
            Abandon,
            TryCancelHandlerFail,
        }
    }

    [NexusServiceHandler(typeof(ICancelTypeService))]
    public class CancelTypeService(ICancelTypeService.Scenario Scenario)
    {
        [NexusOperationHandler]
        public IOperationHandler<NoValue, NoValue> SomeOperation()
        {
            var handler = WorkflowRunOperationHandler.FromHandleFactory(context =>
                context.StartWorkflowAsync(
                    (CancelTypeWorkflow wf) => wf.RunAsync(Scenario),
                    new() { Id = $"wf-{Guid.NewGuid()}" }));
            if (Scenario == ICancelTypeService.Scenario.WaitRequestedHandlerFail ||
                Scenario == ICancelTypeService.Scenario.TryCancelHandlerFail)
            {
                handler = new FailCancelOperationHandler(handler);
            }
            return handler;
        }

        private class FailCancelOperationHandler(IOperationHandler<NoValue, NoValue> Underlying) :
            IOperationHandler<NoValue, NoValue>
        {
            public Task<OperationStartResult<NoValue>> StartAsync(
                OperationStartContext context, NoValue input) =>
                Underlying.StartAsync(context, input);

            public Task CancelAsync(OperationCancelContext context) =>
                throw new HandlerException(HandlerErrorType.NotImplemented, "Intentional failure");
        }
    }

    [Workflow]
    public class CancelTypeWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync(ICancelTypeService.Scenario scenario)
        {
            // Wait on cancellation then return success
            try
            {
                await Workflow.WaitConditionAsync(() => false);
                throw new InvalidOperationException("Unexpected success");
            }
            catch (OperationCanceledException)
            {
                switch (scenario)
                {
                    case ICancelTypeService.Scenario.WaitRequestedTargetHang:
                        await Workflow.WaitConditionAsync(() => false, cancellationToken: CancellationToken.None);
                        return;
                    case ICancelTypeService.Scenario.WaitCompletedTargetTimerAndRethrow:
                        await Workflow.DelayAsync(100, cancellationToken: CancellationToken.None);
                        throw;
                }
            }
        }
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_CancelWaitCompleted_ProperlyCancels()
    {
        var ret = await RunCancelOperationScenarioAsync(ICancelTypeService.Scenario.WaitCompletedTargetTimerAndRethrow);

        // Check caller workflow got Nexus cancel request and cancel completed
        Assert.Single(
            ret.CallerHistory.Events,
            e => e.NexusOperationCancelRequestedEventAttributes != null);
        Assert.Single(
            ret.CallerHistory.Events,
            e => e.NexusOperationCancelRequestCompletedEventAttributes != null);

        // Confirm target got cancel requested, completed a timer, and is canceled
        Assert.Equal(WorkflowExecutionStatus.Canceled, (await ret.TargetHandle.DescribeAsync()).Status);
        Assert.Single(
            (await ret.TargetHandle.FetchHistoryAsync()).Events,
            e => e.TimerFiredEventAttributes != null);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_CancelWaitRequested_ProperlyCancels()
    {
        var ret = await RunCancelOperationScenarioAsync(ICancelTypeService.Scenario.WaitRequestedTargetHang);

        // Check caller workflow got Nexus cancel request and cancel completed
        Assert.Single(
            ret.CallerHistory.Events,
            e => e.NexusOperationCancelRequestedEventAttributes != null);
        Assert.Single(
            ret.CallerHistory.Events,
            e => e.NexusOperationCancelRequestCompletedEventAttributes != null);

        // Confirm target got cancel requested but is still running
        var targetDesc = await ret.TargetHandle.DescribeAsync();
        Assert.Equal(WorkflowExecutionStatus.Running, targetDesc.Status);
        Assert.True(targetDesc.RawDescription.WorkflowExtendedInfo.CancelRequested);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_CancelWaitRequested_ProperlyFails()
    {
        // Confirm workflow fails trying to cancel
        var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunCancelOperationScenarioAsync(ICancelTypeService.Scenario.WaitRequestedHandlerFail));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc.InnerException);
        var exc3 = Assert.IsType<HandlerException>(exc2.InnerException);
        Assert.Equal(HandlerErrorType.NotImplemented, exc3.ErrorType);
        Assert.Equal("Intentional failure", exc3.Message);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_CancelAbandon_ProperlyCancels()
    {
        var ret = await RunCancelOperationScenarioAsync(ICancelTypeService.Scenario.Abandon);

        // Check caller workflow got cancel request but not a Nexus cancel request
        Assert.Single(
            ret.CallerHistory.Events,
            e => e.WorkflowExecutionCancelRequestedEventAttributes != null);
        Assert.DoesNotContain(
            ret.CallerHistory.Events,
            e => e.NexusOperationCancelRequestedEventAttributes != null);

        // Confirm target never got cancel requested and is still running
        var targetDesc = await ret.TargetHandle.DescribeAsync();
        Assert.Equal(WorkflowExecutionStatus.Running, targetDesc.Status);
        Assert.False(targetDesc.RawDescription.WorkflowExtendedInfo.CancelRequested);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_CancelTryCancel_ProperlyCancels()
    {
        var ret = await RunCancelOperationScenarioAsync(ICancelTypeService.Scenario.TryCancelHandlerFail);

        // Check caller workflow got Nexus cancel request but no cancel completed
        Assert.Single(
            ret.CallerHistory.Events,
            e => e.NexusOperationCancelRequestedEventAttributes != null);
        Assert.DoesNotContain(
            ret.CallerHistory.Events,
            e => e.NexusOperationCancelRequestCompletedEventAttributes != null);

        // Confirm target never got cancel requested and is still running
        var targetDesc = await ret.TargetHandle.DescribeAsync();
        Assert.Equal(WorkflowExecutionStatus.Running, targetDesc.Status);
        Assert.False(targetDesc.RawDescription.WorkflowExtendedInfo.CancelRequested);
    }

    private async Task<(WorkflowHistory CallerHistory, WorkflowHandle TargetHandle)> RunCancelOperationScenarioAsync(
        ICancelTypeService.Scenario scenario)
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new CancelTypeService(scenario)).
            AddWorkflow<CancelTypeWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        var handle = await RunInWorkflowAsync(
            workerOptions,
            inWorkflowFunc: async () =>
            {
                var client = Workflow.CreateNexusWorkflowClient<ICancelTypeService>(endpoint);
                try
                {
                    await client.ExecuteNexusOperationAsync(
                        svc => svc.SomeOperation(),
                        new()
                        {
                            CancellationType = scenario switch
                            {
                                ICancelTypeService.Scenario.WaitRequestedTargetHang or
                                ICancelTypeService.Scenario.WaitRequestedHandlerFail =>
                                    NexusOperationCancellationType.WaitCancellationRequested,
                                ICancelTypeService.Scenario.WaitCompletedTargetTimerAndRethrow =>
                                    NexusOperationCancellationType.WaitCancellationCompleted,
                                ICancelTypeService.Scenario.Abandon =>
                                    NexusOperationCancellationType.Abandon,
                                ICancelTypeService.Scenario.TryCancelHandlerFail =>
                                    NexusOperationCancellationType.TryCancel,
                                _ => throw new ArgumentException("Unknown scenario"),
                            },
                        });
                    throw new InvalidOperationException("Unexpected success");
                }
                catch (NexusOperationFailureException e) when (TemporalException.IsCanceledException(e))
                {
                    // Do nothing
                }
            },
            beforeGetResultFunc: async handle =>
            {
                // Wait for the Nexus operation to show as started
                await AssertMore.HasEventEventuallyAsync(
                    handle, evt => evt.NexusOperationStartedEventAttributes != null);
                // Now cancel the workflow
                await handle.CancelAsync();
            });

        // Check caller workflow got Nexus start
        var callerHistory = await handle.FetchHistoryAsync();
        var nexusStartEvent = Assert.Single(
            callerHistory.Events,
            e => e.NexusOperationStartedEventAttributes != null);
        return (
            CallerHistory: callerHistory,
            TargetHandle: Client.GetWorkflowHandle(nexusStartEvent.Links.Single().WorkflowEvent.WorkflowId));
    }

    private class FailOnceCodec : IPayloadCodec
    {
        private int decodeCallCount;

        public int DecodeCallCount => decodeCallCount;

        public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) =>
            Task.FromResult(payloads);

        public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads)
        {
            if (Interlocked.Increment(ref decodeCallCount) == 1)
            {
                throw new InvalidOperationException("Simulated codec failure");
            }
            return Task.FromResult(payloads);
        }
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_CodecFailure_IsRetried()
    {
        var codec = new FailOnceCodec();
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.DataConverter = DataConverter.Default with { PayloadCodec = codec };
        var codecClient = new TemporalClient(Client.Connection, newOptions);

        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>((ctx, name) => $"Hello, {name}")));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        workerOptions = (TemporalWorkerOptions)workerOptions.Clone();
        workerOptions.Interceptors = new IWorkerInterceptor[] { new XunitExceptionInterceptor() };
        workerOptions.AddWorkflow(WorkflowDefinition.Create(
            typeof(CustomFuncWorkflow),
            null,
            _args => new CustomFuncWorkflow(async () =>
            {
                var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
                Assert.Equal("Hello, some-name", result);
                return null;
            })));

        using var worker = new TemporalWorker(codecClient, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await codecClient.StartWorkflowAsync(
                (CustomFuncWorkflow wf) => wf.RunAsync(),
                new($"wf-{Guid.NewGuid()}", workerOptions.TaskQueue!));
            await handle.GetResultAsync();
        });

        // Decode is called once for the Nexus input (fails), once for the retry (succeeds),
        // and once for the Nexus result on the workflow side.
        Assert.True(
            codec.DecodeCallCount >= 2,
            $"Expected at least 2 decode calls (confirming retry), got {codec.DecodeCallCount}");
    }

    [NexusService]
    public interface INoArgService
    {
        [NexusOperation]
        string DoSomething();
    }

    [NexusServiceHandler(typeof(INoArgService))]
    public class NoArgService
    {
        [NexusOperationHandler]
        public IOperationHandler<NoValue, string> DoSomething() =>
            OperationHandler.Sync<NoValue, string>((ctx, _) => "hello");
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_NoValueInputWithCodec_Succeeds()
    {
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.DataConverter = DataConverter.Default with
        {
            PayloadCodec = new Converters.Base64PayloadCodec(),
        };
        var codecClient = new TemporalClient(Client.Connection, newOptions);

        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new NoArgService());
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        workerOptions = (TemporalWorkerOptions)workerOptions.Clone();
        workerOptions.Interceptors = new IWorkerInterceptor[] { new XunitExceptionInterceptor() };
        workerOptions.AddWorkflow(WorkflowDefinition.Create(
            typeof(CustomFuncWorkflow),
            null,
            _args => new CustomFuncWorkflow(async () =>
            {
                var result = await Workflow.CreateNexusWorkflowClient<INoArgService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething());
                Assert.Equal("hello", result);
                return null;
            })));

        using var worker = new TemporalWorker(codecClient, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await codecClient.StartWorkflowAsync(
                (CustomFuncWorkflow wf) => wf.RunAsync(),
                new($"wf-{Guid.NewGuid()}", workerOptions.TaskQueue!));
            await handle.GetResultAsync();
        });
    }

    private class AlwaysFailPayloadConverter : IPayloadConverter
    {
        private readonly IPayloadConverter inner = DataConverter.Default.PayloadConverter;

        public Payload ToPayload(object? value) => inner.ToPayload(value);

        public object? ToValue(Payload payload, Type type) =>
            throw new InvalidOperationException("Simulated converter failure");
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_ConverterFailure_IsNotRetried()
    {
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.DataConverter = DataConverter.Default with
        {
            PayloadConverter = new AlwaysFailPayloadConverter(),
        };
        var converterClient = new TemporalClient(Client.Connection, newOptions);

        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>((ctx, name) => $"Hello, {name}")));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        workerOptions = (TemporalWorkerOptions)workerOptions.Clone();
        workerOptions.Interceptors = new IWorkerInterceptor[] { new XunitExceptionInterceptor() };
        workerOptions.AddWorkflow(WorkflowDefinition.Create(
            typeof(CustomFuncWorkflow),
            null,
            _args => new CustomFuncWorkflow(async () =>
            {
                await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
                return null;
            })));

        // Use converterClient for the worker (Nexus handler uses failing converter)
        // but regular Client for starting the workflow (args serialize correctly)
        using var worker = new TemporalWorker(converterClient, workerOptions);
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(
            () => worker.ExecuteAsync(async () =>
            {
                var handle = await Client.StartWorkflowAsync(
                    (CustomFuncWorkflow wf) => wf.RunAsync(),
                    new($"wf-{Guid.NewGuid()}", workerOptions.TaskQueue!));
                await handle.GetResultAsync();
            }));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<HandlerException>(exc2.InnerException);
        Assert.Equal(HandlerErrorType.BadRequest, exc3.ErrorType);
        Assert.Equal(
            HandlerErrorRetryBehavior.NonRetryable, exc3.ErrorRetryBehavior);
        Assert.Contains(
            "Payload converter failed to decode Nexus operation input",
            exc3.Message);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_GenericHandler_StartWorkflow_Succeeds()
    {
        // Build the worker options w/ the nexus service using the new generic handler
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                TemporalOperationHandler.FromHandleFactory<string, string>(
                    async (context, client, input) =>
                        await client.StartWorkflowAsync(
                            (SimpleWorkflow wf) => wf.RunAsync(input),
                            new() { Id = $"wf-{Guid.NewGuid()}" })))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        // Run the Nexus client code in workflow
        await RunInWorkflowAsync(workerOptions, async () =>
        {
            var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
            Assert.Equal("Hello from workflow, some-name", result);
        });
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_GenericHandler_Cancel_Succeeds()
    {
        // Build the worker options w/ the nexus service using the new generic handler
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                TemporalOperationHandler.FromHandleFactory<string, string>(
                    async (context, client, input) =>
                        await client.StartWorkflowAsync(
                            (WaitForeverWorkflow wf) => wf.RunAsync(input),
                            new() { Id = $"wf-{Guid.NewGuid()}" })))).
            AddWorkflow<WaitForeverWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        // Cancel the whole workflow and confirm it has expected exceptions
        var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() => RunInWorkflowAsync(
            workerOptions,
            async () =>
            {
                var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
                Assert.Equal("Hello from workflow, some-name", result);
            },
            beforeGetResultFunc: async handle =>
            {
                // Wait for Nexus operation to get started
                await AssertMore.HasEventEventuallyAsync(
                    handle, evt => evt.NexusOperationStartedEventAttributes != null);
                // Now cancel entire workflow
                await handle.CancelAsync();
            }));
        Assert.IsType<CanceledFailureException>(wfExc.InnerException);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_GenericHandler_SyncResult_Succeeds()
    {
        // Build the worker options w/ a handler that returns a sync result
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                TemporalOperationHandler.FromHandleFactory<string, string>(
                    (context, client, input) =>
                        Task.FromResult(TemporalOperationResult<string>.SyncResult($"Hello, {input}")))));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        await RunInWorkflowAsync(workerOptions, async () =>
        {
            var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                ExecuteNexusOperationAsync(svc => svc.DoSomething("world"));
            Assert.Equal("Hello, world", result);
        });
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_GenericHandler_LinksAndContext_Populated()
    {
        // Capture the context and client passed to the generic handler so we can assert plumbing
        TemporalOperationStartContext? capturedContext = null;
        ITemporalNexusClient? capturedClient = null;
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                TemporalOperationHandler.FromHandleFactory<string, string>(
                    async (context, client, input) =>
                    {
                        capturedContext = context;
                        capturedClient = client;
                        return await client.StartWorkflowAsync(
                            (SimpleWorkflow wf) => wf.RunAsync(input),
                            new() { Id = $"wf-{Guid.NewGuid()}" });
                    }))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        var handle = await RunInWorkflowAsync(workerOptions, async () =>
        {
            var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"));
            Assert.Equal("Hello from workflow, some-name", result);
        });

        // Context fields populated
        Assert.NotNull(capturedContext);
        Assert.Equal("StringService", capturedContext.Service);
        Assert.Equal("DoSomething", capturedContext.Operation);
        Assert.True(Guid.TryParse(capturedContext.RequestId, out _));
        Assert.False(string.IsNullOrEmpty(capturedContext.Underlying.CallbackUrl));
        // Inbound link points to the caller workflow's scheduled event
        var wfEvent = Assert.Single(capturedContext.InboundLinks).ToWorkflowEvent();
        Assert.Equal(handle.Id, wfEvent.WorkflowId);
        Assert.Equal(Api.Enums.V1.EventType.NexusOperationScheduled, wfEvent.EventRef.EventType);

        // Nexus client exposes the temporal client
        Assert.NotNull(capturedClient);
        Assert.Equal(Env.Client.Options.Namespace, capturedClient.TemporalClient.Options.Namespace);

        // Outbound link on the start event points to the workflow that the handler started
        var startEvent = Assert.Single(
            (await handle.FetchHistoryAsync()).Events,
            evt => evt.NexusOperationStartedEventAttributes != null);
        var link = Assert.Single(startEvent.Links);
        Assert.Equal(1, link.WorkflowEvent.EventRef.EventId);
        Assert.Equal(
            Api.Enums.V1.EventType.WorkflowExecutionStarted,
            link.WorkflowEvent.EventRef.EventType);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_GenericHandler_CancelOperation_CancelsUnderlying()
    {
        // The default CancelWorkflowRunAsync should cancel the underlying workflow when the
        // operation is canceled (as opposed to canceling the caller workflow itself).
        var workflowId = $"wf-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                TemporalOperationHandler.FromHandleFactory<string, string>(
                    async (context, client, input) =>
                        await client.StartWorkflowAsync(
                            (WaitForeverWorkflow wf) => wf.RunAsync(input),
                            new() { Id = workflowId })))).
            AddWorkflow<WaitForeverWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, async () =>
            {
                using var cancelSource = new CancellationTokenSource();
                var handle = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    StartNexusOperationAsync(
                        svc => svc.DoSomething("some-name"),
                        new() { CancellationToken = cancelSource.Token });
#pragma warning disable CA1849, VSTHRD103 // https://github.com/temporalio/sdk-dotnet/issues/327
                cancelSource.Cancel();
#pragma warning restore CA1849, VSTHRD103
                await handle.GetResultAsync();
            }));
        var nexusExc = Assert.IsType<NexusOperationFailureException>(wfExc.InnerException);
        Assert.IsType<CanceledFailureException>(nexusExc.InnerException);

        // Underlying workflow was canceled too
        Assert.IsType<CanceledFailureException>(
            (await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                Client.GetWorkflowHandle(workflowId).GetResultAsync())).InnerException);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_GenericHandler_CancelOverride_Invoked()
    {
        // Subclass with a CancelWorkflowRunAsync override; verify the override is used and
        // receives the right workflow ID.
        var workflowId = $"wf-{Guid.NewGuid()}";
        CancelOverrideHandler? capturedHandler = null;
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
            {
                capturedHandler ??= new CancelOverrideHandler(
                    async (context, client, input) =>
                        await client.StartWorkflowAsync(
                            (WaitForeverWorkflow wf) => wf.RunAsync(input),
                            new() { Id = workflowId }));
                return capturedHandler;
            })).
            AddWorkflow<WaitForeverWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, async () =>
            {
                using var cancelSource = new CancellationTokenSource();
                var handle = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                    StartNexusOperationAsync(
                        svc => svc.DoSomething("some-name"),
                        new() { CancellationToken = cancelSource.Token });
#pragma warning disable CA1849, VSTHRD103
                cancelSource.Cancel();
#pragma warning restore CA1849, VSTHRD103
                await handle.GetResultAsync();
            }));

        Assert.NotNull(capturedHandler);
        Assert.True(capturedHandler.CancelCallCount > 0);
        Assert.Equal(workflowId, capturedHandler.CapturedWorkflowId);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_GenericHandler_StartWorkflowByName_Succeeds()
    {
        // Use the by-name overload of TemporalNexusClient.StartWorkflowAsync
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                TemporalOperationHandler.FromHandleFactory<string, string>(
                    async (context, client, input) =>
                        await client.StartWorkflowAsync<string>(
                            "SimpleWorkflow",
                            new object?[] { input },
                            new() { Id = $"wf-{Guid.NewGuid()}" })))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        await RunInWorkflowAsync(workerOptions, async () =>
        {
            var result = await Workflow.CreateNexusWorkflowClient<IStringService>(endpoint).
                ExecuteNexusOperationAsync(svc => svc.DoSomething("by-name"));
            Assert.Equal("Hello from workflow, by-name", result);
        });
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_GenericHandler_ConflictPolicy_UseExisting()
    {
        // Two operations with the same workflow ID + UseExisting policy attach to the same
        // workflow; both observe the first input. Exercises the OnConflictOptions plumbing in
        // NexusWorkflowStartHelper.
        var workflowId = $"wf-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                TemporalOperationHandler.FromHandleFactory<string, string>(
                    async (context, client, input) =>
                        await client.StartWorkflowAsync(
                            (WaitForSignalWorkflow wf) => wf.RunAsync(input),
                            new()
                            {
                                Id = workflowId,
                                IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
                            })))).
            AddWorkflow<WaitForSignalWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        var results = new List<string>();
        await RunInWorkflowAsync(workerOptions, async () =>
        {
            var client = Workflow.CreateNexusWorkflowClient<IStringService>(endpoint);
            var handle1 = await client.StartNexusOperationAsync(svc => svc.DoSomething("name1"));
            var handle2 = await client.StartNexusOperationAsync(svc => svc.DoSomething("name2"));
            await Workflow.GetExternalWorkflowHandle<WaitForSignalWorkflow>(workflowId).
                SignalAsync(wf => wf.SignalAsync());
            results.Add(await handle1.GetResultAsync());
            results.Add(await handle2.GetResultAsync());
        });
        Assert.Equal(new List<string> { "Hello, name1!", "Hello, name1!" }, results);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_GenericHandler_NoInputOverload_Succeeds()
    {
        // Exercise the no-input FromHandleFactory<TResult> overload
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryNoInputService(() =>
                TemporalOperationHandler.FromHandleFactory<string>(
                    (context, client) =>
                        Task.FromResult(TemporalOperationResult<string>.SyncResult("hello-no-input")))));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);

        await RunInWorkflowAsync(workerOptions, async () =>
        {
            var result = await Workflow.CreateNexusWorkflowClient<INoInputService>(endpoint).
                ExecuteNexusOperationAsync(svc => svc.DoIt());
            Assert.Equal("hello-no-input", result);
        });
    }

    [NexusService]
    public interface INoInputService
    {
        [NexusOperation]
        string DoIt();
    }

    [NexusServiceHandler(typeof(INoInputService))]
    public class HandlerFactoryNoInputService
    {
        private readonly Func<IOperationHandler<NoValue, string>> handlerFactory;

        public HandlerFactoryNoInputService(Func<IOperationHandler<NoValue, string>> handlerFactory) =>
            this.handlerFactory = handlerFactory;

        [NexusOperationHandler]
        public IOperationHandler<NoValue, string> DoIt() => handlerFactory();
    }

    private class CancelOverrideHandler : TemporalOperationHandler<string, string>
    {
        public CancelOverrideHandler(
            Func<TemporalOperationStartContext, ITemporalNexusClient, string,
                Task<TemporalOperationResult<string>>> startFunc)
            : base(startFunc)
        {
        }

        public int CancelCallCount { get; private set; }

        public string? CapturedWorkflowId { get; private set; }

        protected override Task CancelWorkflowRunAsync(
            TemporalOperationCancelContext context, CancelWorkflowRunInput input)
        {
            CancelCallCount++;
            CapturedWorkflowId = input.WorkflowId;
            return base.CancelWorkflowRunAsync(context, input);
        }
    }

    private async Task<string> CreateNexusEndpointAsync(string taskQueue)
    {
        var name = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(name, taskQueue);
        return name;
    }

    [Workflow]
    public class CustomFuncWorkflow
    {
        private readonly Func<Task<object?>> func;

        public CustomFuncWorkflow(Func<Task<object?>> func) => this.func = func;

        [WorkflowRun]
        public Task<object?> RunAsync() => func();
    }

    private async Task<WorkflowHandle> RunInWorkflowAsync(
        TemporalWorkerOptions workerOptions,
        Func<Task> inWorkflowFunc,
        Func<WorkflowHandle, Task>? beforeGetResultFunc = null,
        Func<WorkflowHandle, Task>? checkResultFunc = null) =>
        await RunInWorkflowAsync<object?>(
            workerOptions,
            async () =>
            {
                await inWorkflowFunc();
                return null;
            },
            beforeGetResultFunc,
            checkResultFunc);

    private async Task<WorkflowHandle<CustomFuncWorkflow, TResult>> RunInWorkflowAsync<TResult>(
        TemporalWorkerOptions workerOptions,
        Func<Task<TResult>> inWorkflowFunc,
        Func<WorkflowHandle<CustomFuncWorkflow, TResult>, Task>? beforeGetResultFunc = null,
        Func<WorkflowHandle<CustomFuncWorkflow, TResult>, Task>? checkResultFunc = null)
    {
        workerOptions = (TemporalWorkerOptions)workerOptions.Clone();
        // We want xUnit assertions to fail the workflow
        workerOptions.Interceptors = (workerOptions.Interceptors ?? Array.Empty<IWorkerInterceptor>()).
            Append(new XunitExceptionInterceptor()).ToList();
        workerOptions.AddWorkflow(WorkflowDefinition.Create(
            typeof(CustomFuncWorkflow),
            null,
            _args => new CustomFuncWorkflow(async () => await inWorkflowFunc())));
        using var worker = new TemporalWorker(Client, workerOptions);
        return await worker.ExecuteAsync(async () =>
        {
            var untypedHandle = await Client.StartWorkflowAsync(
                (CustomFuncWorkflow wf) => wf.RunAsync(),
                new($"wf-{Guid.NewGuid()}", workerOptions.TaskQueue!));
            var handle = new WorkflowHandle<CustomFuncWorkflow, TResult>(
                Client: Client,
                Id: untypedHandle.Id,
                RunId: untypedHandle.RunId,
                ResultRunId: untypedHandle.ResultRunId,
                FirstExecutionRunId: untypedHandle.FirstExecutionRunId);
            if (beforeGetResultFunc is { } beforeFunc)
            {
                await beforeFunc(handle);
            }
            if (checkResultFunc is { } checkFunc)
            {
                await checkFunc(handle);
            }
            else
            {
                await handle.GetResultAsync();
            }
            return handle;
        });
    }
}