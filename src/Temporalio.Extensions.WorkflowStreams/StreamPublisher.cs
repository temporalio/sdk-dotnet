using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>Owns serialized client publication state and its single timer.</summary>
    internal sealed class StreamPublisher : IAsyncDisposable
    {
        private readonly object stateLock = new();
        private readonly Func<PublishInput, CancellationToken, Task> signalAsync;
        private readonly IPayloadConverter payloadConverter;
        private readonly TimeSpan batchInterval;
        private readonly TimeSpan maxRetryDuration;
        private readonly int maxBatchSize;
        private readonly string publisherId = Guid.NewGuid().ToString("N").Substring(0, 16);
        private readonly SemaphoreSlim flushGate = new(1, 1);
        private readonly SemaphoreSlim wakeSignal = new(0, 1);
        private readonly CancellationTokenSource stopSource = new();
        private List<PublishEntry> buffer = new();
        private IReadOnlyCollection<PublishEntry>? pending;
        private long sequence;
        private long pendingSequence;
        private long pendingStartedAt;
        private Timer? timer;
        private Task? backgroundTask;
        private Task? disposeTask;
        private bool disposing;
        private FlushTimeoutException? deferredError;

        /// <summary>Initializes a new instance of the <see cref="StreamPublisher"/> class.</summary>
        /// <param name="signalAsync">Injected workflow signal operation.</param>
        /// <param name="payloadConverter">Converter applied synchronously during publication.</param>
        /// <param name="options">Snapshotted client options.</param>
        internal StreamPublisher(
            Func<PublishInput, CancellationToken, Task> signalAsync,
            IPayloadConverter payloadConverter,
            WorkflowStreamClientOptions options)
        {
            this.signalAsync = signalAsync;
            this.payloadConverter = payloadConverter;
            batchInterval = options.BatchInterval;
            maxBatchSize = options.MaxBatchSize;
            maxRetryDuration = options.MaxRetryDuration;
        }

        /// <summary>Gets a value indicating whether this publisher owns a live timer.</summary>
        internal bool HasLiveTimer
        {
            get
            {
                lock (stateLock)
                {
                    return timer != null;
                }
            }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => new(DisposeTaskAsync());

        /// <summary>Converts before enqueueing so one bad value cannot poison later batches.</summary>
        /// <param name="topic">Normalized topic name.</param>
        /// <param name="value">Value or raw payload to publish.</param>
        /// <param name="forceFlush">Whether to wake the flusher immediately.</param>
        internal void Publish(string topic, object? value, bool forceFlush)
        {
            var wake = false;
            lock (stateLock)
            {
                if (disposing)
                {
                    throw new ObjectDisposedException(nameof(WorkflowStreamClient));
                }
                var payload = value as Payload ?? payloadConverter.ToPayload(value);
                var encoded = PayloadWire.Encode(payload);
                if (PayloadWire.EstimateSize(encoded, topic) >
                    WorkflowStreamConstants.MaxPollResponseBytes)
                {
                    throw new ArgumentException(
                        "The Workflow Stream item is too large to fit in a poll response",
                        nameof(value));
                }
                buffer.Add(new() { Topic = topic, Data = encoded });
                EnsureStartedLocked();
                wake = forceFlush || (maxBatchSize > 0 && buffer.Count >= maxBatchSize);
            }
            if (wake)
            {
                Wake();
            }
        }

        /// <summary>Implements a sequence-based barrier over all items present at entry.</summary>
        /// <param name="cancellationToken">Cancellation token for signal attempts.</param>
        /// <returns>A task that completes after the barrier is acknowledged.</returns>
        internal async Task FlushAsync(CancellationToken cancellationToken)
        {
            ThrowDeferredError();
            long targetSequence;
            lock (stateLock)
            {
                if (pending == null && buffer.Count == 0)
                {
                    return;
                }
                var baseSequence = pending == null ? sequence : pendingSequence;
                targetSequence = buffer.Count == 0 ? baseSequence : baseSequence + 1;
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (stateLock)
                {
                    if (sequence >= targetSequence)
                    {
                        break;
                    }
                }
                await FlushOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            ThrowDeferredError();
        }

        /// <summary>Returns the task shared by all terminal operations.</summary>
        /// <returns>The shared terminal task.</returns>
        internal Task DisposeTaskAsync()
        {
            lock (stateLock)
            {
                if (disposeTask != null)
                {
                    return disposeTask;
                }
                disposing = true;
                timer?.Dispose();
                timer = null;
                stopSource.Cancel();
                Wake();
                disposeTask = DisposeCoreAsync();
                return disposeTask;
            }
        }

        private void EnsureStartedLocked()
        {
            if (backgroundTask != null)
            {
                return;
            }
            timer = new Timer(
                state => ((StreamPublisher)state!).Wake(),
                this,
                batchInterval,
                batchInterval);
            backgroundTask = Task.Run(RunBackgroundAsync);
        }

        private void Wake()
        {
            try
            {
                wakeSignal.Release();
            }
            catch (SemaphoreFullException)
            {
                // One pending wake is enough even when timer and publication callbacks race.
            }
            catch (ObjectDisposedException)
            {
                // A racing timer callback has no work once terminal disposal has completed.
            }
        }

        private async Task RunBackgroundAsync()
        {
            while (true)
            {
                try
                {
                    await wakeSignal.WaitAsync(stopSource.Token).ConfigureAwait(false);
                    await FlushOnceAsync(stopSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stopSource.IsCancellationRequested)
                {
                    return;
                }
                catch (FlushTimeoutException err)
                {
                    lock (stateLock)
                    {
                        deferredError ??= err;
                    }
                }
                catch (TemporalException)
                {
                    // The pending batch stays intact and the single timer retries it next interval.
                }
                catch (InvalidOperationException)
                {
                    // The pending batch stays intact and the single timer retries it next interval.
                }
            }
        }

        private async Task FlushOnceAsync(CancellationToken cancellationToken)
        {
            await flushGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                IReadOnlyCollection<PublishEntry> batch;
                long batchSequence;
                lock (stateLock)
                {
                    if (pending != null)
                    {
                        var elapsed = TimeSpan.FromSeconds(
                            (Stopwatch.GetTimestamp() - pendingStartedAt) /
                            (double)Stopwatch.Frequency);
                        if (elapsed > maxRetryDuration)
                        {
                            sequence = pendingSequence;
                            pending = null;
                            pendingSequence = 0;
                            pendingStartedAt = 0;
                            throw new FlushTimeoutException(
                                $"Workflow Stream flush retry exceeded {maxRetryDuration}; " +
                                "the ambiguous batch was dropped");
                        }
                        batch = pending;
                        batchSequence = pendingSequence;
                    }
                    else if (buffer.Count > 0)
                    {
                        batch = buffer;
                        buffer = new();
                        batchSequence = sequence + 1;
                        pending = batch;
                        pendingSequence = batchSequence;
                        pendingStartedAt = Stopwatch.GetTimestamp();
                    }
                    else
                    {
                        return;
                    }
                }

                await signalAsync(
                    new()
                    {
                        Items = batch,
                        PublisherId = publisherId,
                        Sequence = batchSequence,
                    },
                    cancellationToken).ConfigureAwait(false);

                lock (stateLock)
                {
                    sequence = batchSequence;
                    pending = null;
                    pendingSequence = 0;
                    pendingStartedAt = 0;
                }
            }
            finally
            {
                flushGate.Release();
            }
        }

        private async Task DisposeCoreAsync()
        {
            await Task.Yield();
            Exception? firstError = null;
            try
            {
                var task = backgroundTask;
                if (task != null)
                {
                    try
                    {
                        await task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stopSource.IsCancellationRequested)
                    {
                    }
                }

                lock (stateLock)
                {
                    firstError = deferredError;
                    deferredError = null;
                }
                while (HasWork())
                {
                    try
                    {
                        await FlushOnceAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (FlushTimeoutException err)
                    {
                        firstError ??= err;
                    }
                    catch (TemporalException err)
                    {
                        firstError ??= err;
                        break;
                    }
                    catch (InvalidOperationException err)
                    {
                        firstError ??= err;
                        break;
                    }
                }
            }
            finally
            {
                stopSource.Dispose();
                flushGate.Dispose();
                wakeSignal.Dispose();
            }

            if (firstError != null)
            {
                throw firstError;
            }
        }

        private bool HasWork()
        {
            lock (stateLock)
            {
                return pending != null || buffer.Count > 0;
            }
        }

        private void ThrowDeferredError()
        {
            FlushTimeoutException? error;
            lock (stateLock)
            {
                error = deferredError;
                deferredError = null;
            }
            if (error != null)
            {
                throw error;
            }
        }
    }
}
