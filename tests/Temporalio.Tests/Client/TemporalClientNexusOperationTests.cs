namespace Temporalio.Tests.Client;

using System.Collections.Concurrent;
using System.Threading;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Activities;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Exceptions;
using Temporalio.Nexus;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

[CloudTestExclusion(
    CloudTestExclusionReason.RequiresCloudProvisioning,
    "Requires a Cloud namespace with Standalone Nexus Operations enabled.")]
public class TemporalClientNexusOperationTests : WorkflowEnvironmentTestBase
{
    public TemporalClientNexusOperationTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [NexusService]
    public interface ITestService
    {
        [NexusOperation]
        string Echo(string input);

        [NexusOperation]
        void NoResult(string input);
    }

    [NexusServiceHandler(typeof(ITestService))]
    public class SyncServiceHandler
    {
        [NexusOperationHandler]
        public IOperationHandler<string, string> Echo() =>
            OperationHandler.Sync<string, string>((ctx, input) => $"echo:{input}");

        [NexusOperationHandler]
        public IOperationHandler<string, NoValue> NoResult() =>
            OperationHandler.Sync<string, NoValue>(
                (Func<OperationStartContext, string, NoValue>)((ctx, input) => default!));
    }

    [NexusServiceHandler(typeof(ITestService))]
    public class AsyncServiceHandler
    {
        [NexusOperationHandler]
        public IOperationHandler<string, string> Echo() => new AsyncEchoHandler();

        [NexusOperationHandler]
        public IOperationHandler<string, NoValue> NoResult() =>
            OperationHandler.Sync<string, NoValue>(
                (Func<OperationStartContext, string, NoValue>)((ctx, input) => default!));

        private class AsyncEchoHandler : IOperationHandler<string, string>
        {
            public Task<OperationStartResult<string>> StartAsync(
                OperationStartContext context, string input) =>
                Task.FromResult(OperationStartResult.AsyncResult<string>("test-token"));

            public Task CancelAsync(OperationCancelContext context) => Task.CompletedTask;
        }
    }

