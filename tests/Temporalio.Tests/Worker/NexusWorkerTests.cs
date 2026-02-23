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

            public Task<string> FetchResultAsync(OperationFetchResultContext context) =>
                throw new NotImplementedException();

            public Task<OperationInfo> FetchInfoAsync(OperationFetchInfoContext context) =>
                throw new NotImplementedException();
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
                var result = await Workflow.CreateNexusClient<IStringService>(endpointName).
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
            var result = await Workflow.CreateNexusClient<IStringService>(endpoint).
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
                var result = await Workflow.CreateNexusClient<IStringService>(endpoint).
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
                var handle = await Workflow.CreateNexusClient<IStringService>(endpoint).
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
        var contextSource = new TaskCompletionSource<OperationStartContext>();
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (ctx, name) =>
                {
                    contextSource.SetResult(ctx);
                    try
                    {
                        await Task.Delay(40000, ctx.CancellationToken);
                        return "done";
                    }
                    catch (TaskCanceledException)
                    {
                        return "canceled";
                    }
                })));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // Confirm the workflow fails with the timeout
        var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, async () =>
            {
                await Workflow.CreateNexusClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(
                        svc => svc.DoSomething("some-name"),
                        new() { ScheduleToCloseTimeout = TimeSpan.FromSeconds(2) });
            }));
        var timeoutExc = Assert.IsType<TimeoutFailureException>(
            Assert.IsType<NexusOperationFailureException>(exc.InnerException).InnerException);
        Assert.Equal(TimeoutType.ScheduleToClose, timeoutExc.TimeoutType);
        // Also check that our cancel token is canceled for the proper reason
        var ctx = await contextSource.Task;
        Assert.True(await Task.Run(() => ctx.CancellationToken.WaitHandle.WaitOne(2000)));
        Assert.Equal("timed out", ctx.CancellationReason);
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
                await Workflow.CreateNexusClient<IStringService>(endpoint).
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
        var contextSource = new TaskCompletionSource<OperationStartContext>();
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (ctx, name) =>
                {
                    contextSource.SetResult(ctx);
                    try
                    {
                        await Task.Delay(40000, ctx.CancellationToken);
                        return "done";
                    }
                    catch (TaskCanceledException)
                    {
                        return "canceled";
                    }
                })));
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // Confirm the workflow fails with the timeout
        var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, async () =>
            {
                await Workflow.CreateNexusClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(
                        svc => svc.DoSomething("some-name"),
                        new() { ScheduleToStartTimeout = TimeSpan.FromSeconds(2) });
            }));
        var timeoutExc = Assert.IsType<TimeoutFailureException>(
            Assert.IsType<NexusOperationFailureException>(exc.InnerException).InnerException);
        Assert.Equal(TimeoutType.ScheduleToStart, timeoutExc.TimeoutType);
        // Also check that our cancel token is canceled for the proper reason
        var ctx = await contextSource.Task;
        Assert.True(await Task.Run(() => ctx.CancellationToken.WaitHandle.WaitOne(2000)));
        Assert.Equal("timed out", ctx.CancellationReason);
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
                await Workflow.CreateNexusClient<IStringService>(endpoint).
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
                var client = Workflow.CreateNexusClient<IStringService>(endpoint);
                await client.StartNexusOperationAsync(svc => svc.DoSomething("some-name1"));
                await client.StartNexusOperationAsync(svc => svc.DoSomething("some-name2"));
            }));
        Assert.IsType<WorkflowFailedException>(exc);
        exc = exc.InnerException;
        Assert.IsType<NexusOperationFailureException>(exc);
        exc = exc.InnerException;
        Assert.IsType<NexusHandlerFailureException>(exc);
        exc = exc.InnerException;
        Assert.IsType<ApplicationFailureException>(exc);
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
                var client = Workflow.CreateNexusClient<IStringService>(endpoint);
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
                async () => await Workflow.CreateNexusClient("StringService", endpoint).
                    ExecuteNexusOperationAsync<string>("DoSomething", 1234)));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<NexusHandlerFailureException>(exc2.InnerException);
        Assert.Equal(HandlerErrorType.BadRequest, exc3.ErrorType);
        Assert.Equal(
            NexusHandlerErrorRetryBehavior.NonRetryable, exc3.ErrorRetryBehavior);
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
            async () => await Workflow.CreateNexusClient("StringService", endpoint).
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
            var result = await Workflow.CreateNexusClient<IStringService>(endpoint).
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
        // How the exceptions come out:
        // Temporalio.Exceptions.WorkflowFailedException : Workflow failed
        // ---- Temporalio.Exceptions.NexusOperationFailureException : nexus operation completed unsuccessfully
        // -------- Temporalio.Exceptions.NexusHandlerFailureException : handler error (INTERNAL): Handler failed with non-retryable application error
        // ------------ Temporalio.Exceptions.ApplicationFailureException : Handler failed with non-retryable application error
        // ---------------- Temporalio.Exceptions.ApplicationFailureException : Intentional failure
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"))));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<NexusHandlerFailureException>(exc2.InnerException);
        Assert.Equal(HandlerErrorType.Internal, exc3.ErrorType);
        var exc4 = Assert.IsType<ApplicationFailureException>(exc3.InnerException);
        var exc5 = Assert.IsType<ApplicationFailureException>(exc4.InnerException);
        Assert.Equal("Intentional failure", exc5.Message);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_OperationException_ProperlyFails()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new HandlerFactoryStringService(() =>
                OperationHandler.Sync<string, string>(async (context, input) =>
                    throw OperationException.CreateFailure("Intentional failure")))).
            AddWorkflow<SimpleWorkflow>();
        var endpoint = await CreateNexusEndpointAsync(workerOptions.TaskQueue!);
        // How the exceptions come out:
        // Temporalio.Exceptions.WorkflowFailedException : Workflow failed
        // ---- Temporalio.Exceptions.NexusOperationFailureException : nexus operation completed unsuccessfully
        // -------- Temporalio.Exceptions.ApplicationFailureException : Intentional failure
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"))));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<ApplicationFailureException>(exc2.InnerException);
        Assert.Equal("Intentional failure", exc3.Message);
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
        // How the exceptions come out:
        // Temporalio.Exceptions.WorkflowFailedException : Workflow failed
        // ---- Temporalio.Exceptions.NexusOperationFailureException : nexus operation completed unsuccessfully
        // -------- Temporalio.Exceptions.NexusHandlerFailureException : handler error (BAD_REQUEST): Intentional failure
        // ------------ Temporalio.Exceptions.ApplicationFailureException : Intentional failure
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusClient<IStringService>(endpoint).
                    ExecuteNexusOperationAsync(svc => svc.DoSomething("some-name"))));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<NexusHandlerFailureException>(exc2.InnerException);
        Assert.Equal(HandlerErrorType.BadRequest, exc3.ErrorType);
        var exc4 = Assert.IsType<ApplicationFailureException>(exc3.InnerException);
        Assert.Equal("Intentional failure", exc4.Message);
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
            var result = await Workflow.CreateNexusClient("my-service", endpoint).
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
                () => Workflow.CreateNexusClient<IStringService>(endpoint).
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
        // -------- Temporalio.Exceptions.NexusHandlerFailureException : handler error (NOT_FOUND): Unrecognized service missing-service or operation unknown-operation
        // ------------ Temporalio.Exceptions.ApplicationFailureException : Unrecognized service missing-service or operation unknown-operation
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusClient("missing-service", endpoint).
                    ExecuteNexusOperationAsync("unknown-operation", "some-param")));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<NexusHandlerFailureException>(exc2.InnerException);
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
        // -------- Temporalio.Exceptions.NexusHandlerFailureException : handler error (NOT_FOUND): Unrecognized service StringService or operation unknown-operation
        // ------------ Temporalio.Exceptions.ApplicationFailureException : Unrecognized service StringService or operation unknown-operation
        var exc1 = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
            RunInWorkflowAsync(workerOptions, () =>
                Workflow.CreateNexusClient("StringService", endpoint).
                    ExecuteNexusOperationAsync("unknown-operation", "some-param")));
        var exc2 = Assert.IsType<NexusOperationFailureException>(exc1.InnerException);
        var exc3 = Assert.IsType<NexusHandlerFailureException>(exc2.InnerException);
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
            var client = Workflow.CreateNexusClient<IVoidService>(endpoint);
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

            public Task<NoValue> FetchResultAsync(OperationFetchResultContext context) =>
                Underlying.FetchResultAsync(context);

            public Task<OperationInfo> FetchInfoAsync(OperationFetchInfoContext context) =>
                Underlying.FetchInfoAsync(context);

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
        var exc3 = Assert.IsType<NexusHandlerFailureException>(exc2.InnerException);
        Assert.Equal(HandlerErrorType.NotImplemented, exc3.ErrorType);
        var exc4 = Assert.IsType<ApplicationFailureException>(exc3.InnerException);
        Assert.Equal("Intentional failure", exc4.Message);
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
                var client = Workflow.CreateNexusClient<ICancelTypeService>(endpoint);
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
                var result = await Workflow.CreateNexusClient<IStringService>(endpoint).
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
                await Workflow.CreateNexusClient<IStringService>(endpoint).
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
        var exc3 = Assert.IsType<NexusHandlerFailureException>(exc2.InnerException);
        Assert.Equal(HandlerErrorType.BadRequest, exc3.ErrorType);
        Assert.Equal(
            NexusHandlerErrorRetryBehavior.NonRetryable, exc3.ErrorRetryBehavior);
        Assert.Contains(
            "Payload converter failed to decode Nexus operation input",
            exc3.Failure.Cause.Message);
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