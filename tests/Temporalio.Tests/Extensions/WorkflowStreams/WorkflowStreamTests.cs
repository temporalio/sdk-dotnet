namespace Temporalio.Tests.Extensions.WorkflowStreams;

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Extensions.WorkflowStreams.Internal;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

// Integration tests for the workflow-side stream, driven with raw protocol calls like the
// Java module's WorkflowStreamTest.
public class WorkflowStreamTests : WorkflowEnvironmentTestBase
{
    public WorkflowStreamTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task ExternalPublish_AndOffsetQuery()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);

            await handle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName,
                new object?[]
                {
                    WorkflowStreamTestUtils.PublishInputFor("pub1", 1, "events", "a", "events", "b"),
                });
            // Poll first so the offset query observes the published items.
            await handle.ExecuteUpdateAsync<PollResult>(
                WorkflowStreamConstants.PollUpdateName,
                new object?[] { new PollInput { FromOffset = 0 } });

            var offset = await handle.QueryAsync<long>(
                WorkflowStreamConstants.OffsetQueryName, Array.Empty<object?>());
            Assert.Equal(2, offset);

            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Publish_PublisherDedup()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);

            await handle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName,
                new object?[] { WorkflowStreamTestUtils.PublishInputFor("pub1", 1, "events", "a") });
            // Same publisher + sequence: must be dropped.
            await handle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName,
                new object?[] { WorkflowStreamTestUtils.PublishInputFor("pub1", 1, "events", "dup") });
            await handle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName,
                new object?[] { WorkflowStreamTestUtils.PublishInputFor("pub1", 2, "events", "c") });

            var result = await handle.ExecuteUpdateAsync<PollResult>(
                WorkflowStreamConstants.PollUpdateName,
                new object?[] { new PollInput { FromOffset = 0 } });
            Assert.Equal(2, result.Items.Count);
            Assert.Equal(2, result.NextOffset);
            Assert.Equal("a", WorkflowStreamTestUtils.Decode(result.Items[0]));
            Assert.Equal(0, result.Items[0].Offset);
            Assert.Equal("c", WorkflowStreamTestUtils.Decode(result.Items[1]));
            Assert.Equal(1, result.Items[1].Offset);

            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Poll_ReturnsItemsWithTopicFilter()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);

            await handle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName,
                new object?[]
                {
                    WorkflowStreamTestUtils.PublishInputFor("pub1", 1, "a", "1", "b", "2", "a", "3"),
                });

            var result = await handle.ExecuteUpdateAsync<PollResult>(
                WorkflowStreamConstants.PollUpdateName,
                new object?[] { new PollInput { Topics = { "a" }, FromOffset = 0 } });

            // Only topic "a" items, with global offsets 0 and 2.
            Assert.Equal(2, result.Items.Count);
            Assert.Equal("a", result.Items[0].Topic);
            Assert.Equal(0, result.Items[0].Offset);
            Assert.Equal("a", result.Items[1].Topic);
            Assert.Equal(2, result.Items[1].Offset);
            Assert.Equal(3, result.NextOffset);
            Assert.False(result.MoreReady);
            Assert.Equal("3", WorkflowStreamTestUtils.Decode(result.Items[1]));

            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Truncate_DropsEntriesAndFailsOldPolls()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);

            await handle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName,
                new object?[]
                {
                    WorkflowStreamTestUtils.PublishInputFor(
                        "pub1", 1, "events", "a", "events", "b", "events", "c"),
                });
            // Ensure the batch has been applied before truncating.
            await handle.ExecuteUpdateAsync<PollResult>(
                WorkflowStreamConstants.PollUpdateName,
                new object?[] { new PollInput { FromOffset = 0 } });

            await handle.ExecuteUpdateAsync("Truncate", new object?[] { 2L });

            // Offset 0 means "from the beginning of whatever still exists".
            var fromStart = await handle.ExecuteUpdateAsync<PollResult>(
                WorkflowStreamConstants.PollUpdateName,
                new object?[] { new PollInput { FromOffset = 0 } });
            Assert.Single(fromStart.Items);
            Assert.Equal(2, fromStart.Items[0].Offset);
            Assert.Equal("c", WorkflowStreamTestUtils.Decode(fromStart.Items[0]));

            // A poll positioned before the new base offset fails with TruncatedOffset.
            var truncated = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() =>
                handle.ExecuteUpdateAsync<PollResult>(
                    WorkflowStreamConstants.PollUpdateName,
                    new object?[] { new PollInput { FromOffset = 1 } }));
            var appFailure = Assert.IsType<ApplicationFailureException>(truncated.InnerException);
            Assert.Equal(WorkflowStreamConstants.ErrorTypeTruncatedOffset, appFailure.ErrorType);

            // Truncating past the end of the log fails with TruncateOutOfRange.
            var outOfRange = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() =>
                handle.ExecuteUpdateAsync("Truncate", new object?[] { 10L }));
            appFailure = Assert.IsType<ApplicationFailureException>(outOfRange.InnerException);
            Assert.Equal(WorkflowStreamConstants.ErrorTypeTruncateOutOfRange, appFailure.ErrorType);

            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Truncate_FailsPollThatWasAlreadyWaiting()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            await handle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName,
                new object?[] { WorkflowStreamTestUtils.PublishInputFor("pub1", 1, "events", "a") });

            var poll = await handle.StartUpdateAsync<PollResult>(
                WorkflowStreamConstants.PollUpdateName,
                new object?[] { new PollInput { FromOffset = 1 } },
                new WorkflowUpdateStartOptions(WorkflowUpdateStage.Accepted));
            await handle.ExecuteUpdateAsync(
                "PublishLocalAndTruncate", new object?[] { "events", "b", 2L });

            var truncated = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                () => poll.GetResultAsync());
            var appFailure = Assert.IsType<ApplicationFailureException>(truncated.InnerException);
            Assert.Equal(WorkflowStreamConstants.ErrorTypeTruncatedOffset, appFailure.ErrorType);

            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Poll_PagesResponsesAtSizeLimit()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            var payload = DataConverter.Default.PayloadConverter.ToPayload(
                Enumerable.Repeat((byte)'x', 200_000).ToArray());
            for (var i = 0; i < 8; i++)
            {
                await handle.SignalAsync(
                    WorkflowStreamConstants.PublishSignalName,
                    new object?[]
                    {
                        new PublishInput
                        {
                            Items =
                            {
                                new PublishEntry
                                {
                                    Topic = "big",
                                    Data = PayloadWire.Encode(payload),
                                },
                            },
                        },
                    });
            }

            var offset = 0L;
            var gathered = 0;
            var sawMoreReady = false;
            while (gathered < 8)
            {
                var result = await handle.ExecuteUpdateAsync<PollResult>(
                    WorkflowStreamConstants.PollUpdateName,
                    new object?[] { new PollInput { FromOffset = offset } });
                gathered += result.Items.Count;
                offset = result.NextOffset;
                sawMoreReady |= result.MoreReady;
                if (gathered == 8)
                {
                    Assert.False(result.MoreReady);
                }
            }
            Assert.True(sawMoreReady);
            Assert.Equal(8, gathered);

            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task StreamConstructedInWorkflowInit_WorksEndToEnd()
    {
        await ExecuteWorkerAsync<InitHostWorkflow>(async worker =>
        {
            var handle = await Client.StartWorkflowAsync(
                (InitHostWorkflow wf) => wf.RunAsync(null),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await WorkflowStreamTestUtils.WaitStreamReadyAsync(handle);

            await handle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName,
                new object?[] { WorkflowStreamTestUtils.PublishInputFor("pub1", 1, "events", "a") });
            var result = await handle.ExecuteUpdateAsync<PollResult>(
                WorkflowStreamConstants.PollUpdateName,
                new object?[] { new PollInput { FromOffset = 0 } });
            Assert.Single(result.Items);
            Assert.Equal("a", WorkflowStreamTestUtils.Decode(result.Items[0]));
            var offset = await handle.QueryAsync<long>(
                WorkflowStreamConstants.OffsetQueryName, Array.Empty<object?>());
            Assert.Equal(1, offset);

            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task WorkflowPublish_UsesConfiguredConverters() =>
        await ExecuteWorkerAsync<ByteOnlyPublishWorkflow>(async worker =>
        {
            // A string is unconvertible under the byte-array-only set, proving the configured
            // converter drives conversion; a byte[] must still publish cleanly.
            var result = await Client.ExecuteWorkflowAsync(
                (ByteOnlyPublishWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.True(result);
        });

    private async Task<WorkflowHandle<StreamHostWorkflow>> StartHostWorkflowAsync(TemporalWorker worker)
    {
        var handle = await Client.StartWorkflowAsync(
            (StreamHostWorkflow wf) => wf.RunAsync(null),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
        await WorkflowStreamTestUtils.WaitStreamReadyAsync(handle);
        return handle;
    }

    private async Task ExecuteWorkerAsync<TWorkflow>(
        Func<TemporalWorker, Task> action, TemporalWorkerOptions? options = null)
    {
        options = (TemporalWorkerOptions?)options?.Clone() ?? new();
        options.TaskQueue ??= $"tq-{Guid.NewGuid()}";
        options.AddWorkflow<TWorkflow>();
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(Client, options);
        await worker.ExecuteAsync(() => action(worker));
    }

    // Hosts the stream from a [WorkflowInit] constructor, the recommended pattern.
    [Workflow]
    public class InitHostWorkflow
    {
        private readonly WorkflowStream stream;
        private bool finished;

        [WorkflowInit]
        public InitHostWorkflow(WorkflowStreamState? priorState) => stream = new(priorState);

        [WorkflowRun]
        public async Task RunAsync(WorkflowStreamState? priorState) =>
            await Workflow.WaitConditionAsync(() => finished);

        [WorkflowSignal]
        public Task FinishAsync()
        {
            finished = true;
            return Task.CompletedTask;
        }
    }

    // Restricts the stream to the byte-array converter and returns whether publishing a string
    // failed — it should, since that set has no converter for strings, whereas the default
    // set's JSON fallback would accept it. A byte[] must still publish cleanly.
    [Workflow]
    public class ByteOnlyPublishWorkflow
    {
        [WorkflowRun]
        public Task<bool> RunAsync()
        {
            var stream = new WorkflowStream(
                null,
                new WorkflowStreamOptions
                {
                    PayloadConverter = new DefaultPayloadConverter(new BinaryPlainConverter()),
                });
            stream.Topic("events").Publish(Encoding.UTF8.GetBytes("hi"));
            try
            {
                stream.Topic("events").Publish("not-bytes");
                return Task.FromResult(false);
            }
            catch (ArgumentException)
            {
                return Task.FromResult(true);
            }
        }
    }
}
