#pragma warning disable SA1201, SA1204 // We want to have classes near their tests

namespace Temporalio.Tests.Worker;

using System.Runtime.CompilerServices;
using Temporalio.Activities;
using Temporalio.Exceptions;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class WorkflowReplayerTests : WorkflowEnvironmentTestBase
{
    public WorkflowReplayerTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    public static class SayHelloActivities
    {
        [Activity]
        public static string SayHello(string name) => $"Hello, {name}!";
    }

    public record SayHelloParams(
        string Name,
        bool ShouldWait = false,
        bool ShouldError = false,
        bool ShouldCauseNonDeterminism = false);

    [Workflow]
    public class SayHelloWorkflow
    {
        public static readonly SayHelloWorkflow Ref = WorkflowRefs.Create<SayHelloWorkflow>();

        private bool waiting;
        private bool finish;

        [WorkflowRun]
        public async Task<string> RunAsync(SayHelloParams parms)
        {
            var result = await Workflow.ExecuteActivityAsync(
                SayHelloActivities.SayHello,
                parms.Name,
                new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(1) });

            // Wait if requested
            if (parms.ShouldWait)
            {
                waiting = true;
                await Workflow.WaitConditionAsync(() => finish);
                waiting = false;
            }

            // Throw if requested
            if (parms.ShouldError)
            {
                throw new ApplicationFailureException("Intentional error");
            }

            // Cause non-determinism if requested
            if (parms.ShouldCauseNonDeterminism && Workflow.Unsafe.IsReplaying)
            {
                await Workflow.DelayAsync(1);
            }

            return result;
        }

        [WorkflowSignal]
        public async Task FinishAsync() => finish = true;

        [WorkflowQuery]
        public bool Waiting() => waiting;
    }

    [Fact]
    public async Task ReplayWorkflowAsync_SimpleRun_Succeeds()
    {
        using var worker = new TemporalWorker(
            Env.Client, new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddActivity(SayHelloActivities.SayHello).AddWorkflow<SayHelloWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Run workflow to completion
            var handle = await Env.Client.StartWorkflowAsync(
                SayHelloWorkflow.Ref.RunAsync,
                new SayHelloParams("Temporal"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", await handle.GetResultAsync());

            // Collect history and replay it
            var result = await new WorkflowReplayer(
                new WorkflowReplayerOptions().AddWorkflow<SayHelloWorkflow>()).
                    ReplayWorkflowAsync(await handle.FetchHistoryAsync());
            Assert.Null(result.ReplayFailure);
        });
    }

    [Fact]
    public async Task ReplayWorkflowAsync_SimpleRunFromJson_Succeeds()
    {
        // Replay history from JSON
        var json = ReadAllFileText("test_replayer_complete_history.json");
        var result = await new WorkflowReplayer(
            new WorkflowReplayerOptions().AddWorkflow<SayHelloWorkflow>()).
                ReplayWorkflowAsync(WorkflowHistory.FromJson("some-id", json));
        Assert.Null(result.ReplayFailure);
    }

    [Fact]
    public async Task ReplayWorkflowAsync_IncompleteRun_Succeeds()
    {
        using var worker = new TemporalWorker(
            Env.Client, new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddActivity(SayHelloActivities.SayHello).AddWorkflow<SayHelloWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Start workflow
            var handle = await Env.Client.StartWorkflowAsync(
                SayHelloWorkflow.Ref.RunAsync,
                new SayHelloParams("Temporal", ShouldWait: true),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Wait until waiting
            await AssertMore.EventuallyAsync(
                async () => Assert.True(await handle.QueryAsync(SayHelloWorkflow.Ref.Waiting)));

            // Collect history and replay it
            var result = await new WorkflowReplayer(
                new WorkflowReplayerOptions().AddWorkflow<SayHelloWorkflow>()).
                    ReplayWorkflowAsync(await handle.FetchHistoryAsync());
            Assert.Null(result.ReplayFailure);
        });
    }

    [Fact]
    public async Task ReplayWorkflowAsync_FailedRun_Succeeds()
    {
        using var worker = new TemporalWorker(
            Env.Client, new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddActivity(SayHelloActivities.SayHello).AddWorkflow<SayHelloWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Run workflow to completion
            var handle = await Env.Client.StartWorkflowAsync(
                SayHelloWorkflow.Ref.RunAsync,
                new SayHelloParams("Temporal", ShouldError: true),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await Assert.ThrowsAsync<WorkflowFailedException>(() => handle.GetResultAsync());

            // Collect history and replay it
            var result = await new WorkflowReplayer(
                new WorkflowReplayerOptions().AddWorkflow<SayHelloWorkflow>()).
                    ReplayWorkflowAsync(await handle.FetchHistoryAsync());
            Assert.Null(result.ReplayFailure);
        });
    }

    [Fact]
    public async Task ReplayWorkflowAsync_NonDeterministicRun_Fails()
    {
        using var worker = new TemporalWorker(
            Env.Client, new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddActivity(SayHelloActivities.SayHello).AddWorkflow<SayHelloWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Run workflow to completion
            var handle = await Env.Client.StartWorkflowAsync(
                SayHelloWorkflow.Ref.RunAsync,
                new SayHelloParams("Temporal", ShouldCauseNonDeterminism: true),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", await handle.GetResultAsync());

            // Collect history and replay it
            var history = await handle.FetchHistoryAsync();
            var replayer = new WorkflowReplayer(
                new WorkflowReplayerOptions().AddWorkflow<SayHelloWorkflow>());
            var exc = await Assert.ThrowsAsync<InvalidWorkflowOperationException>(
                async () => await replayer.ReplayWorkflowAsync(history));
            Assert.Contains("Nondeterminism", exc.Message);

            // Also confirm we can ask it not to throw
            var result = await replayer.ReplayWorkflowAsync(history, throwOnReplayFailure: false);
            exc = Assert.IsType<InvalidWorkflowOperationException>(result.ReplayFailure);
            Assert.Contains("Nondeterminism", exc.Message);
        });
    }

    [Fact]
    public async Task ReplayWorkflowAsync_NonDeterministicRunFromJson_Fails()
    {
        // Replay history from JSON
        var history = WorkflowHistory.FromJson(
            "some-id", ReadAllFileText("test_replayer_nondeterministic_history.json"));
        var replayer = new WorkflowReplayer(
            new WorkflowReplayerOptions().AddWorkflow<SayHelloWorkflow>());
        var exc = await Assert.ThrowsAsync<InvalidWorkflowOperationException>(
            () => replayer.ReplayWorkflowAsync(history));
        Assert.Contains("Nondeterminism", exc.Message);

        // Also confirm we can ask it not to throw
        var result = await replayer.ReplayWorkflowAsync(history, throwOnReplayFailure: false);
        exc = Assert.IsType<InvalidWorkflowOperationException>(result.ReplayFailure);
        Assert.Contains("Nondeterminism", exc.Message);
    }

    [Fact]
    public async Task ReplayWorkflowAsync_MultipleHistories_WorksProperly()
    {
        static IEnumerable<WorkflowHistory> HistoryIter()
        {
            yield return WorkflowHistory.FromJson(
                "some-id1", ReadAllFileText("test_replayer_complete_history.json"));
            yield return WorkflowHistory.FromJson(
                "some-id1", ReadAllFileText("test_replayer_nondeterministic_history.json"));
        }

        // Fail slow sync iter
        var replayer = new WorkflowReplayer(
            new WorkflowReplayerOptions().AddWorkflow<SayHelloWorkflow>());
        var results = (await replayer.ReplayWorkflowsAsync(HistoryIter())).ToList();
        Assert.Equal(2, results.Count);
        Assert.Null(results[0].ReplayFailure);
        Assert.IsType<InvalidWorkflowOperationException>(results[1].ReplayFailure);

        // Fail fast sync iter
        await Assert.ThrowsAsync<InvalidWorkflowOperationException>(
            () => replayer.ReplayWorkflowsAsync(HistoryIter(), throwOnReplayFailure: true));

        static async IAsyncEnumerable<WorkflowHistory> HistoryIterAsync()
        {
            yield return WorkflowHistory.FromJson(
                "some-id1", ReadAllFileText("test_replayer_complete_history.json"));
            yield return WorkflowHistory.FromJson(
                "some-id1", ReadAllFileText("test_replayer_nondeterministic_history.json"));
        }

        // Fail slow sync iter
        var resultList = new List<WorkflowReplayResult>();
        await foreach (var res in replayer.ReplayWorkflowsAsync(HistoryIterAsync()))
        {
            resultList.Add(res);
        }
        Assert.Equal(2, resultList.Count);
        Assert.Null(resultList[0].ReplayFailure);
        Assert.IsType<InvalidWorkflowOperationException>(resultList[1].ReplayFailure);

        // Fail fast async iter
        await Assert.ThrowsAsync<InvalidWorkflowOperationException>(async () =>
        {
            await foreach (var res in replayer.ReplayWorkflowsAsync(HistoryIterAsync(), throwOnReplayFailure: true))
            {
            }
        });
    }

    private static string ReadAllFileText(
        string relativePath, [CallerFilePath] string sourceFilePath = "") =>
        File.ReadAllText(Path.Join(sourceFilePath, "..", relativePath));
}