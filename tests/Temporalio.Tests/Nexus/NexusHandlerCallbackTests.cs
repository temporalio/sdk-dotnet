#pragma warning disable xUnit1013

namespace Temporalio.Tests.Nexus;

using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Activities;
using Temporalio.Api.Common.V1;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Nexus;
using Temporalio.Nexus.Notifications;
using Temporalio.Tests.Converters;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

[CloudTestExclusion(
    CloudTestExclusionReason.RequiresCloudProvisioning,
    "Requires a Cloud namespace with Standalone Nexus Operations and Nexus handler callbacks enabled.")]
public class NexusHandlerCallbackTests : WorkflowEnvironmentTestBase
{
    public NexusHandlerCallbackTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [NexusService]
    public interface ISourceService
    {
        [NexusOperation]
        TestOperationOutput Echo(string input);
    }

    [NexusService]
    public interface ICallbackService
    {
        [NexusOperation]
        OnCompleteResponse OnComplete(OnCompleteRequest<TestOperationOutput, TestCallbackContext> request);

        [NexusOperation]
        OnCompleteResponse OnCompleteRaw(OnCompleteRequest<RawValue, TestCallbackContext> request);
    }

    [NexusService]
    public interface IActivityCallbackService
    {
        [NexusOperation]
        OnCompleteResponse OnComplete(OnCompleteRequest<TestOperationOutput, TestCallbackContext> input);
    }

    public sealed record TestCallbackContext(string Value);

    public sealed record TestOperationOutput(string Value);

    public sealed record ActivityInput(TestCallbackContext SourceContext, TestOperationOutput SourceResult);

    [Workflow]
    public class SourceWorkflow
    {
        [WorkflowRun]
        public async Task<TestOperationOutput> RunAsync(string input)
        {
            if (input == "wait")
            {
                await Workflow.WaitConditionAsync(() => false);
            }
            if (input == "fail")
            {
                throw new ApplicationFailureException(
                    "source failed", errorType: "SourceFailure", details: new object?[] { "failure-detail" });
            }
            return new($"echo:{input}");
        }
    }

    [NexusServiceHandler(typeof(ISourceService))]
    public class SourceHandler
    {
        [NexusOperationHandler]
        public IOperationHandler<string, TestOperationOutput> Echo() =>
            WorkflowRunOperationHandler.FromHandleFactory(
                (WorkflowRunOperationContext context, string input) =>
                    context.StartWorkflowAsync(
                        (SourceWorkflow wf) => wf.RunAsync(input),
                        new() { Id = $"wf-{Guid.NewGuid()}" }));
    }

    [NexusServiceHandler(typeof(ICallbackService))]
    public class CallbackHandler
    {
        public TaskCompletionSource<OnCompleteRequest<TestOperationOutput, TestCallbackContext>> Typed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<OnCompleteRequest<RawValue, TestCallbackContext>> Raw { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        [NexusOperationHandler]
        public IOperationHandler<OnCompleteRequest<TestOperationOutput, TestCallbackContext>, OnCompleteResponse>
            OnComplete() =>
            OperationHandler.Sync<OnCompleteRequest<TestOperationOutput, TestCallbackContext>, OnCompleteResponse>(
                (ctx, request) =>
                {
                    Typed.TrySetResult(request);
                    return new OnCompleteResponse();
                });

        [NexusOperationHandler]
        public IOperationHandler<OnCompleteRequest<RawValue, TestCallbackContext>, OnCompleteResponse>
            OnCompleteRaw() =>
            OperationHandler.Sync<OnCompleteRequest<RawValue, TestCallbackContext>, OnCompleteResponse>(
                (ctx, request) =>
                {
                    Raw.TrySetResult(request);
                    return new OnCompleteResponse();
                });
    }

