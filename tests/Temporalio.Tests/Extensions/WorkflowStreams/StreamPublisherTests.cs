namespace Temporalio.Tests.Extensions.WorkflowStreams;

using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;
using Xunit;

public class StreamPublisherTests
{
    private static readonly WorkflowStreamClientOptions SlowBatchOptions = new()
    {
        BatchInterval = TimeSpan.FromHours(1),
        MaxRetryDuration = TimeSpan.FromMinutes(1),
    };

    [Fact]
    public async Task FlushAsync_BatchesAndAdvancesSequence()
    {
        var sent = new List<PublishInput>();
        await using var publisher = new StreamPublisher(
            (input, _) =>
            {
                sent.Add(input);
                return Task.CompletedTask;
            },
            DataConverter.Default.PayloadConverter,
            SlowBatchOptions);

        publisher.Publish("one", "first", false);
        publisher.Publish("two", "second", false);
        await publisher.FlushAsync(default);
        publisher.Publish("one", "third", false);
        await publisher.FlushAsync(default);

        Assert.Equal(2, sent.Count);
        Assert.Equal(new long[] { 1, 2 }, sent.Select(input => input.Sequence));
        Assert.Equal(sent[0].PublisherId, sent[1].PublisherId);
        Assert.Equal(new[] { "one", "two" }, sent[0].Items.Select(item => item.Topic));
        var payload = PayloadWire.Decode(sent[0].Items.First().Data);
        Assert.Equal("first", DataConverter.Default.PayloadConverter.ToValue(payload, typeof(string)));
    }

    [Fact]
    public async Task FlushAsync_RetriesAmbiguousBatchWithSameIdentity()
    {
        var attempts = new List<PublishInput>();
        await using var publisher = new StreamPublisher(
            (input, _) =>
            {
                attempts.Add(input);
                return attempts.Count == 1 ?
                    Task.FromException(new InvalidOperationException("ambiguous")) :
                    Task.CompletedTask;
            },
            DataConverter.Default.PayloadConverter,
            SlowBatchOptions);
        publisher.Publish(string.Empty, "value", false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.FlushAsync(default));
        await publisher.FlushAsync(default);

        Assert.Equal(2, attempts.Count);
        Assert.Equal(attempts[0].PublisherId, attempts[1].PublisherId);
        Assert.Equal(attempts[0].Sequence, attempts[1].Sequence);
        Assert.Same(attempts[0].Items, attempts[1].Items);
    }

    [Fact]
    public async Task FlushTimeout_DropsAmbiguousBatchWithoutReusingSequence()
    {
        var attempts = new List<PublishInput>();
        var options = (WorkflowStreamClientOptions)SlowBatchOptions.Clone();
        options.MaxRetryDuration = TimeSpan.FromTicks(1);
        await using var publisher = new StreamPublisher(
            (input, _) =>
            {
                attempts.Add(input);
                return input.Sequence == 1 ?
                    Task.FromException(new InvalidOperationException("ambiguous")) :
                    Task.CompletedTask;
            },
            DataConverter.Default.PayloadConverter,
            options);

        publisher.Publish(string.Empty, "lost-or-delivered", false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.FlushAsync(default));
        await Assert.ThrowsAsync<FlushTimeoutException>(() => publisher.FlushAsync(default));

        publisher.Publish(string.Empty, "later", false);
        await publisher.FlushAsync(default);

        Assert.Equal(new long[] { 1, 2 }, attempts.Select(input => input.Sequence));
    }

    [Fact]
    public async Task BackgroundFlusher_ContinuesAfterTimeout()
    {
        var attempts = new List<long>();
        var firstAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        StreamPublisher? disposedPublisher = null;
        await Assert.ThrowsAsync<FlushTimeoutException>(async () =>
        {
            await using var publisher = new StreamPublisher(
                (input, _) =>
                {
                    attempts.Add(input.Sequence);
                    if (input.Sequence == 1)
                    {
                        firstAttempt.TrySetResult();
                        return Task.FromException(new InvalidOperationException("ambiguous"));
                    }
                    laterDelivered.TrySetResult();
                    return Task.CompletedTask;
                },
                DataConverter.Default.PayloadConverter,
                new()
                {
                    BatchInterval = TimeSpan.FromMilliseconds(10),
                    MaxRetryDuration = TimeSpan.FromTicks(1),
                });
            disposedPublisher = publisher;

            publisher.Publish(string.Empty, "ambiguous", true);
            await firstAttempt.Task.WaitAsync(TimeSpan.FromSeconds(10));
            publisher.Publish(string.Empty, "later", true);
            await laterDelivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(new long[] { 1, 2 }, attempts);
        });
        Assert.False(disposedPublisher!.HasLiveTimer);
    }

    [Fact]
    public async Task DisposeAsync_IsSharedDrainsAndRejectsLaterPublication()
    {
        var signalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = new StreamPublisher(
            async (_, _) =>
            {
                signalStarted.TrySetResult();
                await releaseSignal.Task;
            },
            DataConverter.Default.PayloadConverter,
            SlowBatchOptions);
        publisher.Publish(string.Empty, "value", false);

        var firstDispose = publisher.DisposeAsync().AsTask();
        var secondDispose = publisher.DisposeAsync().AsTask();
        Assert.Same(firstDispose, secondDispose);
        await signalStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(publisher.HasLiveTimer);
        Assert.Throws<ObjectDisposedException>(() =>
            publisher.Publish(string.Empty, "later", false));

        releaseSignal.SetResult();
        await firstDispose;
        await secondDispose;
    }

    [Fact]
    public async Task FlushAsync_ConsumerCancellationLeavesBatchRetryable()
    {
        var attempt = 0;
        var signalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var publisher = new StreamPublisher(
            async (_, cancellationToken) =>
            {
                attempt++;
                if (attempt == 1)
                {
                    signalStarted.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
            },
            DataConverter.Default.PayloadConverter,
            SlowBatchOptions);
        publisher.Publish(string.Empty, "value", false);
        using var cancellationSource = new CancellationTokenSource();

        var flushTask = publisher.FlushAsync(cancellationSource.Token);
        await signalStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cancellationSource.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => flushTask);
        await publisher.FlushAsync(default);

        Assert.Equal(2, attempt);
    }
}
