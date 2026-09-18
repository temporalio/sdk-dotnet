namespace Temporalio.Tests.Testing;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Temporalio.Activities;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

[CloudTestExclusion(
    CloudTestExclusionReason.RequiresLocalServer,
    "Covers local and time-skipping test-server APIs.")]
public class WorkflowEnvironmentTests : TestBase
{
    // Time-skipping test server only runs on x86/x64
    public sealed class OnlyIntelFactAttribute : FactAttribute
    {
        public OnlyIntelFactAttribute()
        {
            if (RuntimeInformation.ProcessArchitecture != Architecture.X86 &&
                RuntimeInformation.ProcessArchitecture != Architecture.X64)
            {
                Skip = "Time-skipping test server only works on x86/x64 platforms";
            }
        }
    }

    public WorkflowEnvironmentTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Workflow]
    public class ReallySlowWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            await Workflow.DelayAsync(TimeSpan.FromDays(2));
            return "all done";
        }

        [WorkflowQuery]
        public DateTime CurrentTime() => Workflow.UtcNow;

        [WorkflowSignal]
        public Task SomeSignalAsync() => Task.CompletedTask;
    }

    [OnlyIntelFact]
    public async Task StartTimeSkippingAsync_SlowWorkflowAutoSkip_ProperlySkips()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<ReallySlowWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Check that timestamp is around now
            AssertMore.DateTimeFromUtcNow(await env.GetCurrentTimeAsync(), TimeSpan.Zero);

            // Run workflow
            var result = await env.Client.ExecuteWorkflowAsync(
                (ReallySlowWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("all done", result);

            // Check that the time is around 2 days from now
            AssertMore.DateTimeFromUtcNow(await env.GetCurrentTimeAsync(), TimeSpan.FromDays(2));
        });
    }

    [OnlyIntelFact]
    public async Task StartTimeSkippingAsync_SlowWorkflowManualSkip_ProperlySkips()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<ReallySlowWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Start workflow
            var handle = await env.Client.StartWorkflowAsync(
                (ReallySlowWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            async Task<DateTime> WorkflowCurrentTimeAsync()
            {
                // We send signal first since query timestamp is based on last non-query-only
                // workflow task
                await handle!.SignalAsync(wf => wf.SomeSignalAsync());
                return await handle.QueryAsync(wf => wf.CurrentTime());
            }

            // Confirm query will say we're near current time
            AssertMore.DateTimeFromUtcNow(await WorkflowCurrentTimeAsync(), TimeSpan.Zero);

            // Sleep and confirm query will say we're near that time
            await env.DelayAsync(TimeSpan.FromHours(2));
            AssertMore.DateTimeFromUtcNow(await WorkflowCurrentTimeAsync(), TimeSpan.FromHours(2));
        });
    }

    // See https://github.com/temporalio/sdk-dotnet/issues/179. Updates used to hang on the
    // time-skipping test server because it did not treat update calls as long polls, a test-server
    // bug fixed in https://github.com/temporalio/sdk-java/issues/2142.
    [Workflow]
    public class UpdateWhileSkippingWorkflow
    {
        private int counter;

        [WorkflowRun]
        public async Task<int> RunAsync()
        {
            // A long sleep the test server must skip once we wait on the result
            await Workflow.DelayAsync(TimeSpan.FromDays(2));
            return counter;
        }

        // Note: no sleep in here on purpose. Time skipping does not apply while an update call
        // is in flight, only while a client is blocked waiting on the workflow result, so a sleep
        // here would cost real test time rather than being skipped.
        [WorkflowUpdate]
        public Task<int> IncrementAsync(int amount)
        {
            counter += amount;
            return Task.FromResult(counter);
        }
    }

    [OnlyIntelFact]
    public async Task StartTimeSkippingAsync_UpdateDuringWorkflow_DoesNotHang()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddWorkflow<UpdateWhileSkippingWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (UpdateWhileSkippingWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            // Several sequential updates, each returning the accumulated value. Previously the
            // first of these never returned.
            Assert.Equal(1, await handle.ExecuteUpdateAsync(wf => wf.IncrementAsync(1)));
            Assert.Equal(3, await handle.ExecuteUpdateAsync(wf => wf.IncrementAsync(2)));

            // Same via the start-then-wait form
            var updateHandle = await handle.StartUpdateAsync(
                wf => wf.IncrementAsync(3), new(WorkflowUpdateStage.Accepted));
            Assert.Equal(6, await updateHandle.GetResultAsync());

            // The workflow must still be running with its update state intact, i.e. the server
            // must not have reported it complete early after the first update
            Assert.Equal(6, await handle.GetResultAsync());

            // Time skipping must still work after updates: the 2 day timer is skipped rather than
            // really waited on, so server time is ~2 days ahead while the test took seconds
            AssertMore.DateTimeFromUtcNow(await env.GetCurrentTimeAsync(), TimeSpan.FromDays(2));
        });
    }

    public class ActivityWaitActivities
    {
        private readonly WorkflowEnvironment env;

        public ActivityWaitActivities(WorkflowEnvironment env) => this.env = env;

        [Activity]
        public async Task<string> SimulateHeartbeatTimeoutAsync()
        {
            // Sleep for twice as long as heartbeat timeout
            var heartbeatTimeout = ActivityExecutionContext.Current.Info.HeartbeatTimeout!.Value;
            await env.DelayAsync(TimeSpan.FromTicks(heartbeatTimeout.Ticks * 2));
            return "all done";
        }
    }

    [Workflow]
    public class ActivityWaitWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            // Run activity with 20 second heartbeat
            return await Workflow.ExecuteActivityAsync(
                (ActivityWaitActivities act) => act.SimulateHeartbeatTimeoutAsync(),
                new()
                {
                    ScheduleToCloseTimeout = TimeSpan.FromSeconds(1000),
                    HeartbeatTimeout = TimeSpan.FromSeconds(20),
                    RetryPolicy = new() { MaximumAttempts = 1 },
                });
        }
    }

    [OnlyIntelFact]
    public async Task StartTimeSkippingAsync_MissesHeartbeatTimeout_TimesOut()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddActivity(new ActivityWaitActivities(env).SimulateHeartbeatTimeoutAsync).
                AddWorkflow<ActivityWaitWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Run workflow and check failure
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                env.Client.ExecuteWorkflowAsync(
                    (ActivityWaitWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
            // Check causes too
            var actExc = Assert.IsType<ActivityFailureException>(exc.InnerException);
            var toExc = Assert.IsType<TimeoutFailureException>(actExc.InnerException);
            Assert.Equal(TimeoutType.Heartbeat, toExc.TimeoutType);
        });
    }

    [Workflow]
    public class ShortSleepWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            await Workflow.DelayAsync(TimeSpan.FromSeconds(3));
            return "all done";
        }
    }

    [OnlyIntelFact]
    public async Task StartTimeSkippingAsync_AutoTimeSkippingDisabled_SleepsFullTime()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<ShortSleepWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Confirm auto time skipping is within 2.5s
            var watch = Stopwatch.StartNew();
            await env.Client.ExecuteWorkflowAsync(
                (ShortSleepWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2.5));

            // Confirm when auto disabled it does wait more than 2.5s
            await env.WithAutoTimeSkippingDisabledAsync(async () =>
            {
                watch = Stopwatch.StartNew();
                await env.Client.ExecuteWorkflowAsync(
                    (ShortSleepWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                Assert.True(watch.Elapsed > TimeSpan.FromSeconds(2.5));
            });
        });
    }

    [Workflow]
    public class LongSleepWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            await Workflow.DelayAsync(TimeSpan.FromSeconds(30));
            return "all done";
        }

        [WorkflowSignal]
        public async Task SomeSignalAsync()
        {
        }
    }

    [OnlyIntelFact]
    public async Task StartTimeSkippingAsync_AutoTimeSkippingDisabled_RestoresAfterCall()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<LongSleepWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var watch = Stopwatch.StartNew();
            var handle = await env.Client.StartWorkflowAsync(
                (LongSleepWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            // Do a signal and confirm that it starts skipping again after the signal. This used to
            // not restore auto-skipping when we didn't properly restore behavior.
            await env.WithAutoTimeSkippingDisabledAsync(async () =>
            {
                await handle.SignalAsync(wf => wf.SomeSignalAsync());
            });
            // Wait for result
            await handle.GetResultAsync();
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
        });
    }

    [Fact]
    public async Task StartLocal_SearchAttributes_ProperlyRegistered()
    {
        // Prepare attrs
        var attrBool = SearchAttributeKey.CreateBool("DotNetTemporalTestBool");
        var attrDateTime = SearchAttributeKey.CreateDateTimeOffset("DotNetTemporalTestDateTime");
        var attrDouble = SearchAttributeKey.CreateDouble("DotNetTemporalTestDouble");
        var attrKeyword = SearchAttributeKey.CreateKeyword("DotNetTemporalTestKeyword");
        var attrKeywordList = SearchAttributeKey.CreateKeywordList("DotNetTemporalTestKeywordList");
        var attrLong = SearchAttributeKey.CreateLong("DotNetTemporalTestLong");
        var attrText = SearchAttributeKey.CreateText("DotNetTemporalTestText");
        var attrVals = new SearchAttributeCollection.Builder().
            Set(attrBool, true).
            Set(attrDateTime, new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)).
            Set(attrDouble, 123.45).
            Set(attrKeyword, "SomeKeyword").
            Set(attrKeywordList, new[] { "SomeKeyword1", "SomeKeyword2" }).
            Set(attrLong, 678).
            Set(attrText, "SomeText").
            ToSearchAttributeCollection();
        var attrs = new SearchAttributeKey[]
        {
            attrBool, attrDateTime, attrDouble, attrKeyword, attrKeywordList, attrLong, attrText,
        };

        // Confirm that when used in env without SAs it fails
        await using var env1 = await WorkflowEnvironment.StartLocalAsync();
        var exc = await Assert.ThrowsAsync<RpcException>(
            () => env1.Client.StartWorkflowAsync(
                "my-workflow",
                Array.Empty<object?>(),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: $"tq-{Guid.NewGuid()}")
                {
                    TypedSearchAttributes = attrVals,
                }));
        Assert.Contains("no mapping defined", exc.Message);

        // Confirm that when used in env with SAs it succeeds
        await using var env2 = await WorkflowEnvironment.StartLocalAsync(
            new() { SearchAttributes = attrs });
        var handle = await env2.Client.StartWorkflowAsync(
            "my-workflow",
            Array.Empty<object?>(),
            new(id: $"wf-{Guid.NewGuid()}", taskQueue: $"tq-{Guid.NewGuid()}")
            {
                TypedSearchAttributes = attrVals,
            });
        Assert.Equal(attrVals, (await handle.DescribeAsync()).TypedSearchAttributes);
    }
}
