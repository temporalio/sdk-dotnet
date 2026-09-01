namespace Temporalio.Tests.Worker;

using System.Collections.Concurrent;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Nexus;
using Temporalio.Worker;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class SystemNexusTests : WorkflowEnvironmentTestBase
{
    public SystemNexusTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    [CloudTestExclusion(
        CloudTestExclusionReason.RequiresLocalServer,
        "Requires local dynamic configuration to enable signal with start from a workflow.")]
    public async Task ExecuteWorkflowAsync_SignalWithStartFromWorkflow_SucceedsAndReplays()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddWorkflow<SystemNexusSignalWithStartTargetWorkflow>();
        await ExecuteWorkerAsync<SystemNexusSignalWithStartCallerWorkflow>(
            async worker =>
            {
                var targetWorkflowId = $"workflow-{Guid.NewGuid()}";
                var callerHandle = await Client.StartWorkflowAsync(
                    (SystemNexusSignalWithStartCallerWorkflow workflow) =>
                        workflow.RunAsync(targetWorkflowId, worker.Options.TaskQueue!),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                var resultWorkflowId = await callerHandle.GetResultAsync();
                Assert.Equal(targetWorkflowId, resultWorkflowId);

                var targetHandle = Client.GetWorkflowHandle<
                    SystemNexusSignalWithStartTargetWorkflow,
                    IReadOnlyCollection<string>>(targetWorkflowId);
                var events = await targetHandle.GetResultAsync();
                Assert.Equal(3, events.Count);
                Assert.Contains("Started: start-value", events);
                Assert.Contains("Signal: signal-one", events);
                Assert.Contains("Signal: signal-two", events);

                var replayer = new WorkflowReplayer(
                    new WorkflowReplayerOptions().AddWorkflow<
                        SystemNexusSignalWithStartCallerWorkflow>());
                var replay = await replayer.ReplayWorkflowAsync(await callerHandle.FetchHistoryAsync());
                Assert.Null(replay.ReplayFailure);
            },
            workerOptions,
            Client);
    }

    [Fact]
    [CloudTestExclusion(
        CloudTestExclusionReason.RequiresLocalServer,
        "Requires local dynamic configuration to enable signal with start from a workflow.")]
    public async Task ExecuteWorkflowAsync_SignalWithStart_UsesTargetSerializationContext()
    {
        var encodings = ((DefaultPayloadConverter)DataConverter.Default.PayloadConverter).EncodingConverters;
        var recordedContexts = new ConcurrentDictionary<string, WorkflowWorkerTests.ContextInfo>();
        var contextualEncodings = encodings.Select(encoding =>
            encoding is JsonPlainConverter ? new RecordingContextJsonPlainConverter(recordedContexts) : encoding).ToArray();
        var payloadConverter = new DefaultPayloadConverter(contextualEncodings);
        var dataConverter = new DataConverter(
            payloadConverter,
            DataConverter.Default.FailureConverter);
        var clientOptions = (TemporalClientOptions)Client.Options.Clone();
        clientOptions.DataConverter = dataConverter;
        var client = new TemporalClient(Client.Connection, clientOptions);
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddWorkflow<SystemNexusContextTargetWorkflow>();

        await ExecuteWorkerAsync<SystemNexusContextCallerWorkflow>(
            async worker =>
            {
                var targetId = $"workflow-{Guid.NewGuid()}";
                var handle = await client.StartWorkflowAsync(
                    (SystemNexusContextCallerWorkflow workflow) =>
                        workflow.RunAsync(targetId, worker.Options.TaskQueue!),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                var context = await handle.GetResultAsync();
                Assert.True(context.Workflow);
                Assert.Equal(targetId, context.WorkflowId);
                Assert.Equal(
                    new[] { "context-details", "context-summary", "context-workflow" },
                    recordedContexts.Keys.OrderBy(value => value));
                Assert.All(recordedContexts.Values, value => Assert.Equal(targetId, value.WorkflowId));
            },
            workerOptions,
            client);
    }

    [Fact]
    [CloudTestExclusion(
        CloudTestExclusionReason.RequiresLocalServer,
        "Requires local dynamic configuration to enable signal with start from a workflow.")]
    public async Task ExecuteWorkflowAsync_SignalWithStart_DoesNotUseNormalNexusInterceptor()
    {
        var interceptor = new NormalNexusOperationInterceptor();
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddWorkflow<SystemNexusSignalWithStartTargetWorkflow>();
        workerOptions.Interceptors = new[] { interceptor };

        await ExecuteWorkerAsync<SystemNexusSignalWithStartCallerWorkflow>(
            async worker =>
            {
                var handle = await Client.StartWorkflowAsync(
                    (SystemNexusSignalWithStartCallerWorkflow workflow) =>
                        workflow.RunAsync($"workflow-{Guid.NewGuid()}", worker.Options.TaskQueue!),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await handle.GetResultAsync();
                Assert.Equal(0, interceptor.ScheduleNexusOperationCount);
                Assert.Equal(2, interceptor.ScheduleSystemNexusOperationCount);
            },
            workerOptions,
            Client);
    }

    [Workflow]
    public class SystemNexusSignalWithStartTargetWorkflow
    {
        private readonly List<string> events = new();

        [WorkflowRun]
        public async Task<IReadOnlyCollection<string>> RunAsync(string value)
        {
            events.Add($"Started: {value}");
            await Workflow.WaitConditionAsync(() => events.Count >= 3);
            return events;
        }

        [WorkflowSignal]
        public Task SignalAsync(string value)
        {
            events.Add($"Signal: {value}");
            return Task.CompletedTask;
        }
    }

    [Workflow]
    public class SystemNexusSignalWithStartCallerWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string workflowId, string taskQueue)
        {
            var handle = await Workflow.SignalWithStartWorkflowAsync(
                    (SystemNexusSignalWithStartTargetWorkflow workflow) =>
                        workflow.RunAsync("start-value"),
                    workflow => workflow.SignalAsync("signal-one"),
                    new(workflowId, taskQueue)
                    {
                        IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
                    });

            await Workflow.SignalWithStartWorkflowAsync(
                (SystemNexusSignalWithStartTargetWorkflow workflow) =>
                    workflow.RunAsync("unused-start-value"),
                workflow => workflow.SignalAsync("signal-two"),
                new(workflowId, taskQueue)
                {
                    IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
                });

            return handle.Id;
        }
    }

    [Workflow]
    public class SystemNexusContextTargetWorkflow
    {
        [WorkflowRun]
        public Task<string> RunAsync(WorkflowWorkerTests.ContextValue value) => Task.FromResult("done");

        [WorkflowSignal]
        public Task SignalAsync(WorkflowWorkerTests.ContextValue value) => Task.CompletedTask;
    }

    [Workflow]
    public class SystemNexusContextCallerWorkflow
    {
        [WorkflowRun]
        public async Task<WorkflowWorkerTests.ContextInfo> RunAsync(string targetId, string taskQueue)
        {
            var value = new WorkflowWorkerTests.ContextValue("context-workflow", new());
            await Workflow.SignalWithStartWorkflowAsync(
                (SystemNexusContextTargetWorkflow workflow) => workflow.RunAsync(value),
                workflow => workflow.SignalAsync(value),
                new(targetId, taskQueue)
                {
                    Memo = new Dictionary<string, object?> { ["context"] = value },
                    StaticSummary = "context-summary",
                    StaticDetails = "context-details",
                });
            return new WorkflowWorkerTests.ContextInfo(Workflow: true, WorkflowId: targetId);
        }
    }

    [Fact]
    public void SignalWithStartEnvelope_UsesSerializationContextForEveryContextAwarePayloadField()
    {
        var recordedContexts = new ConcurrentDictionary<string, WorkflowWorkerTests.ContextInfo>();
        var payloadConverter = CreateRecordingPayloadConverter(recordedContexts);
        var targetContext = new ISerializationContext.Workflow("target-namespace", "target-workflow-id");
        var contextualPayloadConverter =
            ((IWithSerializationContext<IPayloadConverter>)payloadConverter).WithSerializationContext(targetContext);
        var request = new SignalWithStartWorkflowRequest(
            "workflow",
            "target-workflow-id",
            "task-queue",
            "signal",
            "target-namespace")
        {
            Args = new[] { new WorkflowWorkerTests.ContextValue("context-workflow-arg", new()) },
            SignalArgs = new[] { new WorkflowWorkerTests.ContextValue("context-signal-arg", new()) },
            Memo = new Dictionary<string, object?>
            {
                ["context-memo"] = new WorkflowWorkerTests.ContextValue("context-memo", new()),
            },
            UserMetadata = new UserMetadata
            {
                StaticSummary = new WorkflowWorkerTests.ContextValue("context-summary", new()),
                StaticDetails = new WorkflowWorkerTests.ContextValue("context-details", new()),
            },
            Headers = new Dictionary<string, object?>
            {
                ["context-header"] = new WorkflowWorkerTests.ContextValue("context-header", new()),
            },
        };

        new SystemNexusPayloadConverter(contextualPayloadConverter, DataConverter.Default.FailureConverter).
            ToPayload(request);

        Assert.Equal(
            new[]
            {
                "context-details",
                "context-header",
                "context-memo",
                "context-signal-arg",
                "context-summary",
                "context-workflow-arg",
            },
            recordedContexts.Keys.OrderBy(value => value));
        Assert.All(recordedContexts.Values, context =>
        {
            Assert.True(context.Workflow);
            Assert.Equal("target-workflow-id", context.WorkflowId);
        });
    }

    private static DefaultPayloadConverter CreateRecordingPayloadConverter(
        ConcurrentDictionary<string, WorkflowWorkerTests.ContextInfo> recordedContexts)
    {
        var encodings = ((DefaultPayloadConverter)DataConverter.Default.PayloadConverter).EncodingConverters;
        return new DefaultPayloadConverter(encodings.Select(encoding =>
            encoding is JsonPlainConverter ? new RecordingContextJsonPlainConverter(recordedContexts) : encoding).ToArray());
    }

    private sealed class RecordingContextJsonPlainConverter : JsonPlainConverter,
        IWithSerializationContext<IEncodingConverter>
    {
        private readonly ConcurrentDictionary<string, WorkflowWorkerTests.ContextInfo> recordedContexts;
        private readonly WorkflowWorkerTests.ContextInfo? context;

        internal RecordingContextJsonPlainConverter(
            ConcurrentDictionary<string, WorkflowWorkerTests.ContextInfo> recordedContexts,
            WorkflowWorkerTests.ContextInfo? context = null)
            : base(new())
        {
            this.recordedContexts = recordedContexts;
            this.context = context;
        }

        public IEncodingConverter WithSerializationContext(ISerializationContext context) =>
            new RecordingContextJsonPlainConverter(
                recordedContexts,
                WorkflowWorkerTests.ContextInfo.Create(context));

        public override bool TryToPayload(object? value, out Temporalio.Api.Common.V1.Payload? payload)
        {
            if (context != null && value is string { } text && text.StartsWith("context-", StringComparison.Ordinal))
            {
                recordedContexts[text] = context;
            }
            if (context != null && value is WorkflowWorkerTests.ContextValue contextValue)
            {
                recordedContexts[contextValue.Name] = context;
            }
            return base.TryToPayload(value, out payload);
        }
    }

    private sealed class NormalNexusOperationInterceptor : IWorkerInterceptor
    {
        internal int ScheduleNexusOperationCount { get; private set; }

        internal int ScheduleSystemNexusOperationCount { get; private set; }

        public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) =>
            new Inbound(this, nextInterceptor);

        private sealed class Inbound : WorkflowInboundInterceptor
        {
            private readonly NormalNexusOperationInterceptor root;

            internal Inbound(NormalNexusOperationInterceptor root, WorkflowInboundInterceptor next)
                : base(next) => this.root = root;

            public override void Init(WorkflowOutboundInterceptor outbound) =>
                base.Init(new Outbound(root, outbound));
        }

        private sealed class Outbound : WorkflowOutboundInterceptor
        {
            private readonly NormalNexusOperationInterceptor root;

            internal Outbound(NormalNexusOperationInterceptor root, WorkflowOutboundInterceptor next)
                : base(next) => this.root = root;

            public override Task<NexusWorkflowOperationHandle<TResult>> ScheduleNexusOperationAsync<TResult>(
                ScheduleNexusOperationInput input)
            {
                root.ScheduleNexusOperationCount++;
                return base.ScheduleNexusOperationAsync<TResult>(input);
            }

            public override Task<NexusWorkflowOperationHandle<TResult>> ScheduleSystemNexusOperationAsync<TResult>(
                ScheduleSystemNexusOperationInput input)
            {
                root.ScheduleSystemNexusOperationCount++;
                return base.ScheduleSystemNexusOperationAsync<TResult>(input);
            }
        }
    }

    private static async Task ExecuteWorkerAsync<TWorkflow>(
        Func<TemporalWorker, Task> action,
        TemporalWorkerOptions options,
        IWorkerClient client)
    {
        options = (TemporalWorkerOptions)options.Clone();
        options.AddWorkflow<TWorkflow>();
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(client, options);
        await worker.ExecuteAsync(() => action(worker));
    }
}
