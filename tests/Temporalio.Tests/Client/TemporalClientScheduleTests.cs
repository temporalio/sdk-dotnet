namespace Temporalio.Tests.Client;

using System.Collections.Generic;
using Temporalio.Api.Enums.V1;
using Temporalio.Client.Schedules;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Xunit;
using Xunit.Abstractions;

public class TemporalClientScheduleTests : WorkflowEnvironmentTestBase
{
    public TemporalClientScheduleTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task CreateScheduleAsync_Basics_Succeeds()
    {
        await TestUtils.AssertNoSchedulesAsync(Client);

        // Create a schedule with a lot of stuff
        var arg = new KSWorkflowParams(new KSAction(Result: new("some result")));
        var action = ScheduleActionStartWorkflow.Create(
            (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)
            {
                ExecutionTimeout = TimeSpan.FromHours(1),
                Memo = new Dictionary<string, object> { ["memokey1"] = "memoval1" },
            });
        var schedule = new Schedule(
            Action: action,
            Spec: new()
            {
                Calendars = new List<ScheduleCalendarSpec>
                {
                    new()
                    {
                        Second = new List<ScheduleRange> { new(1) },
                        Minute = new List<ScheduleRange> { new(2, 3) },
                        Hour = new List<ScheduleRange> { new(4, 5, 6) },
                        DayOfMonth = new List<ScheduleRange> { new(7) },
                        Month = new List<ScheduleRange> { new(9) },
                        Year = new List<ScheduleRange> { new(2080) },
                        // Intentionally leave day of week absent to check default
                        Comment = "spec comment 1",
                    },
                },
                Intervals = new List<ScheduleIntervalSpec>
                {
                    new(Every: TimeSpan.FromDays(10), Offset: TimeSpan.FromDays(2)),
                },
                CronExpressions = new[] { "0 12 * * MON" },
                Skip = new List<ScheduleCalendarSpec>
                {
                    new() { Year = new List<ScheduleRange> { new(2050) } },
                },
                StartAt = new(2060, 7, 8, 9, 10, 11, DateTimeKind.Utc),
                Jitter = TimeSpan.FromSeconds(80),
            })
        {
            Policy = new()
            {
                Overlap = ScheduleOverlapPolicy.BufferOne,
                CatchupWindow = TimeSpan.FromMinutes(5),
                PauseOnFailure = true,
            },
            State = new()
            {
                Note = "sched note 1",
                Paused = true,
                RemainingActions = 30,
            },
        };
        var handle = await Client.CreateScheduleAsync(
            $"schedule-{Guid.NewGuid()}",
            schedule,
            new() { Memo = new Dictionary<string, object> { ["memokey2"] = "memoval2" } });

        // Describe it and check values
        var desc = await handle.DescribeAsync();
        Assert.Equal(handle.Id, desc.Id);
        var descAction = Assert.IsType<ScheduleActionStartWorkflow>(desc.Schedule.Action);
        Assert.Equal(action.Workflow, descAction.Workflow);
        var descParams = await Assert.IsType<EncodedRawValue>(descAction.Args.Single()).ToValueAsync<KSWorkflowParams>();
        Assert.Equal(
            "some result",
            descParams.Actions?.Single()?.Result?.Value?.ToString());
        Assert.Equal(action.Options.ExecutionTimeout, descAction.Options.ExecutionTimeout);
        Assert.Equal(
            "memoval1",
            await Assert.IsType<EncodedRawValue>(descAction.Options.Memo!["memokey1"]).ToValueAsync<string>());
        Assert.Equal("memoval2", await desc.Memo["memokey2"].ToValueAsync<string>());
        AssertMore.EqualAsJson(
            schedule.Spec with
            {
                // Cron becomes calendar
                CronExpressions = Array.Empty<string>(),
                Calendars = new List<ScheduleCalendarSpec>
                {
                    schedule.Spec.Calendars.Single(),
                    new()
                    {
                        Second = new List<ScheduleRange> { new(0) },
                        Minute = new List<ScheduleRange> { new(0) },
                        Hour = new List<ScheduleRange> { new(12) },
                        DayOfMonth = new List<ScheduleRange> { new(1, 31) },
                        Month = new List<ScheduleRange> { new(1, 12) },
                        DayOfWeek = new List<ScheduleRange> { new(1) },
                    },
                },
            },
            desc.Schedule.Spec);
        AssertMore.EqualAsJson(schedule.Policy, desc.Schedule.Policy);
        AssertMore.EqualAsJson(schedule.State, desc.Schedule.State);

        // Update to just change the schedule workflow's task timeout
        await handle.UpdateAsync(input =>
        {
            var action = Assert.IsType<ScheduleActionStartWorkflow>(input.Description.Schedule.Action);
            action.Options.TaskTimeout = TimeSpan.FromMinutes(7);
            return new(input.Description.Schedule);
        });
        desc = await handle.DescribeAsync();
        descAction = Assert.IsType<ScheduleActionStartWorkflow>(desc.Schedule.Action);
        Assert.Equal(TimeSpan.FromMinutes(7), descAction.Options.TaskTimeout);

        // Update but cancel update
        var expectedUpdateTime = desc.Info.LastUpdatedAt!;
        await handle.UpdateAsync(_ => (ScheduleUpdate?)null);
        Assert.Equal(expectedUpdateTime, (await handle.DescribeAsync()).Info.LastUpdatedAt);

        // Update to only be a schedule of simple defaults
        var newSched = new Schedule(
            Action: action,
            Spec: new())
        { State = new() { Paused = true } };
        await handle.UpdateAsync(_ => new(newSched));
        desc = await handle.DescribeAsync();
        Assert.NotEqual(expectedUpdateTime, desc.Info.LastUpdatedAt);
        // Try to create a duplicate
        await Assert.ThrowsAsync<ScheduleAlreadyRunningException>(
            () => Client.CreateScheduleAsync(handle.Id, newSched));

        // Confirm paused
        Assert.True(desc.Schedule.State.Paused);
        // Pause and confirm still paused
        await handle.PauseAsync();
        desc = await handle.DescribeAsync();
        Assert.True(desc.Schedule.State.Paused);
        Assert.Equal("Paused via .NET SDK", desc.Schedule.State.Note);
        // Unpause
        await handle.UnpauseAsync();
        desc = await handle.DescribeAsync();
        Assert.False(desc.Schedule.State.Paused);
        Assert.Equal("Unpaused via .NET SDK", desc.Schedule.State.Note);
        // Pause with custom message
        await handle.PauseAsync("test1");
        desc = await handle.DescribeAsync();
        Assert.True(desc.Schedule.State.Paused);
        Assert.Equal("test1", desc.Schedule.State.Note);
        // Unpause with custom message
        await handle.UnpauseAsync("test2");
        desc = await handle.DescribeAsync();
        Assert.False(desc.Schedule.State.Paused);
        Assert.Equal("test2", desc.Schedule.State.Note);

        // Trigger
        Assert.Equal(0, desc.Info.NumActions);
        await handle.TriggerAsync();
        await AssertMore.EqualEventuallyAsync(1, async () =>
        {
            desc = await handle.DescribeAsync();
            return desc.Info.NumActions;
        });

        // Get workflow run and check its results
        var exec = Assert.IsType<ScheduleActionExecutionStartWorkflow>(desc.Info.RecentActions.Single().Action);
        Assert.Equal(
            "some result",
            await Client.GetWorkflowHandle(exec.WorkflowId, exec.FirstExecutionRunId).GetResultAsync<string>());

        // Create 4 more schedules of the same type and confirm they are in the list eventually
        var expectedIds = new List<string>
        {
            handle.Id,
            (await Client.CreateScheduleAsync($"{handle.Id}-1", newSched)).Id,
            (await Client.CreateScheduleAsync($"{handle.Id}-2", newSched)).Id,
            (await Client.CreateScheduleAsync($"{handle.Id}-3", newSched)).Id,
            (await Client.CreateScheduleAsync($"{handle.Id}-4", newSched)).Id,
        };
        await AssertMore.EqualEventuallyAsync(expectedIds, async () =>
        {
            var actualIds = new List<string>();
            await foreach (var sched in Client.ListSchedulesAsync())
            {
                actualIds.Add(sched.Id);
            }
            actualIds.Sort();
            return actualIds;
        });

        // Delete when done
        await TestUtils.DeleteAllSchedulesAsync(Client);
    }

