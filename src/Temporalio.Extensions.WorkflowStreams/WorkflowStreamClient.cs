using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Extensions.WorkflowStreams.Internal;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Publishes to and subscribes from a workflow stream from external code (activities,
    /// starters, other processes). The publish path is owned by an internal publisher that
    /// batches buffered items and signals them to the workflow; the client itself holds the
    /// target workflow and the read (subscribe/query) surface.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change.
    /// <para>
    /// The client targets the latest run of the workflow-ID chain, so subscriptions follow
    /// continue-as-new chains automatically. Close the client (via <see cref="CloseAsync" />,
    /// preferred, or <see cref="Dispose" />) to guarantee a final flush of buffered items.
    /// </para>
    /// </remarks>
#if NETCOREAPP3_0_OR_GREATER
    public sealed class WorkflowStreamClient : IDisposable, IAsyncDisposable
#else
    public sealed class WorkflowStreamClient : IDisposable
#endif
    {
        private readonly ITemporalClient client;
        private readonly string workflowId;
        private readonly StreamPublisher publisher;
        private readonly Dictionary<string, TopicHandle> topicHandles = new();
        private readonly object topicHandlesLock = new();
        private readonly HashSet<SubscriptionDriver> liveSubscriptions = new();
        private readonly object liveSubscriptionsLock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStreamClient"/> class targeting
        /// <paramref name="workflowId" /> through the given Temporal client. The client follows
        /// continue-as-new chains in its subscriptions.
        /// </summary>
        /// <param name="client">Temporal client to communicate through.</param>
        /// <param name="workflowId">Workflow ID hosting the stream.</param>
        /// <param name="options">Client options, or null for defaults.</param>
        public WorkflowStreamClient(
            ITemporalClient client, string workflowId, WorkflowStreamClientOptions? options = null)
        {
            this.client = client;
            this.workflowId = workflowId;
            var opts = options ?? new WorkflowStreamClientOptions();
            var payloadConverter = opts.PayloadConverter ?? client.Options.DataConverter.PayloadConverter;
            publisher = new StreamPublisher(
                input => client.GetWorkflowHandle(workflowId).SignalAsync(
                    WorkflowStreamConstants.PublishSignalName, new object?[] { input }),
                payloadConverter,
                opts.BatchInterval,
                opts.MaxBatchSize,
                opts.MaxRetryDuration);
        }

        /// <summary>
        /// Creates a client targeting the current activity's parent workflow, using the
        /// activity's Temporal client. Must be called from within an activity.
        /// </summary>
        /// <param name="options">Client options, or null for defaults.</param>
        /// <returns>The stream client.</returns>
        /// <exception cref="InvalidOperationException">
        /// Not called from an activity, or the activity has no parent workflow; in the latter
        /// case use the constructor with an explicit workflow ID.
        /// </exception>
        public static WorkflowStreamClient FromActivity(WorkflowStreamClientOptions? options = null)
        {
            var context = ActivityExecutionContext.Current;
            var workflowId = context.Info.WorkflowId;
            if (string.IsNullOrEmpty(workflowId))
            {
                throw new InvalidOperationException(
                    "workflowstreams: FromActivity requires an activity scheduled by a workflow; " +
                    "otherwise use the constructor with an explicit workflow ID");
            }
            return new WorkflowStreamClient(context.TemporalClient, workflowId!, options);
        }

        /// <summary>
        /// Returns a handle for publishing to and subscribing from <paramref name="name" />.
        /// Repeated calls with the same name return the same handle.
        /// </summary>
        /// <param name="name">Topic name.</param>
        /// <returns>The topic handle.</returns>
        public TopicHandle Topic(string name)
        {
            lock (topicHandlesLock)
            {
                if (!topicHandles.TryGetValue(name, out var handle))
                {
                    handle = new TopicHandle(name, this);
                    topicHandles[name] = handle;
                }
                return handle;
            }
        }

        /// <summary>
        /// Sends buffered (and pending) items and waits for server confirmation. Returns once the
        /// items buffered at call time have been signaled to the workflow and acknowledged.
        /// </summary>
        /// <returns>Task completing when the flush is confirmed.</returns>
        /// <exception cref="FlushTimeoutException">
        /// A pending batch could not be sent within the max retry duration.
        /// </exception>
        public Task FlushAsync() => publisher.FlushAsync();

        /// <summary>
        /// Queries the current global offset of the stream.
        /// </summary>
        /// <returns>The current global offset.</returns>
        public async Task<long> GetOffsetAsync() =>
            await client.GetWorkflowHandle(workflowId).QueryAsync<long>(
                WorkflowStreamConstants.OffsetQueryName, Array.Empty<object?>()).ConfigureAwait(false);

        /// <summary>
        /// Returns a subscription that long-polls for new items. Iterate with:
        /// <code>
        /// await using var subscription = streamClient.Subscribe(options);
        /// await foreach (var item in subscription)
        /// {
        ///     // use item
        /// }
        /// </code>
        /// (or drive <see cref="WorkflowStreamSubscription.MoveNextAsync" /> manually).
        /// </summary>
        /// <remarks>
        /// Polling is fully async: no thread is held while a poll is blocked on the server, so
        /// many concurrent subscriptions do not mean many threads. Each item carries the raw
        /// <see cref="Temporalio.Api.Common.V1.Payload" />; decode it with your payload
        /// converter. The subscription ends cleanly when the workflow reaches a terminal state,
        /// automatically follows continue-as-new chains, and also ends when this client is
        /// closed. Polling starts lazily on the first
        /// <see cref="WorkflowStreamSubscription.MoveNextAsync" />.
        /// </remarks>
        /// <param name="options">Subscribe options, or null for defaults.</param>
        /// <returns>The subscription.</returns>
#pragma warning disable VSTHRD200 // Name matches the other SDKs' workflow streams packages; subscribing is not itself async work
        public WorkflowStreamSubscription Subscribe(SubscribeOptions? options = null)
#pragma warning restore VSTHRD200
        {
            var opts = options ?? new SubscribeOptions();
            return new WorkflowStreamSubscription(listener => NewSubscriptionDriver(opts, listener));
        }

        /// <summary>
        /// Subscribes <paramref name="listener" /> to the stream without occupying a caller
        /// thread: polling is fully async and never holds a thread while a poll is blocked on
        /// the server, so many subscriptions share the thread pool. Delivery starts immediately.
        /// </summary>
        /// <remarks>
        /// The stream ends cleanly with <see cref="WorkflowStreamListener.OnCompleted" /> when
        /// the workflow reaches a terminal state, automatically follows continue-as-new chains,
        /// and reports unrecoverable failures to <see cref="WorkflowStreamListener.OnError" />.
        /// Stop it early with <see cref="WorkflowStreamSubscriptionHandle.Dispose" />; closing
        /// this client also stops it.
        /// </remarks>
        /// <param name="options">Subscribe options.</param>
        /// <param name="listener">Listener receiving the items.</param>
        /// <returns>A handle controlling the subscription.</returns>
        public WorkflowStreamSubscriptionHandle Subscribe(
            SubscribeOptions options, WorkflowStreamListener listener)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }
            var driver = NewSubscriptionDriver(options, listener);
            driver.Start();
            return new WorkflowStreamSubscriptionHandle(driver);
        }

        /// <summary>
        /// Stops the background publisher and drains any remaining items, guaranteeing a final
        /// flush. It surfaces any deferred <see cref="FlushTimeoutException" /> from a prior
        /// background flush failure. Also stops this client's live subscriptions (their
        /// <see cref="WorkflowStreamSubscriptionHandle.Completion" /> tasks complete normally,
        /// without <see cref="WorkflowStreamListener.OnCompleted" />). Idempotent.
        /// </summary>
        /// <returns>Task completing when the client has closed.</returns>
        public async Task CloseAsync()
        {
            await publisher.CloseAsync().ConfigureAwait(false);
            List<SubscriptionDriver> drivers;
            lock (liveSubscriptionsLock)
            {
                drivers = new List<SubscriptionDriver>(liveSubscriptions);
            }
            foreach (var driver in drivers)
            {
                driver.Close();
            }
        }

        /// <summary>
        /// Closes the client synchronously; see <see cref="CloseAsync" />, which is preferred
        /// over this blocking form.
        /// </summary>
        public void Dispose()
        {
            // Safe to block: every await inside CloseAsync uses ConfigureAwait(false), so no
            // synchronization context can be required to finish it.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            CloseAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Closes the client; see <see cref="CloseAsync" />.
        /// </summary>
        /// <returns>A value task completing when the client has closed.</returns>
        public async ValueTask DisposeAsync()
        {
            await CloseAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
#endif

        /// <summary>
        /// Publishes a value on a topic via the internal publisher.
        /// </summary>
        /// <param name="topic">Topic to publish on.</param>
        /// <param name="value">Value to publish.</param>
        /// <param name="forceFlush">Wake the publisher and send immediately.</param>
        internal void PublishToTopic(string topic, object? value, bool forceFlush) =>
            publisher.Publish(topic, value, forceFlush);

        private SubscriptionDriver NewSubscriptionDriver(
            SubscribeOptions options, WorkflowStreamListener listener)
        {
            var driver = new SubscriptionDriver(
                client,
                workflowId,
                options,
                listener,
                d =>
                {
                    lock (liveSubscriptionsLock)
                    {
                        liveSubscriptions.Remove(d);
                    }
                });
            lock (liveSubscriptionsLock)
            {
                liveSubscriptions.Add(driver);
            }
            return driver;
        }
    }
}
