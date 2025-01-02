#pragma warning disable SA1201, SA1204 // We want to have classes near their tests

namespace Temporalio.Tests.Worker;

using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Worker;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;
using static Temporalio.Tests.Worker.WorkflowWorkerTests;

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
        private bool waiting;
        private bool finish;

        [WorkflowRun]
        public async Task<string> RunAsync(SayHelloParams parms)
        {
            var result = await Workflow.ExecuteActivityAsync(
                () => SayHelloActivities.SayHello(parms.Name),
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
            var arg = new SayHelloParams("Temporal");
            var handle = await Env.Client.StartWorkflowAsync(
                (SayHelloWorkflow wf) => wf.RunAsync(arg),
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
        var json = TestUtils.ReadAllFileText("Histories/replayer-test.complete.json");
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
            var arg = new SayHelloParams("Temporal", ShouldWait: true);
            var handle = await Env.Client.StartWorkflowAsync(
                (SayHelloWorkflow wf) => wf.RunAsync(arg),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Wait until waiting
            await AssertMore.EventuallyAsync(
                async () => Assert.True(await handle.QueryAsync(wf => wf.Waiting())));

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
            var arg = new SayHelloParams("Temporal", ShouldError: true);
            var handle = await Env.Client.StartWorkflowAsync(
                (SayHelloWorkflow wf) => wf.RunAsync(arg),
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
            var arg = new SayHelloParams("Temporal", ShouldCauseNonDeterminism: true);
            var handle = await Env.Client.StartWorkflowAsync(
                (SayHelloWorkflow wf) => wf.RunAsync(arg),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", await handle.GetResultAsync());

            // Collect history and replay it
            var history = await handle.FetchHistoryAsync();
            var replayer = new WorkflowReplayer(
                new WorkflowReplayerOptions().AddWorkflow<SayHelloWorkflow>());
            var exc = await Assert.ThrowsAsync<WorkflowNondeterminismException>(
                async () => await replayer.ReplayWorkflowAsync(history));
            Assert.Contains("Nondeterminism", exc.Message);

            // Also confirm we can ask it not to throw
            var result = await replayer.ReplayWorkflowAsync(history, throwOnReplayFailure: false);
            exc = Assert.IsType<WorkflowNondeterminismException>(result.ReplayFailure);
            Assert.Contains("Nondeterminism", exc.Message);
        });
    }

    [Fact]
    public async Task ReplayWorkflowAsync_NonDeterministicRunFromJson_Fails()
    {
        // Replay history from JSON
        var history = WorkflowHistory.FromJson(
            "some-id",
            TestUtils.ReadAllFileText("Histories/replayer-test.nondeterministic.json"));
        var replayer = new WorkflowReplayer(
            new WorkflowReplayerOptions().AddWorkflow<SayHelloWorkflow>());
        var exc = await Assert.ThrowsAsync<WorkflowNondeterminismException>(
            () => replayer.ReplayWorkflowAsync(history));
        Assert.Contains("Nondeterminism", exc.Message);

        // Also confirm we can ask it not to throw
        var result = await replayer.ReplayWorkflowAsync(history, throwOnReplayFailure: false);
        exc = Assert.IsType<WorkflowNondeterminismException>(result.ReplayFailure);
        Assert.Contains("Nondeterminism", exc.Message);
    }

    [Fact]
    public async Task ReplayWorkflowAsync_MultipleHistories_WorksProperly()
    {
        static IEnumerable<WorkflowHistory> HistoryIter()
        {
            yield return WorkflowHistory.FromJson(
                "some-id1",
                TestUtils.ReadAllFileText("Histories/replayer-test.complete.json"));
            yield return WorkflowHistory.FromJson(
                "some-id1",
                TestUtils.ReadAllFileText("Histories/replayer-test.nondeterministic.json"));
        }

        // Fail slow sync iter
        var replayer = new WorkflowReplayer(
            new WorkflowReplayerOptions().AddWorkflow<SayHelloWorkflow>());
        var results = (await replayer.ReplayWorkflowsAsync(HistoryIter())).ToList();
        Assert.Equal(2, results.Count);
        Assert.Null(results[0].ReplayFailure);
        Assert.IsType<WorkflowNondeterminismException>(results[1].ReplayFailure);

        // Fail fast sync iter
        await Assert.ThrowsAsync<WorkflowNondeterminismException>(
            () => replayer.ReplayWorkflowsAsync(HistoryIter(), throwOnReplayFailure: true));

        static async IAsyncEnumerable<WorkflowHistory> HistoryIterAsync()
        {
            yield return WorkflowHistory.FromJson(
                "some-id1",
                TestUtils.ReadAllFileText("Histories/replayer-test.complete.json"));
            yield return WorkflowHistory.FromJson(
                "some-id1",
                TestUtils.ReadAllFileText("Histories/replayer-test.nondeterministic.json"));
        }

        // Fail slow sync iter
        var resultList = new List<WorkflowReplayResult>();
        await foreach (var res in replayer.ReplayWorkflowsAsync(HistoryIterAsync()))
        {
            resultList.Add(res);
        }
        Assert.Equal(2, resultList.Count);
        Assert.Null(resultList[0].ReplayFailure);
        Assert.IsType<WorkflowNondeterminismException>(resultList[1].ReplayFailure);

        // Fail fast async iter
        await Assert.ThrowsAsync<WorkflowNondeterminismException>(async () =>
        {
            await foreach (var res in replayer.ReplayWorkflowsAsync(HistoryIterAsync(), throwOnReplayFailure: true))
            {
            }
        });
    }

    public class WorkflowResultInterceptor : IWorkerInterceptor
    {
        public Queue<object?> WorkflowResults { get; } = new();

        public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) =>
            new WorkflowInbound(nextInterceptor, WorkflowResults);

        public class WorkflowInbound : WorkflowInboundInterceptor
        {
            private Queue<object?> workflowResults = new();

            public WorkflowInbound(WorkflowInboundInterceptor next, Queue<object?> workflowResults)
                : base(next)
            {
                this.workflowResults = workflowResults;
            }

            public override async Task<object?> ExecuteWorkflowAsync(ExecuteWorkflowInput input)
            {
                var res = await base.ExecuteWorkflowAsync(input);
                workflowResults.Enqueue(res);
                return res;
            }
        }
    }

    [Fact]
    public async Task ReplayWorkflowAsync_EventLoop_CorrectOrdering()
    {
        var expected = new[]
        {
            "sig-before-sync",
            "sig-before-1",
            "sig-before-2",
            "main-start",
            "timer-sync",
            "activity-sync",
            "activity-1",
            "activity-2",
            "sig-1-sync",
            "sig-1-1",
            "sig-1-2",
            "update-1-sync",
            "update-1-1",
            "update-1-2",
            "timer-1",
            "timer-2",
        };
        var expectedDoubleStart = new[]
        {
            "sig-before-sync",
            "sig-before-1",
            "sig-before-2",
            "sig-1-sync",
            "sig-1-1",
            "sig-1-2",
            "main-start",
            "timer-sync",
            "activity-sync",
            "update-1-sync",
            "update-1-1",
            "update-1-2",
            "activity-1",
            "activity-2",
            "timer-1",
            "timer-2",
        };
        var expecteds = new[]
        {
            (expected, "Histories/expected-event-loop-ordering.json"),
            (expectedDoubleStart, "Histories/expected-event-loop-ordering-double-signal-start.json"),
        };
        for (var i = 0; i < expecteds.Length; i++)
        {
            var (exp, file) = expecteds[i];
            var history = WorkflowHistory.FromJson(
                "some-id",
                TestUtils.ReadAllFileText(file));
            var wri = new WorkflowResultInterceptor();
            var replayer = new WorkflowReplayer(
                new WorkflowReplayerOptions()
                { Interceptors = new[] { wri } }
                .AddWorkflow<EventLoopTracingWorkflow>());
            await replayer.ReplayWorkflowAsync(history);
            Assert.Equal(exp, wri.WorkflowResults.Dequeue());
        }
    }
}
