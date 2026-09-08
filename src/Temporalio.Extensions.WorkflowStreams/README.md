# Temporal .NET Workflow Streams

> [!WARNING]
> Workflow Streams is experimental. Its API and wire protocol may change before it is declared
> stable.

`Temporalio.Extensions.WorkflowStreams` provides a durable, offset-addressed, multi-topic log
hosted by a Temporal Workflow. External publishers append batches with a Signal, subscribers
long-poll with an Update, and a Query reports the current global offset. The implementation includes
publisher deduplication, topic filtering, truncation, response paging, and continue-as-new state
handoff.

This is intended for durable progress, event, and incremental-result streams. Each poll is a
Temporal Update round trip, so it is not intended for ultra-low-latency media or token streaming.

## Install

```shell
dotnet add package Temporalio.Extensions.WorkflowStreams
```

The package targets `netstandard2.0`.

## Host a stream in a Workflow

Create the stream while the Workflow instance is constructed. This ensures its dynamically
registered Signal, Update, and Query handlers exist before the first handler can be dispatched,
including on a continue-as-new successor run.

```csharp
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Workflows;

public record OrderInput(int CompletedSteps, WorkflowStreamState? StreamState = null);

[Workflow]
public class OrderWorkflow
{
    private readonly WorkflowStream stream;
    private bool finished;

    [WorkflowInit]
    public OrderWorkflow(OrderInput input)
    {
        stream = new(input.StreamState);
    }

    [WorkflowRun]
    public async Task RunAsync(OrderInput input)
    {
        stream.Topic("status").Publish(new { State = "started" });

        await Workflow.WaitConditionAsync(() => finished || Workflow.ContinueAsNewSuggested);
        if (Workflow.ContinueAsNewSuggested)
        {
            await stream.ContinueAsNewAsync(state =>
                Workflow.CreateContinueAsNewException(
                    (OrderWorkflow workflow) => workflow.RunAsync(
                        input with { StreamState = state })));
        }
    }

    [WorkflowSignal]
    public Task FinishAsync()
    {
        finished = true;
        return Task.CompletedTask;
    }
}
```

`ContinueAsNewAsync` first detaches admitted pollers, waits until all handlers finish, and only then
captures `WorkflowStreamState` and invokes the callback. Thread that state through the next run's
input as shown above. For custom continue-as-new options, perform the same sequence explicitly:
call `DetachPollers()`, wait for `Workflow.AllHandlersFinished`, call `GetState()`, and create the
continue-as-new exception yourself.

The log is retained until the Workflow calls `Truncate(offset)`. Offsets are global across every
topic. A subscriber that falls behind truncation automatically resumes at the beginning of the
retained log.

## Publish from a client or Activity

Use one asynchronously disposed client per target Workflow ID. Values are converted to Temporal
`Payload`s when `Publish` is called, then buffered until the two-second interval, the configured
batch size, a force flush, an explicit flush, or asynchronous disposal.

```csharp
await using var streams = new WorkflowStreamClient(temporalClient, workflowId);
var status = streams.Topic("status");

status.Publish(new { State = "working" });
status.Publish(new { State = "done" }, forceFlush: true);
await streams.FlushAsync(cancellationToken);
```

`FlushAsync` is a barrier for everything buffered before the call. Failed or timed-out Signal RPCs
retain the same publisher ID and sequence for retry, allowing the Workflow to deduplicate ambiguous
delivery. If the retry window expires, `FlushTimeoutException` is thrown and that ambiguous batch is
dropped locally: it may already be present in the Workflow log, or it may be lost. Later batches use
a new sequence.

Inside an Activity, `FromActivity` obtains the Temporal client, parent Workflow ID, and payload
converter from the current Activity context:

```csharp
[Activity]
public async Task ReportAsync(IEnumerable<Progress> values)
{
    await using var streams = WorkflowStreamClient.FromActivity();
    foreach (var value in values)
    {
        streams.Topic("progress").Publish(value);
    }
}
```

Always use `await using` or call `DisposeAsync` so the final buffer is drained and publisher resources
are released. Publication after disposal throws `ObjectDisposedException`.

## Subscribe

Subscriptions yield raw Temporal `Payload`s so consumers can choose a result type per topic. The
returned `IAsyncEnumerable<WorkflowStreamItem>` is reusable: each enumeration starts with its own
offset and polling state.

```csharp
var subscription = streams.SubscribeAsync(new()
{
    Topics = new[] { "status", "progress" },
    FromOffset = 0,
});

await foreach (var item in subscription.WithCancellation(cancellationToken))
{
    var value = matchingPayloadConverter.ToValue(item.Payload, typeof(MyEvent));
    Console.WriteLine($"{item.Offset} {item.Topic}: {value}");
}
```

For one topic, use `streams.Topic("status").SubscribeAsync(fromOffset)`. An empty topic collection
subscribes to every topic; the empty string is the cross-SDK no-topic value. Consumer cancellation
cancels the in-flight RPC and throws `OperationCanceledException`. Disposing the owning client ends
its active enumerations cleanly. Enumerations also end cleanly when the Workflow reaches a terminal
state and automatically follow continue-as-new chains.

## Data conversion and interoperability

The fixed protocol handlers are:

- Signal `__temporal_workflow_stream_publish`
- Update `__temporal_workflow_stream_poll`
- Query `__temporal_workflow_stream_offset`

The public wire DTOs use the protocol's exact snake-case JSON names. Each item's `data` is standard
padded base64 containing a serialized `temporal.api.common.v1.Payload` protobuf. This preserves its
encoding metadata and makes .NET publishers, Workflow hosts, and subscribers interoperable with the
official Workflow Streams implementations in other Temporal SDKs.

Only payload conversion is applied to an individual item. A client's payload codec chain, if any,
is applied once to the Signal or Update envelope rather than once per item, avoiding double encoding.
The envelope itself must use JSON-compatible conversion for cross-language interoperability.

## Operational limits

- Every waiting subscription uses an admitted Workflow Update. Account for concurrent and total
  Update limits when choosing subscriber counts and continue-as-new frequency.
- Poll results are paged at an estimated one megabyte and immediately repolled while another page is
  ready. An individual item must fit in one page.
- The Workflow log has no automatic retention policy. Truncate items once all required consumers have
  advanced, and carry only the retained state through continue-as-new.
- Publishing uses Signals, so malformed or oversized externally supplied wire entries cannot return
  errors to their sender. The Workflow skips them and emits a replay-safe warning.
