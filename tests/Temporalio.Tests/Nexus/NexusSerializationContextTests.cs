namespace Temporalio.Tests.Nexus;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Nexus;
using Temporalio.Worker;
using Xunit;

/// <summary>
/// Server-independent coverage that Nexus payloads are serialized under the endpoint, service and
/// operation they belong to, and that the absence of a Nexus operation is handled without one.
/// </summary>
public class NexusSerializationContextTests
{
    private const string Endpoint = "handler-endpoint";
    private const string Service = "svc";
    private const string Operation = "op";

    [Fact]
    public void SerializationContext_BuiltFromTheOperationBeingHandled()
    {
        var context = NewExecutionContext();

        Assert.Equal(
            new ISerializationContext.Nexus(Endpoint, Service, Operation),
            context.SerializationContext);
    }

    [Fact]
    public void SerializationContext_NoEndpointReported_IsNull()
    {
        // Servers before 1.30.0 do not report the endpoint the task was addressed to. Scoping by an
        // empty endpoint would silently disagree with the caller, which scoped by the real one.
        Assert.Null(NewExecutionContext(endpoint: string.Empty).SerializationContext);
    }

    [Fact]
    public async Task SerializeAsync_NoEndpointReported_UsesNoContext()
    {
        var codec = new RecordingCodec();
        var serializer = new NexusPayloadSerializer(DataConverter.Default with
        {
            PayloadCodec = codec,
        });

        await WithOperationInScopeAsync(
            () => serializer.SerializeAsync("value"), endpoint: string.Empty);

        Assert.Equal(new ISerializationContext?[] { null }, codec.Contexts);
    }

    [Fact]
    public async Task SerializeAsync_WithOperationInScope_UsesOperationContext()
    {
        var codec = new RecordingCodec();
        var serializer = new NexusPayloadSerializer(DataConverter.Default with
        {
            PayloadCodec = codec,
        });

        await WithOperationInScopeAsync(() => serializer.SerializeAsync("value"));

        Assert.Equal(
            new[] { new ISerializationContext.Nexus(Endpoint, Service, Operation) },
            codec.NexusContexts);
    }

    [Fact]
    public async Task SerializeAsync_WithNoOperationInScope_UsesNoContext()
    {
        // The serializer is shared by the whole worker and is reachable outside a Nexus task, where
        // there is no endpoint, service or operation to scope it by.
        var codec = new RecordingCodec();
        var serializer = new NexusPayloadSerializer(DataConverter.Default with
        {
            PayloadCodec = codec,
        });

        await serializer.SerializeAsync("value");

        Assert.Equal(new ISerializationContext?[] { null }, codec.Contexts);
    }

    [Fact]
    public async Task DeserializeAsync_WithOperationInScope_UsesOperationContext()
    {
        var codec = new RecordingCodec();
        var converter = DataConverter.Default with { PayloadCodec = codec };
        var serializer = new NexusPayloadSerializer(converter);

        // The caller encodes under the operation's context, so the handler must decode under the
        // same context to read it back.
        var expected = new ISerializationContext.Nexus(Endpoint, Service, Operation);
        var payload = await converter.WithSerializationContext(expected)
            .ToPayloadAsync("handler-input");
        codec.Reset();

        var result = await WithOperationInScopeAsync(
            () => serializer.DeserializeAsync(
                new ISerializer.Content(payload.ToByteArray()), typeof(string)));

        Assert.Equal("handler-input", result);
        Assert.Equal(new[] { expected }, codec.NexusContexts);
    }

    [Fact]
    public async Task Handle_PartiallyIdentifiedOperation_Throws()
    {
        // A partially identified operation would decode without a Nexus context, which for a
        // converter that varies by context means reading the payload the wrong way, not failing.
        var handle = new Temporalio.Client.NexusOperationHandle<string>(
            Client: null!, Id: "op-1")
        {
            Endpoint = Endpoint,
            Operation = Operation,
        };

        var exc = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handle.GetResultAsync());
        Assert.Contains("must all be set or all be null", exc.Message);
    }

    // Awaits inside the scope so the context has to survive the continuation, not just the
    // synchronous prologue of the call.
    private static async Task<T> WithOperationInScopeAsync<T>(
        Func<Task<T>> action, string endpoint = Endpoint)
    {
        NexusOperationExecutionContext.AsyncLocalCurrent.Value = NewExecutionContext(endpoint);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            NexusOperationExecutionContext.AsyncLocalCurrent.Value = null;
        }
    }

    private static NexusOperationExecutionContext NewExecutionContext(string endpoint = Endpoint) =>
        new(
            handlerContext: new OperationStartContext(
                Service: Service,
                Operation: Operation,
                CancellationToken: CancellationToken.None,
                RequestId: Guid.NewGuid().ToString()),
            info: new("ns", "tq", endpoint),
            logger: NullLogger.Instance,
            runtimeMetricMeter: new Lazy<MetricMeter>(
                () => throw new InvalidOperationException("metric meter not expected in test")),
            temporalClient: null);

    /// <summary>
    /// Records every serialization context it is handed, so a test can assert which contexts the
    /// SDK scoped a conversion by.
    /// </summary>
    private class RecordingCodec : IPayloadCodec, IWithSerializationContext<IPayloadCodec>
    {
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
        public IReadOnlyList<ISerializationContext?> Contexts
        {
            get
            {
                lock (seen)
                {
                    return seen.ToList();
                }
            }
        }

        public IReadOnlyList<ISerializationContext.Nexus> NexusContexts =>
            Contexts.OfType<ISerializationContext.Nexus>().ToList();

        public void Reset()
        {
            lock (seen)
            {
                seen.Clear();
            }
        }

        public IPayloadCodec WithSerializationContext(ISerializationContext context) =>
            new RecordingCodec(seen, context);

        public async Task<IReadOnlyCollection<Payload>> EncodeAsync(
            IReadOnlyCollection<Payload> payloads)
        {
            // Yield first so the recorded context is the one that survived the continuation.
            await Task.Yield();
            Record();
            return payloads;
        }

        public async Task<IReadOnlyCollection<Payload>> DecodeAsync(
            IReadOnlyCollection<Payload> payloads)
        {
            await Task.Yield();
            Record();
            return payloads;
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
