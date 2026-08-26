# Workflow Streams

A durable publish/subscribe log hosted inside a Temporal Workflow.

External code (activities, starters, other processes) publishes messages to
named topics via **signals**; subscribers long-poll for new items via
**updates**; a **query** exposes the current offset. The stream is backed by
Temporal's durable execution, giving ordered, durable, exactly-once delivery
with client-side batching, publisher dedup, continue-as-new survival,
truncation, and ~1 MB response paging.

It is well suited to durable event streams whose cost scales with durable
batches rather than message count. Each poll round-trip costs ~100 ms of
latency, so it is not intended for ultra-low-latency streaming.

All APIs in this package are experimental and may change.

## Workflow side

Construct a `WorkflowStream` once in a `[WorkflowInit]` constructor. The
constructor registers the publish signal, poll update, and offset query
handlers, and a `[WorkflowInit]` constructor runs before any handler dispatch,
so polls and offset queries arriving with the first workflow task (e.g. from
update-with-start) are accepted rather than rejected.

```csharp
public record MyInput(int ItemsProcessed, WorkflowStreamState? StreamState);

[Workflow]
public class MyWorkflow
{
    private readonly WorkflowStream stream;
    private bool done;

    [WorkflowInit]
    public MyWorkflow(MyInput input) => stream = new(input.StreamState);

    [WorkflowRun]
    public async Task RunAsync(MyInput input)
    {
        // Optionally publish from workflow code:
        stream.Topic("events").Publish("hello from the workflow");

        // Run your workflow; the stream serves external publishers and
        // subscribers for as long as the workflow is running. Block until your
        // workflow's exit condition is met (here, a `done` flag set elsewhere,
        // e.g. by a signal).
        await Workflow.WaitConditionAsync(() => done);
    }
}
```

Constructing the stream at the top of the workflow method also works — signals
received earlier are buffered by the SDK — but polls and offset queries are
rejected until the stream exists, so prefer `[WorkflowInit]`.

For workflows that use continue-as-new, the stream's log and offsets must be
carried across each boundary, since continue-as-new starts a fresh run with an
empty history. This is a round-trip with two halves:

- **Capture** the state when rolling over. Instead of throwing
  `Workflow.CreateContinueAsNewException` directly, call
  `stream.ContinueAsNewAsync`. It drains pollers, waits for in-flight handlers,
  snapshots the current stream state, and hands it to your callback, which
  builds the argument list for the next run. The callback is where you assemble
  the full input — carry forward your own workflow state alongside the captured
  `state` (it never returns; it always throws the continue-as-new exception):

  ```csharp
  await stream.ContinueAsNewAsync(state =>
      new object?[] { new MyInput(itemsProcessed, state) });
  ```

- **Restore** it on the next run. That `MyInput` arrives as the next run's
  input, and its `StreamState` field is the value already passed to the
  `WorkflowStream` constructor in the example above. It is `null` on a fresh
  start and non-null after a roll-over, so the stream rehydrates the log
  automatically.

The `WorkflowStreamState` field is what gives the captured stream state
somewhere to live between runs; the other fields on `MyInput` are your own and
are threaded through the same way.

## Publishing (client side)

From an activity, use `FromActivity` to target the parent workflow:

```csharp
[Activity]
public async Task PublishActivityAsync()
{
    await using var client = WorkflowStreamClient.FromActivity();
    var topic = client.Topic("events");
    for (var i = 0; i < 100; i++)
    {
        topic.Publish($"item {i}");
    }
    // DisposeAsync flushes the remaining buffer.
}
```

From a starter or any code with an `ITemporalClient`, use the constructor with
an explicit workflow ID:

```csharp
await using var client = new WorkflowStreamClient(temporalClient, workflowId);
client.Topic("events").Publish("from outside", forceFlush: true);
```

Items are buffered and flushed automatically every batch interval (default 2s),
when the buffer reaches the max batch size, on `forceFlush`, on an explicit
`FlushAsync()`, or on `CloseAsync()`.

