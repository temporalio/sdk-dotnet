namespace Temporalio.Tests.Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Coverage that the standalone Nexus client scopes its data converter to the endpoint, service and
/// operation it is acting on, and that a handle obtained by operation ID — which has no endpoint,
/// service or operation — serializes without a context instead.
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
    public async Task Describe_ReadsTheUncontextualizedSummary()
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

            // The summary is encoded without a Nexus context, so describe has to read it back the
            // same way. Decoding it under a context the encoder never used would corrupt it.
            var description = await handle.DescribeAsync();
            Assert.Equal("the-summary", await description.GetStaticSummaryAsync());
        });
    }

    [Fact]
    public async Task HandleObtainedById_HasNoContext()
    {
        await RunAsync(async (client, codec, endpoint) =>
        {
            var nexusClient = client.CreateNexusClient<IContextService>(endpoint);
            var started = await nexusClient.StartNexusOperationAsync<string>(
                svc => svc.Echo("hello"),
                new($"op-{Guid.NewGuid()}") { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
            await started.GetResultAsync();
            codec.Reset();

            // A handle obtained by ID never saw a start request, so there is no endpoint, service
            // or operation to scope its converter by. Decoding still has to work: a payload encoded
            // under a context must stay readable without one.
            var detached = client.GetNexusOperationHandle<string>(started.Id, started.RunId);
            Assert.Equal("echo:hello", await detached.GetResultAsync());
        });
    }

    private static ISerializationContext.Nexus Expected(string endpoint) =>
        new(endpoint, "ContextService", nameof(IContextService.Echo));

    private Task RunAsync(Func<ITemporalClient, RecordingCodec, string, Task> testFunc) =>
        RunAsync((client, codec, endpoint, _) => testFunc(client, codec, endpoint));

    private async Task RunAsync(
        Func<ITemporalClient, RecordingCodec, string, RecordingFailureConverter, Task> testFunc)
    {
        // Both sides share the codec so a signature written by one is checked by the other. Any
        // payload the SDK encodes and decodes under different contexts therefore fails the decode,
        // the way a codec keyed on the context would. Only the client gets the recording failure
        // converter, so the contexts it records are the client's.
        var codec = new RecordingCodec();
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

    /// <summary>
    /// Records every serialization context it is handed and tags each payload it encodes with the
    /// context used, refusing to decode a payload under a different context than encoded it.
    /// </summary>
    private class RecordingCodec : IPayloadCodec, IWithSerializationContext<IPayloadCodec>
    {
        private const string SignatureKey = "ser-ctx-signature";

        private readonly List<ISerializationContext?> seen;
        private readonly ISerializationContext? context;

        public RecordingCodec()
            : this(new List<ISerializationContext?>(), null)
        {
        }

        private RecordingCodec(List<ISerializationContext?> seen, ISerializationContext? context)
        {
            this.seen = seen;
            this.context = context;
        }

        // Shared by every instance derived via WithSerializationContext, so a test sees them all.
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

        public IPayloadCodec WithSerializationContext(ISerializationContext context) =>
            new RecordingCodec(seen, context);

        public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads)
        {
            Record();
            if (context is not ISerializationContext.Nexus nexus)
            {
                return Task.FromResult(payloads);
            }
            var signature = Signature(nexus);
            return Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
            {
                var copy = p.Clone();
                copy.Metadata[SignatureKey] = ByteString.CopyFromUtf8(signature);
                return copy;
            }).ToList());
        }

        public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads)
        {
            Record();
            return Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
            {
                if (!p.Metadata.TryGetValue(SignatureKey, out var signature))
                {
                    // Decoding under a Nexus context something that was encoded without one means
                    // the SDK picked different contexts for the two halves of a round trip. A codec
                    // keyed on the context (e.g. a per-endpoint encryption key) could not recover
                    // this payload.
                    Assert.False(
                        context is ISerializationContext.Nexus,
                        $"payload encoded without a context was decoded under {context}");
                    return p;
                }
                if (context is ISerializationContext.Nexus nexus)
                {
                    Assert.Equal(Signature(nexus), signature.ToStringUtf8());
                }
                var copy = p.Clone();
                copy.Metadata.Remove(SignatureKey);
                return copy;
            }).ToList());
        }

        private static string Signature(ISerializationContext.Nexus nexus) =>
            $"{nexus.Endpoint}:{nexus.Service}:{nexus.Operation}";

        private void Record()
        {
            lock (seen)
            {
                seen.Add(context);
            }
        }
    }
}