    [Fact]
    public async Task CreateScheduleAsync_CalendarSpecDefaults_AreProper()
    {
        await TestUtils.AssertNoSchedulesAsync(Client);

        var arg = new KSWorkflowParams(new KSAction(Result: new("some result")));
        var handle = await Client.CreateScheduleAsync(
            $"schedule-{Guid.NewGuid()}",
            new(
                Action: ScheduleActionStartWorkflow.Create(
                    (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)),
                Spec: new() { Calendars = new List<ScheduleCalendarSpec> { new() } })
            {
                State = new() { Paused = true },
            });
        var desc = await handle.DescribeAsync();
        AssertMore.EqualAsJson(new ScheduleCalendarSpec(), desc.Schedule.Spec.Calendars.Single());
        // Make sure that every next time has all zero time portion and is one day after the
        // previous
        Assert.Equal(10, desc.Info.NextActionTimes.Count);
        for (var i = 0; i < desc.Info.NextActionTimes.Count; i++)
        {
            var nextTime = desc.Info.NextActionTimes.ElementAt(i);
            Assert.Equal(0, nextTime.Second);
            Assert.Equal(0, nextTime.Minute);
            Assert.Equal(0, nextTime.Hour);
            if (i > 0)
            {
                Assert.Equal(
                    desc.Info.NextActionTimes.ElementAt(i - 1) + TimeSpan.FromDays(1),
                    nextTime);
            }
        }

        // Delete when done
        await TestUtils.DeleteAllSchedulesAsync(Client);
    }