    [NexusServiceHandler(typeof(IActivityCallbackService))]
    public class ActivityCallbackHandler
    {
        [TemporalOperation]
        public Task<TemporalOperationResult<OnCompleteResponse>> OnComplete(
            TemporalOperationStartContext context,
            ITemporalNexusClient client,
            OnCompleteRequest<TestOperationOutput, TestCallbackContext> input)
        {
            if (input.Result is not OnCompleteRequestResult<TestOperationOutput>.Success success)
            {
                return Task.FromResult(
                    TemporalOperationResult<OnCompleteResponse>.SyncResult(new OnCompleteResponse()));
            }

            return client.StartActivityAsync<OnCompleteResponse>(
                "Complete",
                new object?[] { new ActivityInput(input.SourceContext, success.Value) },
                new()
                {
                    Id = $"callback-act-{input.SourceContext.Value}",
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });
        }
    }

    public class CallbackActivity
    {
        public TaskCompletionSource<ActivityInput> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        [Activity]
        public Task<OnCompleteResponse> CompleteAsync(ActivityInput input)
        {
            Received.TrySetResult(input);
            return Task.FromResult(new OnCompleteResponse());
        }
    }

    [Fact]
    public async Task StartAsync_MultipleCallbacks_ByNameAndExpression_ReachHandlers()
    {
        var callbacks = new CallbackHandler();
        var interceptor = new CallbackInterceptor();
        await RunAsync(
            callbacks,
            async (client, endpoint, queue) =>
        {
            var raw = CompletionCallback.Create(
                "CallbackService", nameof(ICallbackService.OnCompleteRaw), queue, new TestCallbackContext("raw"));
            var typed = CompletionCallback.Create(
                (ICallbackService svc, OnCompleteRequest<TestOperationOutput, TestCallbackContext> input) => svc.OnComplete(input),
                queue,
                new TestCallbackContext("typed"));
            var options = new NexusOperationOptions<TestOperationOutput>($"op-{Guid.NewGuid()}")
            {
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                CompletionCallbacks = new()
                {
                    raw,
                    typed,
                },
            };
            var handle = await client.CreateNexusClient<ISourceService>(endpoint).
                StartNexusOperationAsync(svc => svc.Echo("start"), options);
            Assert.Equal(new TestOperationOutput("echo:start"), await handle.GetResultAsync());

            var typedRequest = await callbacks.Typed.Task.WaitAsync(TimeSpan.FromMinutes(1));
            Assert.Equal(new TestCallbackContext("typed"), typedRequest.SourceContext);
            Assert.Equal(
                new TestOperationOutput("echo:start"),
                Assert.IsType<OnCompleteRequestResult<TestOperationOutput>.Success>(typedRequest.Result).Value);

            var rawRequest = await callbacks.Raw.Task.WaitAsync(TimeSpan.FromMinutes(1));
            Assert.Equal(new TestCallbackContext("raw"), rawRequest.SourceContext);
            Assert.Equal(
                new TestOperationOutput("echo:start"),
                DataConverter.Default.PayloadConverter.ToValue<TestOperationOutput>(
                    Assert.IsType<OnCompleteRequestResult<RawValue>.Success>(rawRequest.Result).Value.Payload));

            Assert.Collection(
                Assert.IsAssignableFrom<IReadOnlyCollection<CompletionCallback>>(interceptor.Callbacks),
                callback => Assert.Same(raw, callback),
                callback => Assert.Same(typed, callback));
            Assert.Same(options.CompletionCallbacks, interceptor.Callbacks);
        },
            interceptor: interceptor);
    }

