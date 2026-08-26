namespace Temporalio.Tests.Extensions.WorkflowStreams;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Extensions.WorkflowStreams.Internal;
using Xunit;

public class StreamPublisherTests
{
    private static readonly IPayloadConverter Converter = DataConverter.Default.PayloadConverter;

    [Fact]
    public async Task Flush_SendsBufferedItems()
    {
        var signal = new RecordingSignal();
        var publisher = NewPublisher(signal);

        publisher.Publish("events", "a", forceFlush: false);
        publisher.Publish("events", "b", forceFlush: false);
        await publisher.FlushAsync();

        var signals = signal.Recorded();
        var input = Assert.Single(signals);
        Assert.Equal(2, input.Items.Count);
        Assert.Equal(1, input.Sequence);
        Assert.Equal(16, input.PublisherId.Length);
        Assert.Equal("a", DecodeItem(input, 0));
        Assert.Equal("b", DecodeItem(input, 1));

        await publisher.CloseAsync();
    }

    [Fact]
    public async Task Flush_NoopWhenEmpty()
    {
        var signal = new RecordingSignal();
        var publisher = NewPublisher(signal);

        await publisher.FlushAsync();

        Assert.Empty(signal.Recorded());
        await publisher.CloseAsync();
    }

    [Fact]
    public async Task Sequence_AdvancesAcrossFlushes()
    {
        var signal = new RecordingSignal();
        var publisher = NewPublisher(signal);

        publisher.Publish("t", "x", forceFlush: false);
        await publisher.FlushAsync();
        publisher.Publish("t", "y", forceFlush: false);
        await publisher.FlushAsync();

        var signals = signal.Recorded();
        Assert.Equal(2, signals.Count);
        Assert.Equal(1, signals[0].Sequence);
        Assert.Equal(2, signals[1].Sequence);
        Assert.Equal(signals[0].PublisherId, signals[1].PublisherId);

        await publisher.CloseAsync();
    }

    [Fact]
    public async Task MaxBatchSize_TriggersFlush()
    {
        var signal = new RecordingSignal();
        // Long interval so only the size threshold can trigger a flush.
        var publisher = NewPublisher(signal, TimeSpan.FromHours(1), maxBatchSize: 2);

        publisher.Publish("t", "a", forceFlush: false);
        publisher.Publish("t", "b", forceFlush: false);

        await signal.SentTask.WaitAsync(TimeSpan.FromSeconds(10));
        var input = Assert.Single(signal.Recorded());
        Assert.Equal(2, input.Items.Count);

        await publisher.CloseAsync();
    }

    [Fact]
    public async Task Close_DrainsBuffer()
    {
        var signal = new RecordingSignal();
        var publisher = NewPublisher(signal, TimeSpan.FromHours(1));

        publisher.Publish("t", "a", forceFlush: false);
        await publisher.CloseAsync();

        var input = Assert.Single(signal.Recorded());
        Assert.Single(input.Items);
    }

    [Fact]
    public async Task Publish_AfterCloseThrows()
    {
        var publisher = NewPublisher(new RecordingSignal());

        await publisher.CloseAsync();

        Assert.Throws<ObjectDisposedException>(() => publisher.Publish("t", "a", forceFlush: false));
    }

    [Fact]
    public async Task ForceFlush_SendsImmediately()
    {
        var signal = new RecordingSignal();
        var publisher = NewPublisher(signal, TimeSpan.FromHours(1));

        publisher.Publish("t", "a", forceFlush: true);

        await signal.SentTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(signal.Recorded());

        await publisher.CloseAsync();
    }