Prefer `await using` or an explicit `CloseAsync()` over synchronous `Dispose()`.
The final flush can fail, including with `FlushTimeoutException`; synchronous
disposal blocks and can replace an exception already leaving a `using` body.

## Subscribing

There are two subscriber APIs over one shared poll engine: a non-blocking
listener and an async iterator. Polling is fully async — no thread is occupied
while a poll is blocked on the server — so many concurrent subscriptions do not
mean many threads. Either way, the subscription ends cleanly when the workflow
reaches a terminal state, automatically follows continue-as-new chains,
recovers from truncation by restarting from the current base offset, and also
ends when the owning `WorkflowStreamClient` is closed.

Items carry the raw `Temporalio.Api.Common.V1.Payload`; decode at the call site
with your payload converter. Offsets are **global** (across all topics), not
per-topic.

### Listener (non-blocking)

Pass a `WorkflowStreamListener` to `Subscribe` to have items delivered.
Callbacks are serialized (never invoked concurrently), run on the thread pool,
and must not block; the `Task` returned by `OnNextAsync` is the backpressure
boundary — return a completed task to receive the next item immediately, or a
pending task to defer further delivery and polling until it completes:

```csharp
var options = new SubscribeOptions
{
    Topics = { "events" }, // empty = all topics
};
var handle = client.Subscribe(options, new MyListener());

class MyListener : WorkflowStreamListener
{
    public override Task OnNextAsync(WorkflowStreamItem item)
    {
        var value = DataConverter.Default.PayloadConverter.ToValue<string>(item.Payload);
        Console.WriteLine($"offset={item.Offset} topic={item.Topic} value={value}");
        return Task.CompletedTask; // or a pending task to apply backpressure
    }

    public override void OnCompleted() => Console.WriteLine("stream ended");
}
```

`handle.Dispose()` stops the subscription before the next poll (without calling
`OnCompleted`); `handle.Completion` completes when the subscription ends —
normally on a clean end or dispose, faulted with the failure passed to
`OnError`.

### Async iterator

`Subscribe` without a listener returns a single-use subscription you can pull
from with `await foreach` (on .NET Core targets) or `MoveNextAsync()`:

```csharp
using var subscription = client.Subscribe(options);
await foreach (var item in subscription)
{
    var value = DataConverter.Default.PayloadConverter.ToValue<string>(item.Payload);
    Console.WriteLine($"offset={item.Offset} topic={item.Topic} value={value}");
}
```

`Dispose()` stops it before the next poll; items already fetched still drain.
An unrecoverable poll failure is rethrown from `MoveNextAsync()`.

## Options

| Option | Default | Meaning |
| --- | --- | --- |
| `WorkflowStreamClientOptions.BatchInterval` | 2s | Automatic flush interval |
| `WorkflowStreamClientOptions.MaxBatchSize` | unset | Flush once the buffer reaches this size |
| `WorkflowStreamClientOptions.MaxRetryDuration` | 10m | Max time to retry a failed flush before `FlushTimeoutException`. Must be < the workflow's publisher TTL (15m) to preserve exactly-once delivery |
| `WorkflowStreamClientOptions.PayloadConverter` | client's converter | Per-item serialization. Payload conversion only — the client's codec chain runs once on the envelope, never per item |
| `SubscribeOptions.PollCooldown` | 100ms | Min interval between polls |

Polling is fully asynchronous; unlike some other SDKs there is no poll executor
option, because no threads are held while a poll is blocked on the server.

## Cross-language protocol

The handler names (`WorkflowStreamConstants.PublishSignalName`,
`PollUpdateName`, `OffsetQueryName`), the JSON envelope field names, and the
per-item payload encoding (base64 of the serialized
`temporal.api.common.v1.Payload`) match other languages' packages exactly, so a
.NET publisher or subscriber interoperates with a workflow written in any of
them and vice versa. The data converter codec chain (encryption, compression)
runs once on the signal/update envelope — never per item — so payloads are not
double-encoded.

The protocol envelope types are serialized by the workflow's and client's
*configured* data converter. The default converter produces the wire-compatible
snake_case field names (the types are annotated with `JsonPropertyName`); a
custom converter must produce the same field names for cross-language interop.