    [Fact]
    public async Task ExecuteAsync_ByName_CallbackReceivesSuccess()
    {
        var callbacks = new CallbackHandler();
        await RunAsync(callbacks, async (client, endpoint, queue) =>
        {
            var result = await client.CreateNexusClient(endpoint, "SourceService").
                ExecuteNexusOperationAsync<TestOperationOutput>(
                    nameof(ISourceService.Echo),
                    "execute",
                    new NexusOperationOptions<TestOperationOutput>($"op-{Guid.NewGuid()}")
                    {
                        ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                        CompletionCallbacks = new()
                        {
                            {
                                "CallbackService",
                                nameof(ICallbackService.OnComplete),
                                queue,
                                new TestCallbackContext("execute")
                            },
                        },
                    });
            Assert.Equal(new TestOperationOutput("echo:execute"), result);
            var request = await callbacks.Typed.Task.WaitAsync(TimeSpan.FromMinutes(1));
            Assert.Equal(new TestCallbackContext("execute"), request.SourceContext);
            Assert.Equal(
                result,
                Assert.IsType<OnCompleteRequestResult<TestOperationOutput>.Success>(request.Result).Value);
        });
    }

    [Fact]
    public async Task StartAsync_RawExpression_ReceivesEncodedResultWithCodec()
    {
        var callbacks = new CallbackHandler();
        await RunAsync(
            callbacks,
            async (client, endpoint, queue) =>
        {
            var options = new NexusOperationOptions<TestOperationOutput>($"op-{Guid.NewGuid()}")
            {
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                CompletionCallbacks = new()
                {
                    {
                        (ICallbackService service, OnCompleteRequest<RawValue, TestCallbackContext> input) =>
                            service.OnCompleteRaw(input),
                        queue,
                        new TestCallbackContext("raw-factory")
                    },
                },
            };
            var handle = await client.CreateNexusClient<ISourceService>(endpoint).
                StartNexusOperationAsync(service => service.Echo("raw"), options);
            Assert.Equal(new TestOperationOutput("echo:raw"), await handle.GetResultAsync());

            var request = await callbacks.Raw.Task.WaitAsync(TimeSpan.FromMinutes(1));
            Assert.Equal(new TestCallbackContext("raw-factory"), request.SourceContext);
            var result = Assert.IsType<OnCompleteRequestResult<RawValue>.Success>(request.Result).Value;
            Assert.Equal(
                new TestOperationOutput("echo:raw"),
                DataConverter.Default.PayloadConverter.ToValue<TestOperationOutput>(result.Payload));
        },
            codec: true);
    }

