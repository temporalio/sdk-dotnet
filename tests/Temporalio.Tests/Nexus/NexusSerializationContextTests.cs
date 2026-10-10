namespace Temporalio.Tests.Nexus;

using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using NexusRpc;
using NexusRpc.Handlers;
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
    public async Task SerializationContext_TaskWithoutAnEndpoint_IsScopedByAnEmptyEndpoint()
    {
        // Servers before 1.30.0 do not report the endpoint a Nexus task was addressed to. The
        // handler still uses a Nexus context there, with an empty endpoint, rather than none.
        var expected = new ISerializationContext.Nexus(string.Empty, Service, Operation);
        Assert.Equal(expected, NewExecutionContext(string.Empty).SerializationContext);

        var codec = new RecordingPayloadCodec();
        var serializer = new NexusPayloadSerializer(DataConverter.Default with
        {
            PayloadCodec = codec,
        });

        await WithOperationInScopeAsync(
            () => serializer.SerializeAsync("value"), endpoint: string.Empty);

        Assert.Equal(new[] { expected }, codec.NexusContexts);
    }

    [Fact]
    public async Task SerializeAsync_WithOperationInScope_UsesOperationContext()
    {
        var codec = new RecordingPayloadCodec();
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
    public async Task AsyncLocalCurrent_DoesNotEscapeTheAsyncScopeThatSetIt()
    {
        // Why WithOperationInScopeAsync does not have to restore the previous value itself.
        NexusOperationExecutionContext.AsyncLocalCurrent.Value = null;

        await WithOperationInScopeAsync(() => Task.FromResult(string.Empty));

        Assert.Null(NexusOperationExecutionContext.AsyncLocalCurrent.Value);
    }

    [Fact]
    public async Task SerializeAsync_WithNoOperationInScope_UsesNoContext()
    {
        // The serializer is shared by the whole worker and is reachable outside a Nexus task, where
        // there is no endpoint, service or operation to scope it by.
        var codec = new RecordingPayloadCodec();
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
        var codec = new RecordingPayloadCodec();
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

    // Awaits inside the scope so the context has to survive the continuation, not just the
    // synchronous prologue of the call.
    private static async Task<T> WithOperationInScopeAsync<T>(
        Func<Task<T>> action, string endpoint = Endpoint)
    {
        NexusOperationExecutionContext.AsyncLocalCurrent.Value = NewExecutionContext(endpoint);
        return await action().ConfigureAwait(false);
    }

    private static NexusOperationExecutionContext NewExecutionContext(
        string endpoint = Endpoint) =>
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
}
