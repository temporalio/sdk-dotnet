#pragma warning disable CA1001 // Sync primitives are reclaimed with the owning client; neither is used via WaitHandle, so disposal would add nothing

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Extensions.WorkflowStreams.Internal
{
    /// <summary>
    /// Owns the client-side publish path: it buffers published values, batches them, and sends
    /// each batch to the workflow via the injected signal function. It assigns the per-publisher
    /// dedup key (a stable publisher ID plus a monotonic sequence advanced only on a confirmed
    /// send) so the workflow can drop duplicates, and it retries a failed batch until the max
    /// retry duration elapses.
    /// </summary>
    /// <remarks>
    /// The signal function is injected (rather than holding a client) so the publish path can be
    /// exercised in isolation. Internal to the workflow streams module.
    /// </remarks>
    internal sealed class StreamPublisher
    {
        private readonly Func<PublishInput, Task> signalFunc;
        private readonly IPayloadConverter payloadConverter;
        private readonly string publisherId;
        private readonly TimeSpan batchInterval;
        private readonly int maxBatchSize;
        private readonly TimeSpan maxRetryDuration;
        private readonly Func<long> getTimestamp;
        private readonly long timestampFrequency;
        private readonly object stateLock = new();
        private readonly CancellationTokenSource backgroundCts = new();

        // Guards against concurrent DoFlushAsync callers; sending must stay sequential.
        private readonly SemaphoreSlim flushLock = new(1, 1);

        private List<PublishEntry> buffer = new();
        private List<PublishEntry>? pending;
        private long pendingSeq;
        private long sequence;
        private long pendingStartTimestamp;
        private bool started;
        private bool closed;
        private FlushTimeoutException? deferredError;
        private TaskCompletionSource<object?> wakeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task? backgroundTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamPublisher"/> class.
        /// </summary>
        /// <param name="signalFunc">Sends a publish signal to the target workflow. Throws on
        /// delivery failure.</param>
        /// <param name="payloadConverter">Converts published values to per-item payloads.
        /// </param>
        /// <param name="batchInterval">Interval between automatic flushes.</param>
        /// <param name="maxBatchSize">Buffer size that triggers a flush; 0 disables.</param>
        /// <param name="maxRetryDuration">Max time to retry a failed flush before surfacing a
        /// <see cref="FlushTimeoutException" />.</param>
        /// <param name="getTimestamp">Monotonic timestamp source, or null for
        /// <see cref="Stopwatch.GetTimestamp" />.</param>
        /// <param name="timestampFrequency">Timestamp units per second, or null for
        /// <see cref="Stopwatch.Frequency" />.</param>
        public StreamPublisher(
            Func<PublishInput, Task> signalFunc,
            IPayloadConverter payloadConverter,
            TimeSpan batchInterval,
            int maxBatchSize,
            TimeSpan maxRetryDuration,
            Func<long>? getTimestamp = null,
            long? timestampFrequency = null)
        {
            this.signalFunc = signalFunc;
            this.payloadConverter = payloadConverter;
            publisherId = Guid.NewGuid().ToString("N").Substring(0, 16);
            this.batchInterval = batchInterval;
            this.maxBatchSize = maxBatchSize;
            this.maxRetryDuration = maxRetryDuration;
            this.getTimestamp = getTimestamp ?? Stopwatch.GetTimestamp;
            this.timestampFrequency = timestampFrequency ?? Stopwatch.Frequency;
        }

        /// <summary>
        /// Converts and buffers a value, lazily starting the background flush loop. Triggers an
        /// immediate flush on <paramref name="forceFlush" /> or once the buffer reaches the max
        /// batch size.
        /// </summary>
        /// <remarks>
        /// Conversion happens here, on the caller's thread, so an unconvertible value fails the
        /// publish call itself instead of poisoning the buffer and silently wedging every later
        /// item behind it in the background flush loop. Publishing after close throws.
        /// </remarks>
        /// <param name="topic">Topic to publish on.</param>
        /// <param name="value">Value to publish.</param>
        /// <param name="forceFlush">Wake the background loop to send immediately.</param>
        public void Publish(string topic, object? value, bool forceFlush)
        {
            lock (stateLock)
            {
                if (closed)
                {
                    throw new ObjectDisposedException(nameof(StreamPublisher));
                }
            }
            var payload = value is Payload p ? p : payloadConverter.ToPayload(value);
            var entry = new PublishEntry { Topic = topic, Data = PayloadWire.Encode(payload) };
            lock (stateLock)
            {
                if (closed)
                {
                    throw new ObjectDisposedException(nameof(StreamPublisher));
                }
                buffer.Add(entry);
                EnsureStartedLocked();
                if (forceFlush || (maxBatchSize > 0 && buffer.Count >= maxBatchSize))
                {
                    wakeTcs.TrySetResult(null);
                }
            }
        }

        /// <summary>
        /// Sends the pending batch (retry) or the buffered batch (new batch). Serialized via the
        /// flush lock so concurrent callers send sequentially.
        /// </summary>
        /// <returns>Task completing when the send attempt finished.</returns>
        public async Task DoFlushAsync()
        {
            await flushLock.WaitAsync().ConfigureAwait(false);
            try
            {
                List<PublishEntry> batch;
                long seq;
                lock (stateLock)
                {
                    if (pending != null)
                    {
                        var elapsedSeconds =
                            (getTimestamp() - pendingStartTimestamp) /
                            (double)timestampFrequency;
                        if (elapsedSeconds > maxRetryDuration.TotalSeconds)
                        {
                            // Advance the confirmed sequence so the next batch gets a fresh
                            // sequence number. Without this the next batch reuses pendingSeq,
                            // which the workflow may have already accepted — causing silent
                            // dedup (data loss).
                            sequence = pendingSeq;
                            pending = null;
                            pendingSeq = 0;
                            pendingStartTimestamp = 0;
                            throw new FlushTimeoutException(
                                $"workflowstreams: flush retry exceeded the max retry duration " +
                                $"({(long)maxRetryDuration.TotalMilliseconds}ms); pending batch dropped");
                        }
                        batch = pending;
                        seq = pendingSeq;
                    }
                    else if (buffer.Count > 0)
                    {
                        batch = buffer;
                        buffer = new();
                        seq = sequence + 1;
                        pending = batch;
                        pendingSeq = seq;
                        pendingStartTimestamp = getTimestamp();
                    }
                    else
                    {
                        return;
                    }
                }

                // On failure the signal throws and pending stays set for retry.
                await signalFunc(new PublishInput
                {
                    Items = batch,
                    PublisherId = publisherId,
                    Sequence = seq,
                }).ConfigureAwait(false);

                lock (stateLock)
                {
                    sequence = seq;
                    pending = null;
                    pendingSeq = 0;
                    pendingStartTimestamp = 0;
                }
            }
            finally
            {
                flushLock.Release();
            }
        }

        /// <summary>
        /// Sends buffered (and pending) items and waits for confirmation. Returns once the items
        /// buffered at call time have been signaled and acknowledged. Transient send failures
        /// propagate; the batch stays pending for the next flush or background tick.
        /// </summary>
        /// <returns>Task completing when the flush is confirmed.</returns>
        /// <exception cref="FlushTimeoutException">
        /// A pending batch could not be sent within the max retry duration.
        /// </exception>
        public async Task FlushAsync()
        {
            ThrowDeferred();

            long targetSeq;
            lock (stateLock)
            {
                if (pending == null && buffer.Count == 0)
                {
                    return;
                }
                var baseSeq = pending != null ? pendingSeq : sequence;
                targetSeq = buffer.Count == 0 ? baseSeq : baseSeq + 1;
            }

            while (true)
            {
                await DoFlushAsync().ConfigureAwait(false);
                lock (stateLock)
                {
                    if (sequence >= targetSeq)
                    {
                        break;
                    }
                }
            }
            ThrowDeferred();
        }

        /// <summary>
        /// Stops the background flush loop and drains any remaining items, surfacing a deferred
        /// <see cref="FlushTimeoutException" /> from a prior background failure. Idempotent.
        /// </summary>
        /// <returns>Task completing when the drain finished.</returns>
        public async Task CloseAsync()
        {
            Task? toAwait;
            lock (stateLock)
            {
                if (closed)
                {
                    return;
                }
                closed = true;
                toAwait = backgroundTask;
            }
            backgroundCts.Cancel();
            if (toAwait != null)
            {
                // The loop never throws; it exits once any in-flight flush finishes.
                await toAwait.ConfigureAwait(false);
            }

            // Final drain: a single DoFlushAsync processes either pending OR the buffer.
            while (true)
            {
                lock (stateLock)
                {
                    if (pending == null && buffer.Count == 0)
                    {
                        break;
                    }
                }
                await DoFlushAsync().ConfigureAwait(false);
            }
            ThrowDeferred();
        }

        private void EnsureStartedLocked()
        {
            if (started || closed)
            {
                return;
            }
            started = true;
            backgroundTask = BackgroundLoopAsync();
        }

        private async Task BackgroundLoopAsync()
        {
            while (true)
            {
                Task wakeTask;
                lock (stateLock)
                {
                    wakeTask = wakeTcs.Task;
                }
                var delayTask = Task.Delay(batchInterval, backgroundCts.Token);
                var completed = await Task.WhenAny(delayTask, wakeTask).ConfigureAwait(false);
                if (completed == delayTask && delayTask.IsCanceled)
                {
                    return;
                }
                lock (stateLock)
                {
                    if (wakeTask.IsCompleted)
                    {
                        wakeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    }
                }
                try
                {
                    await DoFlushAsync().ConfigureAwait(false);
                }
                catch (FlushTimeoutException e)
                {
                    // The pending batch was dropped and can't be recovered. Stash the error so
                    // FlushAsync/CloseAsync surface it and stop the loop.
                    lock (stateLock)
                    {
                        deferredError = e;
                    }
                    return;
                }
#pragma warning disable CA1031 // Transient send failures of any kind are retried on the next tick
                catch (Exception)
#pragma warning restore CA1031
                {
                    // Transient failure: pending stays set for retry on the next tick.
                }
            }
        }

        private void ThrowDeferred()
        {
            lock (stateLock)
            {
                if (deferredError != null)
                {
                    var e = deferredError;
                    deferredError = null;
                    throw e;
                }
            }
        }
    }
}