    [Fact]
    public async Task FailedOperation_CallbackReceivesApplicationFailureAndDetailsWithCodec()
    {
        var callbacks = new CallbackHandler();
        await RunAsync(
            callbacks,
            async (client, endpoint, queue) =>
        {
            var handle = await client.CreateNexusClient<ISourceService>(endpoint).
                StartNexusOperationAsync(
                    svc => svc.Echo("fail"),
                    Options(queue, "failure"));
            await Assert.ThrowsAsync<NexusOperationFailedException>(() => handle.GetResultAsync());

            var request = await callbacks.Typed.Task.WaitAsync(TimeSpan.FromMinutes(1));
            Assert.Equal(new TestCallbackContext("failure"), request.SourceContext);
            var failure = Assert.IsType<OnCompleteRequestResult<TestOperationOutput>.Failure>(request.Result);
            var appFailure = Assert.IsType<ApplicationFailureException>(failure.Value);
            Assert.Equal("SourceFailure", appFailure.ErrorType);
            Assert.Equal("failure-detail", appFailure.Details.ElementAt<string>(0));
        },
            codec: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoppedOperation_CallbackReceivesFailure(bool terminate)
    {
        var callbacks = new CallbackHandler();
        await RunAsync(callbacks, async (client, endpoint, queue) =>
        {
            var handle = await client.CreateNexusClient<ISourceService>(endpoint).
                StartNexusOperationAsync(
                    svc => svc.Echo("wait"),
                    Options(queue, terminate ? "terminate" : "cancel"));
            if (terminate)
            {
                await handle.TerminateAsync();
            }
            else
            {
                await handle.CancelAsync();
            }

            var request = await callbacks.Typed.Task.WaitAsync(TimeSpan.FromMinutes(1));
            Assert.Equal(
                new TestCallbackContext(terminate ? "terminate" : "cancel"),
                request.SourceContext);
            var failure = Assert.IsType<OnCompleteRequestResult<TestOperationOutput>.Failure>(request.Result);
            if (terminate)
            {
                Assert.IsType<TerminatedFailureException>(failure.Value);
            }
            else
            {
                Assert.IsType<CanceledFailureException>(failure.Value);
            }
        });
    }

    [Fact]
    public async Task SuccessfulOperation_CallbackDecodesResultAndSourceContextWithCodec()
    {
        var callbacks = new CallbackHandler();
        await RunAsync(
            callbacks,
            async (client, endpoint, queue) =>
        {
            var result = await client.CreateNexusClient<ISourceService>(endpoint).
                ExecuteNexusOperationAsync(
                    svc => svc.Echo("codec"),
                    Options(queue, "codec-context"));
            var request = await callbacks.Typed.Task.WaitAsync(TimeSpan.FromMinutes(1));
            Assert.Equal(new TestOperationOutput("echo:codec"), result);
            Assert.Equal(new TestCallbackContext("codec-context"), request.SourceContext);
            Assert.Equal(
                result,
                Assert.IsType<OnCompleteRequestResult<TestOperationOutput>.Success>(request.Result).Value);
        },
            codec: true);
    }

    [Fact]
    public Task CompletedSource_CallbackStartsActivityWithDefaultConverter() =>
        AssertCallbackStartsActivityAsync(DataConverter.Default);

    [Fact]
    public Task CompletedSource_CallbackStartsActivityWithBase64Codec() =>
        AssertCallbackStartsActivityAsync(
            DataConverter.Default with { PayloadCodec = new Base64PayloadCodec() });

    public class RequestTests
    {
        [Fact]
        public async Task StartNexusOperationAsync_RawCallbackAcceptsValueTypeOutput()
        {
            var codec = new RecordingCodec();
            var client = TemporalClient.CreateLazy(new("localhost:1")
            {
                Namespace = "callbacks",
                DataConverter = DataConverter.Default with { PayloadCodec = codec },
            });
            var callbacks = new CompletionCallbackCollection<int>();
            callbacks.Add(
                (INotifications service, OnCompleteRequest<IRawValue, string> input) => service.OnCompleteRaw(input),
                "notifications",
                "request-context");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.CreateNexusClient<ISource>("source-endpoint").StartNexusOperationAsync(
                    service => service.Run(),
                    new NexusOperationOptions<int>("source-id")
                    {
                        CompletionCallbacks = callbacks,
                    }));

            Assert.Equal("Stopped before RPC", exception.Message);
            Assert.Single(codec.Payloads);
        }

        [Fact]
        public async Task StartNexusOperationAsync_TypedCallback_EncodesContext()
        {
            var codec = new RecordingCodec();
            var client = TemporalClient.CreateLazy(new("localhost:1")
            {
                Namespace = "callbacks",
                DataConverter = DataConverter.Default with { PayloadCodec = codec },
            });
            var callbacks = new CompletionCallbackCollection<string>();
            callbacks.Add(
                (INotifications service, OnCompleteRequest<string, string> input) => service.OnComplete(input),
                "notifications",
                "request-context");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.CreateNexusClient<ISource>("source-endpoint").StartNexusOperationAsync(
                    service => service.Echo(),
                    new NexusOperationOptions<string>("source-id") { CompletionCallbacks = callbacks }));

            Assert.Equal("Stopped before RPC", exception.Message);
            Assert.Single(codec.Payloads);
        }

        [Fact]
        public async Task StartNexusOperationAsync_NamedOperationWithIncompatibleCallback_ReachesEncoding()
        {
            var codec = new RecordingCodec();
            var client = TemporalClient.CreateLazy(new("localhost:1")
            {
                Namespace = "callbacks",
                DataConverter = DataConverter.Default with { PayloadCodec = codec },
            });
            var callbacks = new CompletionCallbackCollection<string>();
            callbacks.Add(
                (INotifications service, OnCompleteRequest<string, string> input) => service.OnComplete(input),
                "notifications",
                "request-context");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.CreateNexusClient<ISource>("source-endpoint").StartNexusOperationAsync<string>(
                    nameof(ISource.Run),
                    null,
                    new NexusOperationOptions<string>("source-id") { CompletionCallbacks = callbacks }));

            Assert.Equal("Stopped before RPC", exception.Message);
            Assert.Single(codec.Payloads);
        }