    [Fact]
    public async Task CreateScheduleAsync_TriggerImmediately_Succeeds()
    {
        await TestUtils.AssertNoSchedulesAsync(Client);

        // Create paused schedule that triggers immediately
        var arg = new KSWorkflowParams(new KSAction(Result: new("some result")));
        var handle = await Client.CreateScheduleAsync(
            $"schedule-{Guid.NewGuid()}",
            new(
                Action: ScheduleActionStartWorkflow.Create(
                    (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)),
                Spec: new())
            {
                State = new() { Paused = true },
            },
            new() { TriggerImmediately = true });

        // Confirm workflow result
        var desc = await handle.DescribeAsync();
        Assert.Equal(1, desc.Info.NumActions);
        var exec = Assert.IsType<ScheduleActionExecutionStartWorkflow>(desc.Info.RecentActions.Single().Action);
        Assert.Equal(
            "some result",
            await Client.GetWorkflowHandle(exec.WorkflowId, exec.FirstExecutionRunId).GetResultAsync<string>());

        // Delete when done
        await TestUtils.DeleteAllSchedulesAsync(Client);
    }

    [Fact]
    public async Task CreateScheduleAsync_Backfill_CreatesProperActions()
    {
        await TestUtils.AssertNoSchedulesAsync(Client);

        // Create paused schedule that runs every minute and has two backfills
        var now = DateTime.UtcNow;
        // Intervals align to the epoch boundary, so trim off seconds
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMinute));
        var arg = new KSWorkflowParams(new KSAction(Result: new("some result")));
        var handle = await Client.CreateScheduleAsync(
            $"schedule-{Guid.NewGuid()}",
            new(
                Action: ScheduleActionStartWorkflow.Create(
                    (IKitchenSinkWorkflow wf) => wf.RunAsync(arg),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)),
                Spec: new()
                {
                    Intervals = new List<ScheduleIntervalSpec> { new(Every: TimeSpan.FromMinutes(1)) },
                })
            {
                State = new() { Paused = true },
            },
            new()
            {
                // Backfill across 61 seconds starting 1s before minute boundary and confirm 2 count
                Backfills = new List<ScheduleBackfill>
                {
                    new(
                        StartAt: now - TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1),
                        EndAt: now - TimeSpan.FromMinutes(9),
                        Overlap: ScheduleOverlapPolicy.AllowAll),
                },
            });
        Assert.Equal(2, (await handle.DescribeAsync()).Info.NumActions);

        // Add two more backfills and -2m will be deduped
        await handle.BackfillAsync(new List<ScheduleBackfill>
        {
            new(
                StartAt: now - TimeSpan.FromMinutes(4),
                EndAt: now - TimeSpan.FromMinutes(2),
                Overlap: ScheduleOverlapPolicy.AllowAll),
            new(
                StartAt: now - TimeSpan.FromMinutes(2),
                EndAt: now,
                Overlap: ScheduleOverlapPolicy.AllowAll),
        });
        Assert.Equal(6, (await handle.DescribeAsync()).Info.NumActions);

        // Delete when done
        await TestUtils.DeleteAllSchedulesAsync(Client);
    }
}