namespace Temporalio.Tests.Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Coverage that the standalone Nexus client scopes its data converter to the endpoint, service and
/// operation it is acting on, and that a handle obtained by operation ID — which has no endpoint,
/// service or operation — decodes with the context the server returns.
/// </summary>
[CloudTestExclusion(
    CloudTestExclusionReason.RequiresCloudProvisioning,
    "Requires a Cloud namespace with Standalone Nexus Operations enabled.")]
public class TemporalClientNexusSerializationContextTests : WorkflowEnvironmentTestBase
{
    public TemporalClientNexusSerializationContextTests(
        ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [NexusService]
    public interface IContextService
    {
        [NexusOperation]
        string Echo(string input);
    }

    [NexusServiceHandler(typeof(IContextService))]
    public class ContextServiceHandler
    {
        public const string FailInput = "please-fail";
        public const string HandlerFailInput = "please-fail-handler";

        [NexusOperationHandler]
        public IOperationHandler<string, string> Echo() =>
            OperationHandler.Sync<string, string>((ctx, input) =>
                input == FailInput
                    // Details give the failure an actual payload, so the codec sees it and the
                    // strict signature check cross-checks the handler's encode context against the
                    // caller's decode context. Without details the failure carries no payloads and
                    // the codec is never consulted.
                    ? throw OperationException.CreateFailed(
                        "operation failed on purpose",
                        new ApplicationFailureException(
                            "root cause",
                            errorType: "ContextFailure",
                            details: new object?[] { "failure-detail" }))
                    : input == HandlerFailInput
                    ? throw new HandlerException(
                        HandlerErrorType.BadRequest,
                        "handler failed on purpose",
                        new ApplicationFailureException(
                            "root cause",
                            errorType: "ContextFailure",
                            details: new object?[] { "failure-detail" }))
                    : $"echo:{input}");
    }

    [Fact]
    public async Task StartedHandle_UsesItsStartRequestContext()
    {
        await RunAsync(async (client, codec, endpoint) =>
        {
            var nexusClient = client.CreateNexusClient<IContextService>(endpoint);
            var result = await nexusClient.ExecuteNexusOperationAsync<string>(
                svc => svc.Echo("hello"),
                new($"op-{Guid.NewGuid()}") { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });

            Assert.Equal("echo:hello", result);
            Assert.Contains(Expected(endpoint), codec.NexusContexts);
        });
    }

    [Fact]
    public async Task Failure_UsesTheOperationsContext()
    {
        await RunAsync(async (client, codec, endpoint, failureConverter) =>
        {
            var nexusClient = client.CreateNexusClient<IContextService>(endpoint);
            var handle = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo(ContextServiceHandler.FailInput),
                new($"op-{Guid.NewGuid()}") { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
            // Ignore what the start request itself converted, so only the failure path is observed.
            failureConverter.Reset();

            await Assert.ThrowsAsync<NexusOperationFailedException>(
                () => handle.GetResultAsync());

            Assert.Equal(
                new[] { Expected(endpoint) }, failureConverter.NexusContexts);
        });
    }

    [Fact]
    public async Task HandlerFailure_UsesTheOperationsContext()
    {
        await RunAsync(async (client, codec, endpoint) =>
        {
            var nexusClient = client.CreateNexusClient<IContextService>(endpoint);
            var handle = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo(ContextServiceHandler.HandlerFailInput),
                new($"op-{Guid.NewGuid()}") { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });

            // The worker encodes this failure after the handler has returned, outside the
            // operation's scope. The strict codec fails the caller's decode if the worker used a
            // different context.
            var exc = await Assert.ThrowsAsync<NexusOperationFailedException>(
                () => handle.GetResultAsync());
            var appFailure = Assert.IsType<ApplicationFailureException>(
                exc.InnerException?.InnerException);
            Assert.Equal("failure-detail", appFailure.Details.ElementAt<string>(0));
        });
    }

    [Fact]
    public async Task Describe_ReadsTheSummaryUnderTheOperationsContext()
    {
        await RunAsync(async (client, codec, endpoint) =>
        {
            var nexusClient = client.CreateNexusClient<IContextService>(endpoint);
            var handle = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo("hello"),
                new($"op-{Guid.NewGuid()}")
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                    Summary = "the-summary",
                });
            await handle.GetResultAsync();

            // User metadata is serialized with the operation's context, so describe has to read it
            // back under the same one. The strict codec fails a mismatch either way round.
            var description = await handle.DescribeAsync();
            Assert.Equal("the-summary", await description.GetStaticSummaryAsync());
        });
    }

    [Fact]
    public async Task HandleObtainedById_UsesTheContextTheServerReturns()
    {
        await RunAsync(async (client, codec, endpoint) =>
        {
            var nexusClient = client.CreateNexusClient<IContextService>(endpoint);
            var started = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo("hello"),
                new($"op-{Guid.NewGuid()}") { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
            await started.GetResultAsync();
            codec.Reset();
            Assert.Equal(endpoint, started.Endpoint);
            Assert.Equal("ContextService", started.Service);
            Assert.Equal(nameof(IContextService.Echo), started.Operation);

            // This handle never started the operation, so it relies on the context the server
            // returns with the result
            var detached = client.GetNexusOperationHandle<string>(started.Id, started.RunId);
            Assert.Null(detached.Endpoint);
            Assert.Null(detached.Service);
            Assert.Null(detached.Operation);
            Assert.Equal("echo:hello", await detached.GetResultAsync());
            Assert.Contains(Expected(endpoint), codec.NexusContexts);
        });
    }

    private static ISerializationContext.Nexus Expected(string endpoint) =>
        new(endpoint, "ContextService", nameof(IContextService.Echo));

    private Task RunAsync(
        Func<ITemporalClient, SigningRecordingCodec, string, Task> testFunc) =>
        RunAsync((client, codec, endpoint, _) => testFunc(client, codec, endpoint));

    private async Task RunAsync(
        Func<ITemporalClient, SigningRecordingCodec, string, RecordingFailureConverter, Task> testFunc)
    {
        // Both sides share the codec so a signature written by one is checked by the other. Any
        // payload the SDK encodes and decodes under different contexts therefore fails the decode,
        // the way a codec keyed on the context would. Only the client gets the recording failure
        // converter, so the contexts it records are the client's.
        var codec = new SigningRecordingCodec();
        var failureConverter = new RecordingFailureConverter();
        var clientOptions = (TemporalClientOptions)Client.Options.Clone();
        clientOptions.DataConverter = DataConverter.Default with
        {
            PayloadCodec = codec,
            FailureConverter = failureConverter,
        };
        var client = new TemporalClient(Client.Connection, clientOptions);

        var workerClientOptions = (TemporalClientOptions)Client.Options.Clone();
        workerClientOptions.DataConverter = DataConverter.Default with { PayloadCodec = codec };
        var workerClient = new TemporalClient(Client.Connection, workerClientOptions);

        var taskQueue = $"tq-{Guid.NewGuid()}";
        var endpointName = $"nexus-endpoint-{taskQueue}";
        await Env.TestEnv.CreateNexusEndpointAsync(endpointName, taskQueue);

        using var worker = new TemporalWorker(
            workerClient,
            new TemporalWorkerOptions(taskQueue).AddNexusService(new ContextServiceHandler()));
        await worker.ExecuteAsync(() => testFunc(client, codec, endpointName, failureConverter));
    }

    /// <summary>Records the Nexus contexts the SDK scopes failure conversion by.</summary>
    private class RecordingFailureConverter :
        IFailureConverter, IWithSerializationContext<IFailureConverter>
    {
        private readonly IFailureConverter inner = DataConverter.Default.FailureConverter;
        private readonly List<ISerializationContext?> seen;
        private readonly ISerializationContext? context;

        public RecordingFailureConverter()
            : this(new List<ISerializationContext?>(), null)
        {
        }

        private RecordingFailureConverter(
            List<ISerializationContext?> seen, ISerializationContext? context)
        {
            this.seen = seen;
            this.context = context;
        }

        public IReadOnlyList<ISerializationContext.Nexus> NexusContexts
        {
            get
            {
                lock (seen)
                {
                    return seen.OfType<ISerializationContext.Nexus>().ToList();
                }
            }
        }

        public void Reset()
        {
            lock (seen)
            {
                seen.Clear();
            }
        }

        public IFailureConverter WithSerializationContext(ISerializationContext context) =>
            new RecordingFailureConverter(seen, context);

        public Api.Failure.V1.Failure ToFailure(
            Exception exception, IPayloadConverter payloadConverter)
        {
            Record();
            return inner.ToFailure(exception, payloadConverter);
        }

        public Exception ToException(
            Api.Failure.V1.Failure failure, IPayloadConverter payloadConverter)
        {
            Record();
            return inner.ToException(failure, payloadConverter);
        }

        private void Record()
        {
            lock (seen)
            {
                seen.Add(context);
            }
        }
    }
}