        [Fact]
        public async Task StartNexusOperationAsync_Callback_UsesSourceContextAndCodec()
        {
            var codec = new RecordingCodec();
            var client = TemporalClient.CreateLazy(new("localhost:1")
            {
                Namespace = "callbacks",
                DataConverter = DataConverter.Default with { PayloadCodec = codec },
            });
            var callbacks = new CompletionCallbackCollection<int>();
            callbacks.Add(
                service: "Notifications",
                operation: "OnComplete",
                taskQueue: "notifications",
                sourceContext: "request-context");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.CreateNexusClient<ISource>("source-endpoint").StartNexusOperationAsync(
                    service => service.Run(),
                    new NexusOperationOptions<int>("source-id")
                    {
                        CompletionCallbacks = callbacks,
                    }));

            Assert.Equal("Stopped before RPC", exception.Message);
            Assert.Equal(
                new ISerializationContext.Nexus("source-endpoint", "Source", "Run"),
                codec.Context);
            Assert.Equal("\"request-context\"", Assert.Single(codec.Payloads).Data.ToStringUtf8());
        }

        [Fact]
        public async Task StartNexusOperationAsync_NamedRawCallbackAcceptsAnyOutput()
        {
            var codec = new RecordingCodec();
            var client = TemporalClient.CreateLazy(new("localhost:1")
            {
                Namespace = "callbacks",
                DataConverter = DataConverter.Default with { PayloadCodec = codec },
            });
            var callbacks = new CompletionCallbackCollection<IRawValue>();
            callbacks.Add("Notifications", "OnCompleteRaw", "notifications", "request-context");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.CreateNexusClient("source-endpoint", "Source").StartNexusOperationAsync(
                    "Run",
                    null,
                    new NexusOperationOptions<IRawValue>("source-id") { CompletionCallbacks = callbacks }));