    [Workflow]
    public class EchoWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string input) => $"echo:{input}";
    }

    [Workflow]
    public class WaitForCancelWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string input)
        {
            await Workflow.WaitConditionAsync(() => false);
            return "never-reached";
        }
    }

    [NexusServiceHandler(typeof(ITestService))]
    public class WorkflowBackedEchoHandler
    {
        [NexusOperationHandler]
        public IOperationHandler<string, string> Echo() =>
            WorkflowRunOperationHandler.FromHandleFactory(
                (WorkflowRunOperationContext context, string input) =>
                    context.StartWorkflowAsync(
                        (EchoWorkflow wf) => wf.RunAsync(input),
                        new() { Id = $"wf-{Guid.NewGuid()}" }));

        [NexusOperationHandler]
        public IOperationHandler<string, NoValue> NoResult() =>
            OperationHandler.Sync<string, NoValue>(
                (Func<OperationStartContext, string, NoValue>)((ctx, input) => default!));
    }

    [NexusServiceHandler(typeof(ITestService))]
    public class WorkflowBackedWaitHandler
    {
        [NexusOperationHandler]
        public IOperationHandler<string, string> Echo() =>
            WorkflowRunOperationHandler.FromHandleFactory(
                (WorkflowRunOperationContext context, string input) =>
                    context.StartWorkflowAsync(
                        (WaitForCancelWorkflow wf) => wf.RunAsync(input),
                        new() { Id = $"wf-{Guid.NewGuid()}" }));

        [NexusOperationHandler]
        public IOperationHandler<string, NoValue> NoResult() =>
            OperationHandler.Sync<string, NoValue>(
                (Func<OperationStartContext, string, NoValue>)((ctx, input) => default!));
    }

    [NexusServiceHandler(typeof(ITestService))]
    public class CapturingWorkflowBackedEchoHandler
    {
        public NexusWorkflowRunHandle<string>? CapturedHandle { get; private set; }

        [NexusOperationHandler]
        public IOperationHandler<string, string> Echo() =>
            WorkflowRunOperationHandler.FromHandleFactory(
                async (WorkflowRunOperationContext context, string input) =>
                {
                    var handle = await context.StartWorkflowAsync(
                        (EchoWorkflow wf) => wf.RunAsync(input),
                        new() { Id = $"wf-{Guid.NewGuid()}" });
                    CapturedHandle = handle;
                    return handle;
                });

        [NexusOperationHandler]
        public IOperationHandler<string, NoValue> NoResult() =>
            OperationHandler.Sync<string, NoValue>(
                (Func<OperationStartContext, string, NoValue>)((ctx, input) => default!));
    }

    [Activity]
    public static Task<string> EchoActivityAsync(string input) => Task.FromResult($"echo:{input}");

    [NexusServiceHandler(typeof(ITestService))]
    public class RawActivityStartEchoHandler
    {
        [NexusOperationHandler]
        public IOperationHandler<string, string> Echo() =>
            OperationHandler.Sync<string, string>(async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var taskQueue = NexusOperationExecutionContext.Current.Info.TaskQueue;
                var first = await client.StartActivityAsync<string>(
                    () => EchoActivityAsync($"{input}-a"),
                    new()
                    {
                        Id = $"act-a-{Guid.NewGuid()}",
                        TaskQueue = taskQueue,
                        ScheduleToCloseTimeout = TimeSpan.FromMinutes(1),
                    });
                var second = await client.StartActivityAsync<string>(
                    () => EchoActivityAsync($"{input}-b"),
                    new()
                    {
                        Id = $"act-b-{Guid.NewGuid()}",
                        TaskQueue = taskQueue,
                        ScheduleToCloseTimeout = TimeSpan.FromMinutes(1),
                    });
                return $"{first.Id}|{second.Id}|{await first.GetResultAsync()}|" +
                    $"{await second.GetResultAsync()}";
            });

        [NexusOperationHandler]
        public IOperationHandler<string, NoValue> NoResult() =>
            OperationHandler.Sync<string, NoValue>(
                (Func<OperationStartContext, string, NoValue>)((ctx, input) => default!));
    }

    [Workflow]
    public class NexusCallerWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string endpoint, string input) =>
            await Workflow.CreateNexusWorkflowClient<ITestService>(endpoint).
                ExecuteNexusOperationAsync(svc => svc.Echo(input));
    }

    [NexusServiceHandler(typeof(ITestService))]
    public class RawActivityStartConflictPolicyEchoHandler
    {
        [NexusOperationHandler]
        public IOperationHandler<string, string> Echo() =>
            OperationHandler.Sync<string, string>(async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                await client.StartActivityAsync(
                    () => WaitForeverActivityAsync(),
                    new()
                    {
                        Id = input,
                        TaskQueue = NexusOperationExecutionContext.Current.Info.TaskQueue,
                        ScheduleToCloseTimeout = TimeSpan.FromMinutes(2),
                        IdConflictPolicy = ActivityIdConflictPolicy.UseExisting,
                    });
                return "attached";
            });

        [NexusOperationHandler]
        public IOperationHandler<string, NoValue> NoResult() =>
            OperationHandler.Sync<string, NoValue>(
                (Func<OperationStartContext, string, NoValue>)((ctx, input) => default!));
    }

    [NexusServiceHandler(typeof(ITestService))]
    public class RawActivityStartRedeliveryHandler
    {
        private int invocationCount;

        [NexusOperationHandler]
        public IOperationHandler<string, string> Echo() =>
            OperationHandler.Sync<string, string>(async (ctx, input) =>
            {
                var attempt = Interlocked.Increment(ref invocationCount);
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = await client.StartActivityAsync<string>(
                    () => CountingEchoActivityAsync(input),
                    new()
                    {
                        Id = $"act-{input}",
                        TaskQueue = NexusOperationExecutionContext.Current.Info.TaskQueue,
                        ScheduleToCloseTimeout = TimeSpan.FromMinutes(2),
                    });
                var result = await handle.GetResultAsync();
                if (attempt == 1)
                {
                    // Forces a real server-driven redelivery of this StartOperation task.
                    throw new InvalidOperationException("Simulated failure to force redelivery");
                }
                return result;
            });

        [NexusOperationHandler]
        public IOperationHandler<string, NoValue> NoResult() =>
            OperationHandler.Sync<string, NoValue>(
                (Func<OperationStartContext, string, NoValue>)((ctx, input) => default!));
    }

    [Fact]
    public async Task StartActivityAsync_RawActivityStartFromSyncHandler_TwoActivitiesBothSucceed()
    {
        await RunRawActivityStartTwoActivitiesAsync();
    }

    [Fact]
    public async Task StartActivityAsync_RawActivityStartFromSyncHandler_ActivitiesCarryInboundLink()
    {
        var (activityIds, callerWorkflowId) = await RunRawActivityStartTwoActivitiesAsync();
        foreach (var activityId in activityIds)
        {
            var desc = await Client.GetActivityHandle(activityId).DescribeAsync();
            var link = Assert.Single(desc.RawDescription.Info.Links);
            Assert.Equal(callerWorkflowId, link.WorkflowEvent.WorkflowId);
            Assert.Equal(
                Api.Enums.V1.EventType.NexusOperationScheduled,
                link.WorkflowEvent.EventRef.EventType);
        }
    }

    [Fact]
    public async Task StartActivityAsync_RawActivityStartUseExistingWithoutInboundLinks_Succeeds()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var activityId = $"act-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(new RawActivityStartConflictPolicyEchoHandler()).
            AddActivity(WaitForeverActivityAsync);
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(Client, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var existingHandle = await Client.StartActivityAsync(
                () => WaitForeverActivityAsync(),
                new()
                {
                    Id = activityId,
                    TaskQueue = taskQueue,
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(2),
                    HeartbeatTimeout = TimeSpan.FromSeconds(10),
                });

            var nexusClient = Client.CreateNexusClient<ITestService>(endpointName);
            var result = await nexusClient.ExecuteNexusOperationAsync<string>(
                svc => svc.Echo(activityId),
                new($"op-{Guid.NewGuid()}") { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
            Assert.Equal("attached", result);

            await existingHandle.CancelAsync();
        });
    }

    [Fact]
    public async Task StartActivityAsync_RawActivityStartFromSyncHandler_DedupsAcrossRedelivery()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var activityKey = $"redelivery-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(new RawActivityStartRedeliveryHandler()).
            AddActivity(CountingEchoActivityAsync);
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(Client, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var nexusClient = Client.CreateNexusClient<ITestService>(endpointName);
            var result = await nexusClient.ExecuteNexusOperationAsync<string>(
                svc => svc.Echo(activityKey),
                new($"op-{Guid.NewGuid()}")
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(3),
                    Rpc = new() { Timeout = TimeSpan.FromMinutes(3) },
                });
            Assert.Equal($"echo:{activityKey}", result);
        });

        Assert.Equal(1, ActivityRunCounts.GetValueOrDefault(activityKey));
    }

    [Fact]
    public async Task StartActivityAsync_RawActivityStartFromSyncHandler_UserInterceptorStillRuns()
    {
        var interceptor = new ActivityStartCapturingInterceptor();
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Interceptors = new IClientInterceptor[] { interceptor };
        var interceptedClient = new TemporalClient(Client.Connection, newOptions);

        var taskQueue = $"tq-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(new RawActivityStartEchoHandler()).
            AddActivity(EchoActivityAsync).
            AddWorkflow<NexusCallerWorkflow>();
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(interceptedClient, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await interceptedClient.StartWorkflowAsync(
                (NexusCallerWorkflow wf) => wf.RunAsync(endpointName, "hello"),
                new($"caller-{Guid.NewGuid()}", taskQueue));
            var parts = (await handle.GetResultAsync()).Split('|');
            Assert.Equal("echo:hello-a|echo:hello-b", $"{parts[2]}|{parts[3]}");
        });

        Assert.Equal(2, interceptor.ActivityIds.Count);
    }

    [Fact]
    public async Task StartActivityAsync_RawActivityStartFromSyncHandler_DoesNotReplayClientPlugins()
    {
        var plugin = new CountingClientPlugin();
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Plugins = new ITemporalClientPlugin[] { plugin };
        var pluginClient = new TemporalClient(Client.Connection, newOptions);
        Assert.Equal(1, plugin.ConfigureClientCallCount);

        var taskQueue = $"tq-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(new RawActivityStartEchoHandler()).
            AddActivity(EchoActivityAsync).
            AddWorkflow<NexusCallerWorkflow>();
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(pluginClient, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await pluginClient.StartWorkflowAsync(
                (NexusCallerWorkflow wf) => wf.RunAsync(endpointName, "hello"),
                new($"caller-{Guid.NewGuid()}", taskQueue));
            var parts = (await handle.GetResultAsync()).Split('|');
            Assert.Equal("echo:hello-a|echo:hello-b", $"{parts[2]}|{parts[3]}");
        });

        Assert.Equal(1, plugin.ConfigureClientCallCount);
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_SimpleWithResult_Succeeds()
    {
        await ExecuteNexusWorkerAsync<SyncServiceHandler>(async (nexusClient) =>
        {
            var result = await nexusClient.ExecuteNexusOperationAsync<string>(
                svc => svc.Echo("hello"),
                new($"op-{Guid.NewGuid()}")
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });
            Assert.Equal("echo:hello", result);
        });
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_VoidResult_Succeeds()
    {
        await ExecuteNexusWorkerAsync<SyncServiceHandler>(async (nexusClient) =>
        {
            await nexusClient.ExecuteNexusOperationAsync(
                svc => svc.NoResult("hello"),
                new($"op-{Guid.NewGuid()}")
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });
        });
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_ByName_Succeeds()
    {
        await ExecuteNexusWorkerAsync<SyncServiceHandler>(async (_, untypedClient) =>
        {
            var result = await untypedClient.ExecuteNexusOperationAsync<string>(
                "Echo",
                "world",
                new($"op-{Guid.NewGuid()}")
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });
            Assert.Equal("echo:world", result);
        });
    }

    [Fact]
    public async Task StartNexusOperationAsync_AlreadyStarted_Throws()
    {
        await ExecuteNexusWorkerAsync<AsyncServiceHandler>(async (nexusClient) =>
        {
            var operationId = $"op-{Guid.NewGuid()}";
            var handle = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo("hello"),
                new(operationId)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                    IdConflictPolicy = NexusOperationIdConflictPolicy.Fail,
                });

            // Try to start again with same ID
            var err = await Assert.ThrowsAsync<NexusOperationAlreadyStartedException>(() =>
                nexusClient.StartNexusOperationAsync<string>(
                    svc => svc.Echo("hello"),
                    new(operationId)
                    {
                        ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                        IdConflictPolicy = NexusOperationIdConflictPolicy.Fail,
                    }));
            Assert.Equal(operationId, err.OperationId);
            Assert.NotNull(err.RunId);

            // Cleanup
            await handle.TerminateAsync();
        });
    }

    [Fact]
    public async Task StartNexusOperationAsync_IdReusePolicyRejectDuplicate_Throws()
    {
        await ExecuteNexusWorkerAsync<SyncServiceHandler>(async (nexusClient) =>
        {
            var operationId = $"op-{Guid.NewGuid()}";
            var opts = new NexusOperationOptions(operationId)
            {
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                IdReusePolicy = NexusOperationIdReusePolicy.RejectDuplicate,
            };

            // Start and complete first operation
            var handle = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo("first"), opts);
            await handle.GetResultAsync();

            // Try to start again with same ID - should fail
            var err = await Assert.ThrowsAsync<NexusOperationAlreadyStartedException>(() =>
                nexusClient.StartNexusOperationAsync<string>(
                    svc => svc.Echo("second"), opts));
            Assert.Equal(operationId, err.OperationId);
        });
    }

    [Fact]
    public async Task GetNexusOperationHandle_ExistingOperation_Succeeds()
    {
        await ExecuteNexusWorkerAsync<SyncServiceHandler>(async (nexusClient) =>
        {
            var operationId = $"op-{Guid.NewGuid()}";
            var handle = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo("test"),
                new(operationId)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });
            await handle.GetResultAsync();

            // Get handle by ID and RunId with known result type
            var handle2 = Client.GetNexusOperationHandle<string>(operationId, handle.RunId);
            Assert.Equal(operationId, handle2.Id);
            Assert.Equal(handle.RunId, handle2.RunId);
            Assert.Equal("echo:test", await handle2.GetResultAsync());
        });
    }

    [Fact]
    public async Task DescribeAsync_RunningAndTerminated_IsAccurate()
    {
        await ExecuteNexusWorkerAsync<AsyncServiceHandler>(async (nexusClient) =>
        {
            var operationId = $"op-{Guid.NewGuid()}";
            var handle = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo("hello"),
                new(operationId)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });

            // Describe while running
            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(NexusOperationExecutionStatus.Running, desc.Status);
                Assert.Equal(operationId, desc.OperationId);
                Assert.True(desc.ScheduledTime > DateTime.MinValue);
                Assert.NotNull(desc.ScheduleToCloseTimeout);
                Assert.Null(desc.CloseTime);
            });

            // Terminate and describe again
            await handle.TerminateAsync();
            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(NexusOperationExecutionStatus.Terminated, desc.Status);
                Assert.NotNull(desc.CloseTime);
            });
        });
    }

    [Fact]
    public async Task DescribeAsync_UserMetadata_IsAccurate()
    {
        await ExecuteNexusWorkerAsync<SyncServiceHandler>(async (nexusClient) =>
        {
            var handle = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo("meta"),
                new($"op-{Guid.NewGuid()}")
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                    Summary = "Test summary",
                });
            await handle.GetResultAsync();

            var desc = await handle.DescribeAsync();
            Assert.Equal("Test summary", await desc.GetStaticSummaryAsync());
        });
    }

    [Fact]
    public async Task CancelAsync_RunningOperation_Succeeds()
    {
        await ExecuteNexusWorkerAsync<AsyncServiceHandler>(async (nexusClient) =>
        {
            var handle = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo("hello"),
                new($"op-{Guid.NewGuid()}")
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });

            // Cancel should not throw
            await handle.CancelAsync(new() { Reason = "test cancel reason" });

            // Cleanup
            await handle.TerminateAsync();
            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(NexusOperationExecutionStatus.Terminated, desc.Status);
            });
        });
    }

    [Fact]
    public async Task ListNexusOperationsAsync_SimpleList_IsAccurate()
    {
        await ExecuteNexusWorkerAsync<SyncServiceHandler>(
            async (nexusClient, _, endpointName) =>
            {
                // Start and complete 5 operations
                for (var i = 0; i < 5; i++)
                {
                    await nexusClient.ExecuteNexusOperationAsync<string>(
                        svc => svc.Echo($"item-{i}"),
                        new($"op-list-{Guid.NewGuid()}")
                        {
                            ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                        });
                }

                // List and verify
                await AssertMore.EventuallyAsync(async () =>
                {
                    var operations = new List<NexusOperationExecution>();
                    await foreach (var op in Client.ListNexusOperationsAsync(
                        $"Endpoint = '{endpointName}'"))
                    {
                        operations.Add(op);
                    }
                    Assert.Equal(5, operations.Count);
                    foreach (var op in operations)
                    {
                        Assert.Equal(NexusOperationExecutionStatus.Completed, op.Status);
                    }
                });

                // Verify count
                await AssertMore.EventuallyAsync(async () =>
                {
                    var resp = await Client.CountNexusOperationsAsync(
                        $"Endpoint = '{endpointName}'");
                    Assert.Equal(5, resp.Count);
                });

                // Verify manual paging
                await AssertMore.EventuallyAsync(async () =>
                {
                    var options = new NexusOperationListPaginatedOptions { PageSize = 2 };
                    var firstPage = await Client.ListNexusOperationsPaginatedAsync(
                        $"Endpoint = '{endpointName}'", null, options);
                    Assert.Equal(2, firstPage.Operations.Count);
                    Assert.NotNull(firstPage.NextPageToken);

                    var secondPage = await Client.ListNexusOperationsPaginatedAsync(
                        $"Endpoint = '{endpointName}'", firstPage.NextPageToken, options);
                    Assert.Equal(2, secondPage.Operations.Count);
                    Assert.NotNull(secondPage.NextPageToken);

                    var thirdPage = await Client.ListNexusOperationsPaginatedAsync(
                        $"Endpoint = '{endpointName}'", secondPage.NextPageToken, options);
                    Assert.Single(thirdPage.Operations);
                    Assert.Null(thirdPage.NextPageToken);
                });
            });
    }

    [Fact]
    public async Task StartNexusOperationAsync_Interceptors_AreCalledProperly()
    {
        var interceptor = new NexusTracingInterceptor();
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Interceptors = new IClientInterceptor[] { interceptor };
        var client = new TemporalClient(Client.Connection, newOptions);

        var taskQueue = $"tq-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(new AsyncServiceHandler());
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(client, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var nexusClient = client.CreateNexusClient<ITestService>(endpointName);
            var operationId = $"op-{Guid.NewGuid()}";
            var handle = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo("hello"),
                new(operationId)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });

            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(NexusOperationExecutionStatus.Running, desc.Status);
            });

            await handle.CancelAsync();
            await handle.TerminateAsync();

            Assert.Equal("StartNexusOperation", interceptor.Events[0].Name);
            Assert.Equal(
                operationId,
                ((StartNexusOperationInput)interceptor.Events[0].Input).Options.Id);

            Assert.Equal("DescribeNexusOperation", interceptor.Events[1].Name);
            Assert.Equal(
                operationId,
                ((DescribeNexusOperationInput)interceptor.Events[1].Input).Id);

            Assert.Equal("CancelNexusOperation", interceptor.Events[^2].Name);
            Assert.Equal(
                operationId,
                ((CancelNexusOperationInput)interceptor.Events[^2].Input).Id);

            Assert.Equal("TerminateNexusOperation", interceptor.Events[^1].Name);
            Assert.Equal(
                operationId,
                ((TerminateNexusOperationInput)interceptor.Events[^1].Input).Id);
        });
    }

    [Fact]
    public async Task StartNexusOperationAsync_WorkflowBacked_OutboundLinkPointsToStartedWorkflow()
    {
        var handler = new CapturingWorkflowBackedEchoHandler();
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(handler).
            AddWorkflow<EchoWorkflow>();
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(Client, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var nexusClient = Client.CreateNexusClient<ITestService>(endpointName);
            var operationId = $"op-{Guid.NewGuid()}";
            var handle = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo("hello"),
                new(operationId) { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
            Assert.Equal("echo:hello", await handle.GetResultAsync());

            Assert.NotNull(handler.CapturedHandle);

            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                var link = Assert.Single(desc.RawDescription.Info.Links);
                Assert.Equal(Client.Options.Namespace, link.WorkflowEvent.Namespace);
                Assert.Equal(handler.CapturedHandle.WorkflowId, link.WorkflowEvent.WorkflowId);
                Assert.Equal(1, link.WorkflowEvent.EventRef.EventId);
                Assert.Equal(
                    Api.Enums.V1.EventType.WorkflowExecutionStarted,
                    link.WorkflowEvent.EventRef.EventType);
            });

            // The started workflow's WorkflowExecutionStarted event should carry the nexus
            // operation link on its completion callback so the workflow's completion can be
            // tied back to the nexus operation.
            var wfHandle = Client.GetWorkflowHandle(handler.CapturedHandle.WorkflowId);
            var startedEvent = Assert.Single(
                (await wfHandle.FetchHistoryAsync()).Events,
                evt => evt.WorkflowExecutionStartedEventAttributes != null);
            var callback = Assert.Single(
                startedEvent.WorkflowExecutionStartedEventAttributes.CompletionCallbacks);
            var backlink = Assert.Single(callback.Links);
            Assert.Equal(Client.Options.Namespace, backlink.NexusOperation.Namespace);
            Assert.Equal(operationId, backlink.NexusOperation.OperationId);
            Assert.Equal(handle.RunId, backlink.NexusOperation.RunId);
        });
    }

    [Fact]
    public async Task ExecuteNexusOperationAsync_WorkflowBacked_Succeeds()
    {
        await ExecuteWorkflowBackedAsync<WorkflowBackedEchoHandler>(async (nexusClient) =>
        {
            var result = await nexusClient.ExecuteNexusOperationAsync<string>(
                svc => svc.Echo("hello"),
                new($"op-{Guid.NewGuid()}")
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });
            Assert.Equal("echo:hello", result);
        });
    }

    [Fact]
    public async Task CancelAsync_WorkflowBacked_CancelsOperation()
    {
        await ExecuteWorkflowBackedAsync<WorkflowBackedWaitHandler>(async (nexusClient) =>
        {
            var handle = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo("hello"),
                new($"op-{Guid.NewGuid()}")
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });

            // Wait for running
            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(NexusOperationExecutionStatus.Running, desc.Status);
            });

            // Cancel
            await handle.CancelAsync(new() { Reason = "test cancel" });

            // Should eventually be canceled
            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(NexusOperationExecutionStatus.Canceled, desc.Status);
            });
        });
    }

    internal record TracingEvent(string Name, object Input);

    internal class NexusTracingInterceptor : IClientInterceptor
    {
        public List<TracingEvent> Events { get; } = new();

        public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor next) =>
            new NexusTracingOutboundInterceptor(next, Events);
    }

    internal class NexusTracingOutboundInterceptor : ClientOutboundInterceptor
    {
        public NexusTracingOutboundInterceptor(
            ClientOutboundInterceptor next, List<TracingEvent> events)
            : base(next)
        {
            Events = events;
        }

        public List<TracingEvent> Events { get; private init; }

        public override Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
            StartNexusOperationInput input)
        {
            Events.Add(new("StartNexusOperation", input));
            return base.StartNexusOperationAsync<TResult>(input);
        }

        public override Task<NexusOperationExecutionDescription> DescribeNexusOperationAsync(
            DescribeNexusOperationInput input)
        {
            Events.Add(new("DescribeNexusOperation", input));
            return base.DescribeNexusOperationAsync(input);
        }

        public override Task CancelNexusOperationAsync(CancelNexusOperationInput input)
        {
            Events.Add(new("CancelNexusOperation", input));
            return base.CancelNexusOperationAsync(input);
        }

        public override Task TerminateNexusOperationAsync(TerminateNexusOperationInput input)
        {
            Events.Add(new("TerminateNexusOperation", input));
            return base.TerminateNexusOperationAsync(input);
        }
    }

    internal class ActivityStartCapturingInterceptor : IClientInterceptor
    {
        public List<string> ActivityIds { get; } = new();

        public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor next) =>
            new ActivityStartCapturingOutboundInterceptor(next, ActivityIds);
    }

    internal class ActivityStartCapturingOutboundInterceptor : ClientOutboundInterceptor
    {
        public ActivityStartCapturingOutboundInterceptor(
            ClientOutboundInterceptor next, List<string> activityIds)
            : base(next)
        {
            ActivityIds = activityIds;
        }

        public List<string> ActivityIds { get; private init; }

        public override Task<ActivityHandle<TResult>> StartActivityAsync<TResult>(
            StartActivityInput input)
        {
            ActivityIds.Add(input.Options.Id!);
            return base.StartActivityAsync<TResult>(input);
        }
    }

    internal class CountingClientPlugin : ITemporalClientPlugin
    {
        public int ConfigureClientCallCount { get; private set; }

        public string Name => "CountingClientPlugin";

        public void ConfigureClient(TemporalClientOptions options) => ConfigureClientCallCount++;

        public Task<TemporalConnection> ConnectAsync(
            TemporalClientConnectOptions options,
            Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation) =>
            continuation(options);
    }

    [Activity]
    private static async Task WaitForeverActivityAsync()
    {
        var ctx = ActivityExecutionContext.Current;
        while (!ctx.CancellationToken.IsCancellationRequested)
        {
            ctx.Heartbeat();
            await Task.Delay(TimeSpan.FromMilliseconds(200), ctx.CancellationToken);
        }
    }

    private static readonly ConcurrentDictionary<string, int> ActivityRunCounts = new();

    [Activity]
    private static Task<string> CountingEchoActivityAsync(string input)
    {
        ActivityRunCounts.AddOrUpdate(input, 1, (_, count) => count + 1);
        return Task.FromResult($"echo:{input}");
    }

    private async Task<(string[] ActivityIds, string CallerWorkflowId)>
        RunRawActivityStartTwoActivitiesAsync()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var callerWorkflowId = $"caller-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(new RawActivityStartEchoHandler()).
            AddActivity(EchoActivityAsync).
            AddWorkflow<NexusCallerWorkflow>();
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(Client, workerOptions);
        return await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (NexusCallerWorkflow wf) => wf.RunAsync(endpointName, "hello"),
                new(callerWorkflowId, taskQueue));
            var result = await handle.GetResultAsync();
            var parts = result.Split('|');
            Assert.Equal("echo:hello-a|echo:hello-b", $"{parts[2]}|{parts[3]}");
            return (new[] { parts[0], parts[1] }, callerWorkflowId);
        });
    }

    private async Task ExecuteWorkflowBackedAsync<THandler>(
        Func<NexusClient<ITestService>, Task> testFunc)
        where THandler : new()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(new THandler()).
            AddWorkflow<EchoWorkflow>().
            AddWorkflow<WaitForCancelWorkflow>();
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(Client, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var typedClient = Client.CreateNexusClient<ITestService>(endpointName);
            await testFunc(typedClient);
        });
    }

    private async Task ExecuteNexusWorkerAsync<THandler>(
        Func<NexusClient<ITestService>, Task> testFunc)
        where THandler : new()
    {
        await ExecuteNexusWorkerAsync<THandler>(
            (typed, _, _) => testFunc(typed));
    }

    private async Task ExecuteNexusWorkerAsync<THandler>(
        Func<NexusClient<ITestService>, NexusClient, Task> testFunc)
        where THandler : new()
    {
        await ExecuteNexusWorkerAsync<THandler>(
            (typed, untyped, _) => testFunc(typed, untyped));
    }

    private async Task ExecuteNexusWorkerAsync<THandler>(
        Func<NexusClient<ITestService>, NexusClient, string, Task> testFunc)
        where THandler : new()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var workerOptions = new TemporalWorkerOptions(taskQueue).
            AddNexusService(new THandler());
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(Client, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var typedClient = Client.CreateNexusClient<ITestService>(endpointName);
            var untypedClient = Client.CreateNexusClient(endpointName, "TestService");
            await testFunc(typedClient, untypedClient, endpointName);
        });
    }
}
