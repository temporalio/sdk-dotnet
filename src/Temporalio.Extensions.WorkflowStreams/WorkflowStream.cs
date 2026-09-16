using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Temporalio.Api.Common.V1;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Temporalio.Extensions.WorkflowStreams
{
#pragma warning disable CA1711 // The cross-SDK protocol's established public name describes a stream.
#pragma warning disable CA2007 // Workflow continuations must stay on the deterministic scheduler.
    /// <summary>
    /// A durable, offset-addressed, multi-topic log hosted inside a Temporal workflow.
    /// </summary>
    /// <remarks>
    /// Construct one instance during workflow initialization. WARNING: Workflow Streams is
    /// experimental and may change.
    /// </remarks>
    public sealed class WorkflowStream
    {
        private readonly List<LogEntry> log = new();
        private readonly SortedDictionary<string, long> publisherSequences = new();
        private readonly SortedDictionary<string, double> publisherLastSeen = new();
        private readonly WorkflowStreamOptions options;
        private long baseOffset;
        private bool draining;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStream"/> class.
        /// </summary>
        public WorkflowStream()
            : this(null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStream"/> class.
        /// </summary>
        /// <param name="state">
        /// State captured before continue-as-new, or null for a new stream.
        /// </param>
        public WorkflowStream(WorkflowStreamState? state)
            : this(state, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStream"/> class.
        /// </summary>
        /// <param name="state">
        /// State captured before continue-as-new, or null for a new stream.
        /// </param>
        /// <param name="options">
        /// Stream options, snapshotted by this constructor.
        /// </param>
        public WorkflowStream(WorkflowStreamState? state, WorkflowStreamOptions? options)
        {
            this.options = (WorkflowStreamOptions)(options ?? new WorkflowStreamOptions()).Clone();
            if (this.options.PublisherTtl <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), "PublisherTtl must be greater than zero");
            }
            Restore(state);
            Workflow.Signals[WorkflowStreamConstants.PublishSignalName] =
                WorkflowSignalDefinition.CreateWithoutAttribute(
                    WorkflowStreamConstants.PublishSignalName,
                    (Func<PublishInput, Task>)HandlePublishAsync,
                    HandlerUnfinishedPolicy.Abandon);
            Workflow.Updates[WorkflowStreamConstants.PollUpdateName] =
                WorkflowUpdateDefinition.CreateWithoutAttribute(
                    WorkflowStreamConstants.PollUpdateName,
                    (Func<PollInput, Task<PollResult>>)HandlePollAsync,
                    (Action<PollInput>)ValidatePoll,
                    HandlerUnfinishedPolicy.Abandon);
            Workflow.Queries[WorkflowStreamConstants.OffsetQueryName] =
                WorkflowQueryDefinition.CreateWithoutAttribute(
                    WorkflowStreamConstants.OffsetQueryName,
                    (Func<long>)(() => baseOffset + log.Count));
        }

        /// <summary>
        /// Creates a workflow-side publisher for a topic.
        /// </summary>
        /// <param name="name">
        /// Topic name. Null is represented by the empty topic.
        /// </param>
        /// <returns>
        /// A topic handle.
        /// </returns>
        public WorkflowStreamTopicHandle GetTopic(string? name)
        {
            name ??= string.Empty;
            return new(this, name);
        }

        /// <summary>
        /// Creates a strongly typed workflow-side publisher for a topic.
        /// </summary>
        /// <typeparam name="T">
        /// Type of values published to the topic.
        /// </typeparam>
        /// <param name="name">
        /// Topic name. Null is represented by the empty topic.
        /// </param>
        /// <returns>
        /// A topic handle.
        /// </returns>
        public WorkflowStreamTopicHandle<T> GetTopic<T>(string? name)
        {
            name ??= string.Empty;
            return new(this, name);
        }

        /// <summary>
        /// Unblocks admitted pollers and rejects new polls during continue-as-new.
        /// </summary>
        public void DetachPollers() => draining = true;

        /// <summary>
        /// Captures retained state using the configured publisher time-to-live.
        /// </summary>
        /// <returns>
        /// A cross-language state snapshot.
        /// </returns>
        public WorkflowStreamState GetState() => GetState(options.PublisherTtl);

        /// <summary>
        /// Captures retained state with a specific publisher time-to-live.
        /// </summary>
        /// <param name="publisherTtl">
        /// Age after which publisher deduplication state is omitted.
        /// </param>
        /// <returns>
        /// A cross-language state snapshot.
        /// </returns>
        public WorkflowStreamState GetState(TimeSpan publisherTtl)
        {
            if (publisherTtl <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(publisherTtl), "Publisher TTL must be greater than zero");
            }

            var now = new DateTimeOffset(
                DateTime.SpecifyKind(Workflow.UtcNow, DateTimeKind.Utc)).ToUnixTimeMilliseconds() /
                1000d;
            var sequences = new Dictionary<string, long>();
            var lastSeen = new Dictionary<string, double>();
            foreach (var pair in publisherSequences)
            {
                if (publisherLastSeen.TryGetValue(pair.Key, out var seen) &&
                    now - seen < publisherTtl.TotalSeconds)
                {
                    sequences.Add(pair.Key, pair.Value);
                    lastSeen.Add(pair.Key, seen);
                }
            }

            return new()
            {
                BaseOffset = baseOffset,
                Log = log.Select(entry => new WorkflowStreamWireItem
                {
                    Topic = entry.Topic,
                    Data = PayloadWire.Encode(entry.Payload),
                    Offset = 0,
                }).ToArray(),
                PublisherSequences = sequences,
                PublisherLastSeen = lastSeen,
            };
        }

        /// <summary>
        /// Detaches pollers, waits for handlers, captures state, and continues as new.
        /// </summary>
        /// <param name="createException">
        /// Callback that creates the SDK continue-as-new exception from the captured state.
        /// </param>
        /// <returns>
        /// A task that does not complete normally.
        /// </returns>
        public async Task ContinueAsNewAsync(
            Func<WorkflowStreamState, ContinueAsNewException> createException)
        {
            DetachPollers();
            await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished);
            throw createException(GetState());
        }

        /// <summary>
        /// Discards entries before a global offset.
        /// </summary>
        /// <param name="upToOffset">
        /// The first offset to retain.
        /// </param>
        public void Truncate(long upToOffset)
        {
            var removeCount = upToOffset - baseOffset;
            if (removeCount <= 0)
            {
                return;
            }
            if (removeCount > log.Count)
            {
                throw new ApplicationFailureException(
                    $"Cannot truncate to offset {upToOffset}; the stream ends at " +
                    $"{baseOffset + log.Count}",
                    WorkflowStreamConstants.TruncateOutOfRangeErrorType,
                    nonRetryable: true);
            }
            log.RemoveRange(0, (int)removeCount);
            baseOffset = upToOffset;
        }

        /// <summary>
        /// Keeps workflow-side publications on the same converter and log path.
        /// </summary>
        /// <param name="topic">
        /// Normalized topic name.
        /// </param>
        /// <param name="value">
        /// Value or raw payload to append.
        /// </param>
        internal void Publish(string topic, object? value)
        {
            var payload = value as Payload ?? Workflow.PayloadConverter.ToPayload(value);
            log.Add(new(topic, payload));
        }

        private void Restore(WorkflowStreamState? state)
        {
            if (state == null)
            {
                return;
            }
            if (state.BaseOffset < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(state), "The base offset cannot be negative");
            }
            baseOffset = state.BaseOffset;
            foreach (var item in state.Log ?? Array.Empty<WorkflowStreamWireItem>())
            {
                var topic = item?.Topic ?? string.Empty;
                log.Add(new(topic, PayloadWire.Decode(item?.Data ?? string.Empty)));
            }
            foreach (var pair in state.PublisherSequences ?? new Dictionary<string, long>())
            {
                publisherSequences[pair.Key] = pair.Value;
            }
            foreach (var pair in state.PublisherLastSeen ?? new Dictionary<string, double>())
            {
                publisherLastSeen[pair.Key] = pair.Value;
            }
        }

        private void ValidatePoll(PollInput input)
        {
            if (draining)
            {
                throw new ApplicationFailureException(
                    "Workflow Stream is draining for continue-as-new",
                    WorkflowStreamConstants.StreamDrainingErrorType,
                    nonRetryable: true);
            }
        }

        private Task HandlePublishAsync(PublishInput input)
        {
            if (input == null)
            {
                Workflow.Logger.LogWarning("Ignoring a malformed Workflow Stream publish signal");
                return Task.CompletedTask;
            }
            if (input.PublisherId.Length > 0)
            {
                if (publisherSequences.TryGetValue(input.PublisherId, out var sequence) &&
                    input.Sequence <= sequence)
                {
                    return Task.CompletedTask;
                }
                publisherSequences[input.PublisherId] = input.Sequence;
                publisherLastSeen[input.PublisherId] = new DateTimeOffset(
                    DateTime.SpecifyKind(
                        Workflow.UtcNow,
                        DateTimeKind.Utc)).ToUnixTimeMilliseconds() / 1000d;
            }

            foreach (var item in input.Items ?? Array.Empty<PublishEntry>())
            {
                var topic = item?.Topic ?? string.Empty;
                var data = item?.Data ?? string.Empty;
                try
                {
                    log.Add(new(topic, PayloadWire.Decode(data)));
                }
                catch (Exception err) when (
                    err is FormatException || err is InvalidProtocolBufferException)
                {
                    Workflow.Logger.LogWarning(
                        err,
                        "Ignoring a malformed Workflow Stream signal item on topic {Topic}",
                        topic);
                }
            }
            return Task.CompletedTask;
        }

        private async Task<PollResult> HandlePollAsync(PollInput input)
        {
            input ??= new PollInput();
            await Workflow.WaitConditionAsync(() =>
                draining ||
                (input.FromOffset != 0 && input.FromOffset < baseOffset) ||
                log.Count > Math.Max(input.FromOffset - baseOffset, 0));

            if (input.FromOffset != 0 && input.FromOffset < baseOffset)
            {
                throw new ApplicationFailureException(
                    $"Requested offset {input.FromOffset} has been truncated; current base " +
                    $"offset is {baseOffset}",
                    WorkflowStreamConstants.TruncatedOffsetErrorType,
                    nonRetryable: true);
            }

            var requestedIndex = Math.Max(input.FromOffset - baseOffset, 0);
            var startIndex = requestedIndex > log.Count ? log.Count : (int)requestedIndex;
            HashSet<string>? topicFilter = null;
            if (input.Topics.Count > 0)
            {
                topicFilter = new(input.Topics.Select(topic => topic ?? string.Empty));
            }

            var items = new List<WorkflowStreamWireItem>();
            var size = 0;
            var moreReady = false;
            var nextOffset = baseOffset + log.Count;
            for (var index = startIndex; index < log.Count; index++)
            {
                var entry = log[index];
                if (topicFilter != null && !topicFilter.Contains(entry.Topic))
                {
                    continue;
                }
                var encoded = PayloadWire.Encode(entry.Payload);
                var itemSize = PayloadWire.EstimateSize(encoded, entry.Topic);
                var offset = baseOffset + index;
                if (size + itemSize > WorkflowStreamConstants.MaxPollResponseBytes &&
                    items.Count > 0)
                {
                    nextOffset = offset;
                    moreReady = true;
                    break;
                }
                size += itemSize;
                items.Add(new()
                {
                    Topic = entry.Topic,
                    Data = encoded,
                    Offset = offset,
                });
            }

            return new()
            {
                Items = items,
                NextOffset = nextOffset,
                MoreReady = moreReady,
            };
        }

        private sealed class LogEntry
        {
            internal LogEntry(string topic, Payload payload)
            {
                Topic = topic;
                Payload = payload;
            }

            internal string Topic { get; }

            internal Payload Payload { get; }
        }
    }
}
