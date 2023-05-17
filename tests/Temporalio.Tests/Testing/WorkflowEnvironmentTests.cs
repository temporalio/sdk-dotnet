#pragma warning disable SA1201, SA1204 // We want to have classes near their tests

namespace Temporalio.Tests.Testing;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Temporalio.Activities;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

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
        public static readonly ReallySlowWorkflow Ref = WorkflowRefs.Create<ReallySlowWorkflow>();

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
                    ReallySlowWorkflow.Ref.RunAsync,
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
                    ReallySlowWorkflow.Ref.RunAsync,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            async Task<DateTime> WorkflowCurrentTimeAsync()
            {
                // We send signal first since query timestamp is based on last non-query-only
                // workflow task
                await handle!.SignalAsync(ReallySlowWorkflow.Ref.SomeSignalAsync);
                return await handle.QueryAsync(ReallySlowWorkflow.Ref.CurrentTime);
            }

            // Confirm query will say we're near current time
            AssertMore.DateTimeFromUtcNow(await WorkflowCurrentTimeAsync(), TimeSpan.Zero);

            // Sleep and confirm query will say we're near that time
            await env.DelayAsync(TimeSpan.FromHours(2));
            AssertMore.DateTimeFromUtcNow(await WorkflowCurrentTimeAsync(), TimeSpan.FromHours(2));
        });
    }

    public class ActivityWaitActivities
    {
        public static readonly ActivityWaitActivities Ref = ActivityRefs.Create<ActivityWaitActivities>();
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
        public static readonly ActivityWaitWorkflow Ref = WorkflowRefs.Create<ActivityWaitWorkflow>();

        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            // Run activity with 20 second heartbeat
            return await Workflow.ExecuteActivityAsync(
                ActivityWaitActivities.Ref.SimulateHeartbeatTimeoutAsync,
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
                    ActivityWaitWorkflow.Ref.RunAsync,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
            // Check causes too
            var actExc = Assert.IsType<ActivityFailureException>(exc.InnerException);
            var toExc1 = Assert.IsType<TimeoutFailureException>(actExc.InnerException);
            var toExc2 = Assert.IsType<TimeoutFailureException>(toExc1.InnerException);
            Assert.Equal(TimeoutType.Heartbeat, toExc2.TimeoutType);
        });
    }

    [Workflow]
    public class ShortSleepWorkflow
    {
        public static readonly ShortSleepWorkflow Ref = WorkflowRefs.Create<ShortSleepWorkflow>();

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
                ShortSleepWorkflow.Ref.RunAsync,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2.5));

            // Confirm when auto disabled it does wait more than 2.5s
            await env.WithAutoTimeSkippingDisabledAsync(async () =>
            {
                watch = Stopwatch.StartNew();
                await env.Client.ExecuteWorkflowAsync(
                    ShortSleepWorkflow.Ref.RunAsync,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                Assert.True(watch.Elapsed > TimeSpan.FromSeconds(2.5));
            });
        });
    }
}