    [Fact]
    public async Task ForceFlush_CancelsSupersededDelay()
    {
        var signal = new RecordingSignal();
        var delay = new ManualDelay();
        var publisher = NewPublisher(signal, delayAsync: delay.DelayAsync);

        publisher.Publish("t", "a", forceFlush: true);

        await signal.SentTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, delay.CanceledCount);
        await publisher.CloseAsync();
    }

    [Fact]
    public async Task FlushTimeout_AfterMaxRetryDuration()
    {
        var signal = new RecordingSignal { Failure = new InvalidOperationException("boom") };
        long timestamp = 0;
        var publisher = NewPublisher(
            signal,
            TimeSpan.FromHours(1),
            maxRetry: TimeSpan.FromSeconds(1),
            getTimestamp: () => timestamp,
            timestampFrequency: 1);

        publisher.Publish("t", "a", forceFlush: false);

        // The first flush sets pending and fails to send (transient "boom").
        var boom = await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.FlushAsync());
        Assert.Equal("boom", boom.Message);

        timestamp = 2;

        await Assert.ThrowsAsync<FlushTimeoutException>(() => publisher.FlushAsync());

        // The failed batch may have reached the workflow before its acknowledgement failed, so
        // a later batch must use a fresh sequence rather than being silently deduplicated.
        signal.Failure = null;
        publisher.Publish("t", "b", forceFlush: false);
        await publisher.FlushAsync();
        Assert.Equal(2, Assert.Single(signal.Recorded()).Sequence);

        await publisher.CloseAsync();
    }

    [Fact]
    public async Task Publish_RejectsItemLargerThanPollResponse()
    {
        var signal = new RecordingSignal();
        var publisher = NewPublisher(signal);

        Assert.Throws<ArgumentException>(() => publisher.Publish(
            "events", new string('x', WorkflowStreamConstants.MaxPollResponseBytes), forceFlush: false));

        publisher.Publish("events", "ok", forceFlush: false);
        await publisher.FlushAsync();
        Assert.Single(signal.Recorded());
        await publisher.CloseAsync();
    }

    [Fact]
    public async Task PayloadConverters_DriveItemConversion()
    {
        // With only the byte-array converter, a byte[] round-trips but a string has no
        // converter and fails — whereas the default set's JSON fallback would have accepted it.
        var byteOnly = new DefaultPayloadConverter(new BinaryPlainConverter());
        var signal = new RecordingSignal();
        var publisher = NewPublisher(signal, converter: byteOnly);

        publisher.Publish("events", Encoding.UTF8.GetBytes("hi"), forceFlush: false);
        await publisher.FlushAsync();
        var input = Assert.Single(signal.Recorded());
        var payload = PayloadWire.Decode(input.Items[0].Data!);
        Assert.Equal("binary/plain", payload.Metadata["encoding"].ToStringUtf8());
        await publisher.CloseAsync();

        // A string is unconvertible under the byte-array-only set, so the publish call itself
        // fails — conversion happens at publish time so a bad value cannot poison the buffer.
        var signal2 = new RecordingSignal();
        var publisher2 = NewPublisher(signal2, converter: byteOnly);
        Assert.Throws<ArgumentException>(() => publisher2.Publish("events", "not-bytes", forceFlush: false));

        // The rejected value must not wedge the publisher: a valid item published afterwards
        // still ships.
        publisher2.Publish("events", Encoding.UTF8.GetBytes("ok"), forceFlush: false);
        await publisher2.FlushAsync();
        Assert.Single(signal2.Recorded());
        await publisher2.CloseAsync();
    }

    [Fact]
    public async Task FlushTimeout_InBackgroundLoop_DoesNotStopLaterPublishes()
    {
        var signal = new RecordingSignal { Failure = new InvalidOperationException("boom") };
        var delay = new ManualDelay();
        long timestamp = 0;
        var publisher = NewPublisher(
            signal,
            TimeSpan.FromHours(1),
            maxRetry: TimeSpan.FromSeconds(1),
            getTimestamp: () => timestamp,
            timestampFrequency: 1,
            delayAsync: delay.DelayAsync);

        // The background loop picks this up, fails to send (transient "boom"), and keeps the
        // batch pending across retries.
        publisher.Publish("t", "a", forceFlush: false);
        await delay.AdvanceAsync();
        await signal.AttemptedTask.WaitAsync(TimeSpan.FromSeconds(10));

        // Push past the max retry duration so the next background tick drops the batch with a
        // FlushTimeoutException, which stops the loop.
        timestamp = 2;
        await delay.AdvanceAsync();
        await publisher.BackgroundTask!.WaitAsync(TimeSpan.FromSeconds(10));

        // A later publish must still reach the workflow on its own: the loop restarts rather
        // than leaving the publisher silently buffering until someone calls FlushAsync.
        signal.Failure = null;
        publisher.Publish("t", "b", forceFlush: true);
        await signal.SentTask.WaitAsync(TimeSpan.FromSeconds(10));

        var input = Assert.Single(signal.Recorded());
        Assert.Equal("b", DecodeItem(input, 0));
        Assert.Equal(2, input.Sequence);

        // The deferred timeout is still surfaced once, on close.
        await Assert.ThrowsAsync<FlushTimeoutException>(() => publisher.CloseAsync());
    }

    private static StreamPublisher NewPublisher(
        RecordingSignal signal,
        TimeSpan? batchInterval = null,
        int maxBatchSize = 0,
        TimeSpan? maxRetry = null,
        IPayloadConverter? converter = null,
        Func<long>? getTimestamp = null,
        long? timestampFrequency = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null) =>
        new(
            signal.Send,
            converter ?? Converter,
            batchInterval ?? TimeSpan.FromSeconds(2),
            maxBatchSize,
            maxRetry ?? WorkflowStreamConstants.DefaultMaxRetryDuration,
            getTimestamp,
            timestampFrequency,
            delayAsync);

    private static string DecodeItem(PublishInput input, int index) =>
        Converter.ToValue<string>(PayloadWire.Decode(input.Items[index].Data!));

    // Records sent batches; when Failure is set, sending throws it instead.
    private sealed class RecordingSignal
    {
        private readonly List<PublishInput> signals = new();
        private readonly TaskCompletionSource sent = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource attempted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Exception? Failure { get; set; }

        public Task SentTask => sent.Task;

        public Task AttemptedTask => attempted.Task;

        public Task Send(PublishInput input)
        {
            attempted.TrySetResult();
            lock (signals)
            {
                if (Failure != null)
                {
                    throw Failure;
                }
                signals.Add(input);
            }
            sent.TrySetResult();
            return Task.CompletedTask;
        }

        public List<PublishInput> Recorded()
        {
            lock (signals)
            {
                return new List<PublishInput>(signals);
            }
        }
    }

    private sealed class ManualDelay
    {
        private readonly Channel<TaskCompletionSource> delays =
            Channel.CreateUnbounded<TaskCompletionSource>();

        private int canceledCount;

        public int CanceledCount => Volatile.Read(ref canceledCount);

        public Task DelayAsync(TimeSpan _, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            delays.Writer.TryWrite(completion);
            return WaitAsync(completion, cancellationToken);
        }

        public async Task AdvanceAsync()
        {
            var completion = await delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            completion.TrySetResult();
        }

        private async Task WaitAsync(
            TaskCompletionSource completion, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() =>
                {
                    Interlocked.Increment(ref canceledCount);
                    completion.TrySetCanceled();
                }))
            {
                await completion.Task;
            }
        }
    }
}