            Assert.Equal("Stopped before RPC", exception.Message);
            Assert.Single(codec.Payloads);
        }

        [NexusService("Source")]
        public interface ISource
        {
            [NexusOperation]
            int Run();

            [NexusOperation]
            string Echo();
        }

        [NexusService("Notifications")]
        public interface INotifications
        {
            [NexusOperation]
            OnCompleteResponse OnComplete(OnCompleteRequest<string, string> input);

            [NexusOperation]
            OnCompleteResponse OnCompleteRaw(OnCompleteRequest<IRawValue, string> input);
        }

        private sealed class RecordingCodec :
            IPayloadCodec, IWithSerializationContext<IPayloadCodec>
        {
            public List<Payload> Payloads { get; } = new();

            public ISerializationContext? Context { get; private set; }

            public IPayloadCodec WithSerializationContext(ISerializationContext context)
            {
                Context = context;
                return this;
            }

            public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads)
            {
                Payloads.AddRange(payloads);
                throw new InvalidOperationException("Stopped before RPC");
            }

            public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads) =>
                throw new NotSupportedException();
        }
    }

    private static NexusOperationOptions<TestOperationOutput> Options(string queue, string context) =>
        new($"op-{Guid.NewGuid()}")
        {
            ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
            CompletionCallbacks = new()
            {
                {
                    (ICallbackService svc, OnCompleteRequest<TestOperationOutput, TestCallbackContext> input) => svc.OnComplete(input),
                    queue,
                    new TestCallbackContext(context)
                },
            },
        };

    private async Task RunAsync(
        CallbackHandler callbacks,
        Func<ITemporalClient, string, string, Task> test,
        bool codec = false,
        CallbackInterceptor? interceptor = null)
    {
        var options = (TemporalClientOptions)Client.Options.Clone();
        if (codec)
        {
            options.DataConverter = DataConverter.Default with { PayloadCodec = new Base64PayloadCodec() };
        }
        if (interceptor != null)
        {
            options.Interceptors = new IClientInterceptor[] { interceptor };
        }
        var client = new TemporalClient(Client.Connection, options);
        var queue = $"tq-{Guid.NewGuid()}";
        var endpoint = $"nexus-endpoint-{queue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpoint, queue);
        using var worker = new TemporalWorker(
            client,
            new TemporalWorkerOptions(queue).
                AddNexusService(new SourceHandler()).
                AddNexusService(callbacks).
                AddWorkflow<SourceWorkflow>());
        await worker.ExecuteAsync(() => test(client, endpoint, queue));
    }

    private async Task AssertCallbackStartsActivityAsync(DataConverter dataConverter)
    {
        var options = (TemporalClientOptions)Client.Options.Clone();
        options.DataConverter = dataConverter;
        var client = new TemporalClient(Client.Connection, options);
        var queue = $"tq-{Guid.NewGuid()}";
        var endpoint = $"nexus-endpoint-{queue}";
        var context = new TestCallbackContext(Guid.NewGuid().ToString());
        var activityId = $"callback-act-{context.Value}";
        var activity = new CallbackActivity();
        await Env.TestEnv.CreateNexusEndpointAsync(endpoint, queue);
        using var worker = new TemporalWorker(
            client,
            new TemporalWorkerOptions(queue).
                AddNexusService(new SourceHandler()).
                AddNexusService(new ActivityCallbackHandler()).
                AddWorkflow<SourceWorkflow>().
                AddActivity(activity.CompleteAsync));
        await worker.ExecuteAsync(async () =>
        {
            var callback = CompletionCallback.Create(
                (IActivityCallbackService svc, OnCompleteRequest<TestOperationOutput, TestCallbackContext> input) => svc.OnComplete(input),
                queue,
                context);
            var handle = await client.CreateNexusClient<ISourceService>(
                endpoint).StartNexusOperationAsync(
                    svc => svc.Echo("from-source"),
                    new NexusOperationOptions<TestOperationOutput>($"op-{Guid.NewGuid()}")
                    {
                        ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                        CompletionCallbacks = new() { callback },
                    });
            var sourceResult = await handle.GetResultAsync().WaitAsync(TimeSpan.FromMinutes(1));
            Assert.Equal(new TestOperationOutput("echo:from-source"), sourceResult);
            Assert.Equal(
                new ActivityInput(context, sourceResult),
                await activity.Received.Task.WaitAsync(TimeSpan.FromMinutes(1)));
            Assert.IsType<OnCompleteResponse>(
                await client.GetActivityHandle<OnCompleteResponse>(activityId).
                    GetResultAsync().WaitAsync(TimeSpan.FromMinutes(1)));
        });
    }

    private class CallbackInterceptor : IClientInterceptor
    {
        public IReadOnlyCollection<CompletionCallback>? Callbacks { get; private set; }

        public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor next) =>
            new CallbackOutboundInterceptor(next, this);

        private class CallbackOutboundInterceptor : ClientOutboundInterceptor
        {
            private readonly CallbackInterceptor owner;

            public CallbackOutboundInterceptor(ClientOutboundInterceptor next, CallbackInterceptor owner)
                : base(next) =>
                this.owner = owner;

            public override Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
                StartNexusOperationInput input)
            {
                owner.Callbacks = input.Options.CompletionCallbacks;
                return base.StartNexusOperationAsync<TResult>(input);
            }
        }
    }
}
