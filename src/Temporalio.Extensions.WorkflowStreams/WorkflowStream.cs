using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Extensions.WorkflowStreams.Internal;
using Temporalio.Workflows;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// The workflow-side stream object: an append-only, multi-topic log served to external
    /// publishers (via signal), subscribers (via update), and offset queries (via query).
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change.
    /// <para>
    /// Construct it once per workflow, preferably in a <c>[WorkflowInit]</c> constructor: the
    /// constructor registers all three handlers on the current workflow, and a
    /// <c>[WorkflowInit]</c> constructor runs before any handler dispatch, so polls arriving with
    /// the first workflow task are accepted. Constructing it at the start of the workflow method
    /// also works — signals received earlier are buffered by the SDK — but polls and offset
    /// queries are rejected until the stream exists. Constructing it twice throws because the
    /// handler names would be duplicated.
    /// </para>
    /// </remarks>
#pragma warning disable CA1711 // Name matches the other SDKs' workflow streams packages exactly
    public sealed class WorkflowStream
#pragma warning restore CA1711
    {
        private readonly List<Entry> log = new();
        private readonly Dictionary<string, long> publisherSequences = new();
        private readonly Dictionary<string, double> publisherLastSeen = new();
        private readonly Dictionary<string, WorkflowTopicHandle> topicHandles = new();
        private readonly IPayloadConverter payloadConverter;
        private long baseOffset;
        private bool draining;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStream" /> class and registers its
        /// signal, update, and query handlers on the current workflow. Pass
        /// <paramref name="priorState" /> (which may be null) to restore state carried across a
        /// continue-as-new boundary.
        /// </summary>
        /// <param name="priorState">State captured by <see cref="GetState()" /> or
        /// <see cref="ContinueAsNewAsync" /> on a previous run, or null on a fresh start.
        /// </param>
        /// <param name="options">Stream options, or null for defaults.</param>
        /// <exception cref="InvalidOperationException">Not called from within a workflow.
        /// </exception>
        public WorkflowStream(WorkflowStreamState? priorState = null, WorkflowStreamOptions? options = null)
        {
            if (!Workflow.InWorkflow)
            {
                throw new InvalidOperationException("Cannot use workflow stream outside of workflow");
            }
            payloadConverter = options?.PayloadConverter ?? Workflow.PayloadConverter;

            if (priorState != null)
            {
                baseOffset = priorState.BaseOffset;
                if (priorState.Log != null)
                {
                    foreach (var item in priorState.Log)
                    {
                        log.Add(new Entry(item.Topic, PayloadWire.Decode(item.Data ?? string.Empty)));
                    }
                }
                if (priorState.PublisherSequences != null)
                {
                    foreach (var pair in priorState.PublisherSequences)
                    {
                        publisherSequences[pair.Key] = pair.Value;
                    }
                }
                if (priorState.PublisherLastSeen != null)
                {
                    foreach (var pair in priorState.PublisherLastSeen)
                    {
                        publisherLastSeen[pair.Key] = pair.Value;
                    }
                }
            }

            // Both handlers are registered with Abandon rather than the WarnAndAbandon default: a
            // long poll is outstanding essentially all the time, so the default would log
            // TMPRL1102 on every completion of a stream-hosting workflow, pointing users at a
            // handler attribute they cannot reach.
            Workflow.Signals.Add(
                WorkflowStreamConstants.PublishSignalName,
                WorkflowSignalDefinition.CreateWithoutAttribute(
                    WorkflowStreamConstants.PublishSignalName,
                    (Func<PublishInput, Task>)HandlePublishAsync,
                    HandlerUnfinishedPolicy.Abandon));
            Workflow.Updates.Add(
                WorkflowStreamConstants.PollUpdateName,
                WorkflowUpdateDefinition.CreateWithoutAttribute(
                    WorkflowStreamConstants.PollUpdateName,
                    (Func<PollInput, Task<PollResult>>)HandlePollAsync,
                    (Action<PollInput>)ValidatePoll,
                    HandlerUnfinishedPolicy.Abandon));
            Workflow.Queries.Add(
                WorkflowStreamConstants.OffsetQueryName,
                WorkflowQueryDefinition.CreateWithoutAttribute(
                    WorkflowStreamConstants.OffsetQueryName,
                    (Func<long>)HandleOffsetQuery));
        }

        /// <summary>
        /// Returns a handle for publishing to <paramref name="name" />. Repeated calls with the
        /// same name return the same handle.
        /// </summary>
        /// <param name="name">Topic name.</param>
        /// <returns>The topic handle.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="name" /> is null.</exception>
        public WorkflowTopicHandle Topic(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (!topicHandles.TryGetValue(name, out var handle))
            {
                handle = new WorkflowTopicHandle(name, this);
                topicHandles[name] = handle;
            }
            return handle;
        }

        /// <summary>
        /// Discards log entries before <paramref name="upToOffset" /> and advances the base
        /// offset. After truncation, polls requesting an offset before the new base receive a
        /// <c>TruncatedOffset</c> error; this also unblocks (and fails) waiting pollers whose
        /// requested offset fell below the new base.
        /// </summary>
        /// <param name="upToOffset">Offset to truncate up to (exclusive).</param>
        /// <exception cref="ApplicationFailureException">
        /// A non-retryable <c>TruncateOutOfRange</c> failure if <paramref name="upToOffset" /> is
        /// past the end of the log.
        /// </exception>
        public void Truncate(long upToOffset)
        {
            var logIndex = upToOffset - baseOffset;
            if (logIndex <= 0)
            {
                return;
            }
            if (logIndex > log.Count)
            {
                throw new ApplicationFailureException(
                    $"cannot truncate to offset {upToOffset}: only {baseOffset + log.Count} items exist",
                    WorkflowStreamConstants.ErrorTypeTruncateOutOfRange,
                    nonRetryable: true);
            }
            log.RemoveRange(0, (int)logIndex);
            baseOffset = upToOffset;
        }

        /// <summary>
        /// Unblocks all waiting poll handlers and rejects new polls. Used before continue-as-new.
        /// </summary>
        public void DetachPollers() => draining = true;

        /// <summary>
        /// Returns a serializable snapshot of stream state for continue-as-new, using the default
        /// publisher TTL. See <see cref="GetState(TimeSpan)" />.
        /// </summary>
        /// <returns>Stream state snapshot.</returns>
        public WorkflowStreamState GetState() => GetState(WorkflowStreamConstants.DefaultPublisherTtl);

        /// <summary>
        /// Returns a serializable snapshot of stream state for continue-as-new. It drops
        /// per-publisher sequence tracking for publishers that have not sent a batch within
        /// <paramref name="publisherTtl" />.
        /// </summary>
        /// <param name="publisherTtl">How long per-publisher dedup state is retained since the
        /// publisher's last accepted batch.</param>
        /// <returns>Stream state snapshot.</returns>
        public WorkflowStreamState GetState(TimeSpan publisherTtl)
        {
            var now = NowUnixSeconds();
            var state = new WorkflowStreamState { BaseOffset = baseOffset };
            foreach (var pair in publisherSequences)
            {
                publisherLastSeen.TryGetValue(pair.Key, out var lastSeen);
                if (now - lastSeen < publisherTtl.TotalSeconds)
                {
                    state.PublisherSequences[pair.Key] = pair.Value;
                    state.PublisherLastSeen[pair.Key] = lastSeen;
                }
            }
            foreach (var entry in log)
            {
                // Per-item offset is re-derivable from baseOffset + index on reload.
                state.Log.Add(new WireItem
                {
                    Topic = entry.Topic,
                    Data = PayloadWire.Encode(entry.Payload),
                    Offset = 0,
                });
            }
            return state;
        }

        /// <summary>
        /// Drains pollers, waits for in-flight handlers to finish, captures stream state, and
        /// continues-as-new with the arguments built by <paramref name="buildArgs" />, so it can
        /// take a moment before the current run ends. <paramref name="buildArgs" /> receives the
        /// post-detach stream state and returns the positional arguments for the new run; thread
        /// the <see cref="WorkflowStreamState" /> into your workflow input so the stream survives
        /// the rollover.
        /// </summary>
        /// <remarks>
        /// State is captured with the default 15-minute publisher TTL. For a custom TTL, use the
        /// manual recipe: <see cref="DetachPollers" />,
        /// <c>await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished)</c>,
        /// <see cref="GetState(TimeSpan)" />, then
        /// <see cref="Workflow.CreateContinueAsNewException(string, IReadOnlyCollection{object?}, ContinueAsNewOptions?)" />.
        /// </remarks>
        /// <param name="buildArgs">Builds the next run's arguments from the captured state.
        /// </param>
        /// <param name="options">Continue-as-new options, or null for defaults.</param>
        /// <returns>Never returns; always throws the continue-as-new exception.</returns>
        public async Task ContinueAsNewAsync(
            Func<WorkflowStreamState, IReadOnlyCollection<object?>> buildArgs,
            ContinueAsNewOptions? options = null)
        {
            DetachPollers();
            await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished).ConfigureAwait(true);
            throw Workflow.CreateContinueAsNewException(
                Workflow.Info.WorkflowType,
                buildArgs(GetState(WorkflowStreamConstants.DefaultPublisherTtl)),
                options);
        }

        /// <summary>
        /// Appends a workflow-published value directly to the log: no signal, no dedup, no base64
        /// round-trip; immediately visible to pollers.
        /// </summary>
        /// <param name="name">Topic name.</param>
        /// <param name="value">Value to publish.</param>
        internal void PublishToTopic(string name, object? value)
        {
            var payload = value is Payload p ? p : payloadConverter.ToPayload(value);
            var encoded = PayloadWire.Encode(payload);
            if (PayloadWire.IsTooLarge(encoded, name))
            {
                throw new ApplicationFailureException(
                    $"workflowstreams: published item exceeds the " +
                    $"{WorkflowStreamConstants.MaxPollResponseBytes}-byte poll response limit",
                    WorkflowStreamConstants.ErrorTypeItemTooLarge,
                    nonRetryable: true);
            }
            log.Add(new Entry(name, payload));
        }

        private static double NowUnixSeconds() =>
            new DateTimeOffset(Workflow.UtcNow).ToUnixTimeMilliseconds() / 1000.0;

        private Task HandlePublishAsync(PublishInput input)
        {
            if (!string.IsNullOrEmpty(input.PublisherId))
            {
                if (publisherSequences.TryGetValue(input.PublisherId, out var lastSeq) &&
                    input.Sequence <= lastSeq)
                {
                    return Task.CompletedTask;
                }
                publisherSequences[input.PublisherId] = input.Sequence;
                publisherLastSeen[input.PublisherId] = NowUnixSeconds();
            }
            if (input.Items == null)
            {
                return Task.CompletedTask;
            }
            foreach (var entry in input.Items)
            {
                Payload payload;
                try
                {
                    payload = PayloadWire.Decode(entry.Data ?? string.Empty);
                }
                catch (ArgumentException)
                {
                    // A malformed entry would be a protocol violation; skip it rather than
                    // corrupting the log.
                    continue;
                }
                if (PayloadWire.IsTooLarge(entry.Data ?? string.Empty, entry.Topic))
                {
                    // A signal has no response through which to reject one entry. Dropping it
                    // prevents every subscriber reaching this offset from wedging the workflow.
                    continue;
                }
                log.Add(new Entry(entry.Topic, payload));
            }
            return Task.CompletedTask;
        }

        private void ValidatePoll(PollInput input)
        {
            if (draining)
            {
                throw new ApplicationFailureException(
                    "workflow is draining for continue-as-new",
                    WorkflowStreamConstants.ErrorTypeStreamDraining,
                    nonRetryable: true);
            }
        }

        private async Task<PollResult> HandlePollAsync(PollInput input)
        {
            // Wait until items at or after the requested offset are available, the requested
            // offset has been truncated away, or the stream is draining. baseOffset can advance
            // via truncate while waiting, so the condition re-evaluates the requested position
            // against the current baseOffset rather than capturing it once up front.
            await Workflow.WaitConditionAsync(() =>
                draining ||
                (input.FromOffset != 0 && input.FromOffset < baseOffset) ||
                log.Count > Math.Max(input.FromOffset - baseOffset, 0)).ConfigureAwait(true);
            if (input.FromOffset != 0 && input.FromOffset < baseOffset)
            {
                throw new ApplicationFailureException(
                    $"requested offset {input.FromOffset} has been truncated; current base offset is {baseOffset}",
                    WorkflowStreamConstants.ErrorTypeTruncatedOffset,
                    nonRetryable: true);
            }

            var logOffset = Math.Max(input.FromOffset - baseOffset, 0);
            var topicSet = input.Topics == null || input.Topics.Count == 0
                ? null
                : new HashSet<string>(input.Topics);

            var items = new List<WireItem>();
            var size = 0;
            var moreReady = false;
            var nextOffset = baseOffset + log.Count;

            for (long i = logOffset; i < log.Count; i++)
            {
                var entry = log[(int)i];
                if (topicSet != null && !topicSet.Contains(entry.Topic))
                {
                    continue;
                }
                var globalOffset = baseOffset + i;
                var encoded = PayloadWire.Encode(entry.Payload);
                var itemSize = PayloadWire.WireSize(encoded, entry.Topic);
                if (size + itemSize > WorkflowStreamConstants.MaxPollResponseBytes && items.Count > 0)
                {
                    // Resume from this item on the next poll.
                    nextOffset = globalOffset;
                    moreReady = true;
                    break;
                }
                size += itemSize;
                items.Add(new WireItem { Topic = entry.Topic, Data = encoded, Offset = globalOffset });
            }

            return new PollResult { Items = items, NextOffset = nextOffset, MoreReady = moreReady };
        }

        private long HandleOffsetQuery() => baseOffset + log.Count;

        /// <summary>
        /// A single decoded log entry held in workflow memory.
        /// </summary>
        private sealed class Entry
        {
            public Entry(string topic, Payload payload)
            {
                Topic = topic;
                Payload = payload;
            }

            public string Topic { get; }

            public Payload Payload { get; }
        }
    }
}
