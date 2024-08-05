#pragma warning disable CA1724 // Don't care about name conflicts
#pragma warning disable SA1201, SA1204 // We want to have classes near their tests

namespace Temporalio.Tests.Worker;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Client.Schedules;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Runtime;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class WorkflowWorkerTests : WorkflowEnvironmentTestBase
{
    public WorkflowWorkerTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Workflow]
    public class SimpleWorkflow
    {
        [WorkflowRun]
        public Task<string> RunAsync(string name) => Task.FromResult($"Hello, {name}!");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Simple_Succeeds()
    {
        await ExecuteWorkerAsync<SimpleWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync(
                (SimpleWorkflow wf) => wf.RunAsync("Temporal"),
                new(id: $"dotnet-workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_ManualDefinition_Succeeds()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddWorkflow(WorkflowDefinition.Create(typeof(SimpleWorkflow), "other-name", null)));
        await worker.ExecuteAsync(async () =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync<string>(
                "other-name",
                new[] { "Temporal" },
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_UnknownWorkflow_ProperlyFails()
    {
        await ExecuteWorkerAsync<SimpleWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                "not-found-workflow",
                Array.Empty<object?>(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await AssertTaskFailureContainsEventuallyAsync(handle, "not registered");
        });
    }

    [Workflow]
    public interface IInterfaceWorkflow
    {
        [WorkflowRun]
        Task<string> RunAsync();

        [Workflow("InterfaceWorkflow")]
        public class InterfaceWorkflow : IInterfaceWorkflow
        {
            [WorkflowRun]
            public Task<string> RunAsync() => Task.FromResult($"Hello, Temporal!");
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Interface_Succeeds()
    {
        await ExecuteWorkerAsync<IInterfaceWorkflow.InterfaceWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync(
                (IInterfaceWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Workflow]
    public record RecordWorkflow
    {
        // TODO(cretz): When https://github.com/dotnet/csharplang/issues/7047 is done, test
        // [WorkflowInit] on record constructor
        [WorkflowRun]
        public Task<string> RunAsync() => Task.FromResult($"Hello, Temporal!");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Record_Succeeds()
    {
        await ExecuteWorkerAsync<RecordWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync(
                (RecordWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

#pragma warning disable IDE0250, CA1815, CA1815 // We don't care about struct rules here
    [Workflow]
    public struct StructWorkflow
    {
        [WorkflowRun]
        public Task<string> RunAsync() => Task.FromResult($"Hello, Temporal!");
    }
#pragma warning restore IDE0250, CA1815, CA1815

    [Fact]
    public async Task ExecuteWorkflowAsync_Struct_Succeeds()
    {
        await ExecuteWorkerAsync<StructWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync(
                (StructWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Workflow]
    public class WorkflowInitWorkflow
    {
        private readonly string name;

        [WorkflowInit]
        public WorkflowInitWorkflow(string name) => this.name = name;

        [WorkflowRun]
        public Task<string> RunAsync(string _) => Task.FromResult($"Hello, {name}!");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WorkflowInit_Succeeds()
    {
        await ExecuteWorkerAsync<WorkflowInitWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync(
                (WorkflowInitWorkflow wf) => wf.RunAsync("Temporal"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Workflow]
    public class WorkflowInitNoParamsWorkflow
    {
        [WorkflowInit]
        public WorkflowInitNoParamsWorkflow()
        {
        }

        [WorkflowRun]
        public Task<string> RunAsync() => Task.FromResult($"Hello, Temporal!");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WorkflowInitNoParams_Succeeds()
    {
        await ExecuteWorkerAsync<WorkflowInitNoParamsWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync(
                (WorkflowInitNoParamsWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Workflow]
    public class StandardLibraryCallsWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(Scenario scenario)
        {
            switch (scenario)
            {
                // Bad
                case Scenario.TaskDelay:
                    await Task.Delay(5000);
                    return "done";
                case Scenario.TaskRun:
                    return await Task.Run(async () => "done");
                case Scenario.TaskFactoryStartNewDefaultScheduler:
                    return await Task.Factory.StartNew(
                        () => "done", default, default, TaskScheduler.Default);
                case Scenario.TaskContinueWithDefaultScheduler:
                    return await Task.CompletedTask.ContinueWith(_ => "done", TaskScheduler.Default);
                case Scenario.TaskWaitAsync:
                    await Workflow.DelayAsync(10000).WaitAsync(TimeSpan.FromSeconds(3));
                    return "done";
                case Scenario.DataflowReceiveAsync:
                    var block = new BufferBlock<string>();
                    await block.SendAsync("done");
                    return await block.ReceiveAsync();
                case Scenario.TaskWhenAnyWithResultThreeParam:
                    return await await Task.WhenAny(
                        Task.FromResult("done"), Task.FromResult("done"), Task.FromResult("done"));

                // Good
                case Scenario.TaskFactoryStartNew:
                    return await Task.Factory.StartNew(() =>
                    {
                        Assert.True(Workflow.InWorkflow);
                        return "done";
                    });
                case Scenario.TaskStart:
                    var taskStart = new Task<string>(() => "done");
                    taskStart.Start();
                    return await taskStart;
                case Scenario.TaskContinueWith:
                    return await Task.CompletedTask.ContinueWith(_ => "done");
                case Scenario.TaskWhenAnyWithResultTwoParam:
                    return await await Task.WhenAny(
                        Task.FromResult("done"), Task.FromResult("done"));
                case Scenario.WorkflowWhenAnyWithResultThreeParam:
                    return await await Workflow.WhenAnyAsync(
                        Task.FromResult("done"), Task.FromResult("done"), Task.FromResult("done"));
            }
            throw new InvalidOperationException("Unexpected completion");
        }

        public enum Scenario
        {
            // Bad
            TaskDelay,
            TaskRun,
            TaskFactoryStartNewDefaultScheduler,
            TaskContinueWithDefaultScheduler,
            TaskWaitAsync,
            // https://github.com/dotnet/runtime/issues/83159
            DataflowReceiveAsync,
            // https://github.com/dotnet/runtime/issues/87481
            TaskWhenAnyWithResultThreeParam,

            // Good
            TaskFactoryStartNew,
            TaskStart,
            TaskContinueWith,
            // https://github.com/dotnet/runtime/issues/87481
            TaskWhenAnyWithResultTwoParam,
            WorkflowWhenAnyWithResultThreeParam,
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_StandardLibraryCalls_FailsTaskWhenInvalid()
    {
        Task AssertScenarioFailsTask(
            StandardLibraryCallsWorkflow.Scenario scenario, string exceptionContains) =>
            ExecuteWorkerAsync<StandardLibraryCallsWorkflow>(async worker =>
            {
                // Start the workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    (StandardLibraryCallsWorkflow wf) => wf.RunAsync(scenario),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

                // Make sure task failure occurs in history eventually
                await AssertTaskFailureContainsEventuallyAsync(handle, exceptionContains);
            });

        await AssertScenarioFailsTask(
            StandardLibraryCallsWorkflow.Scenario.TaskDelay, "non-deterministic");
        await AssertScenarioFailsTask(
            StandardLibraryCallsWorkflow.Scenario.TaskRun, "not scheduled on workflow scheduler");
        await AssertScenarioFailsTask(
            StandardLibraryCallsWorkflow.Scenario.TaskFactoryStartNewDefaultScheduler,
            "not scheduled on workflow scheduler");
        await AssertScenarioFailsTask(
            StandardLibraryCallsWorkflow.Scenario.TaskContinueWithDefaultScheduler,
            "not scheduled on workflow scheduler");
        await AssertScenarioFailsTask(
            StandardLibraryCallsWorkflow.Scenario.TaskWaitAsync, "non-deterministic");
        await AssertScenarioFailsTask(
            StandardLibraryCallsWorkflow.Scenario.DataflowReceiveAsync,
            "not scheduled on workflow scheduler");
        await AssertScenarioFailsTask(
            StandardLibraryCallsWorkflow.Scenario.TaskWhenAnyWithResultThreeParam,
            "not scheduled on workflow scheduler");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_StandardLibraryCalls_SucceedWhenValid()
    {
        Task AssertScenarioSucceeds(StandardLibraryCallsWorkflow.Scenario scenario) =>
            ExecuteWorkerAsync<StandardLibraryCallsWorkflow>(async worker =>
            {
                var result = await Env.Client.ExecuteWorkflowAsync(
                    (StandardLibraryCallsWorkflow wf) => wf.RunAsync(scenario),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                    {
                        RunTimeout = TimeSpan.FromSeconds(10),
                    });
                Assert.Equal("done", result);
            });

        await AssertScenarioSucceeds(StandardLibraryCallsWorkflow.Scenario.TaskFactoryStartNew);
        await AssertScenarioSucceeds(StandardLibraryCallsWorkflow.Scenario.TaskStart);
        await AssertScenarioSucceeds(StandardLibraryCallsWorkflow.Scenario.TaskContinueWith);
        await AssertScenarioSucceeds(StandardLibraryCallsWorkflow.Scenario.TaskWhenAnyWithResultTwoParam);
        await AssertScenarioSucceeds(StandardLibraryCallsWorkflow.Scenario.WorkflowWhenAnyWithResultThreeParam);
    }

    [Workflow]
    public class InfoWorkflow
    {
        [WorkflowRun]
        public Task<WorkflowInfo> RunAsync() => Task.FromResult(Workflow.Info);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Info_Succeeds()
    {
        await ExecuteWorkerAsync<InfoWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                (InfoWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            var result = await handle.GetResultAsync();
            Assert.Equal(1, result.Attempt);
            Assert.Null(result.ContinuedRunId);
            Assert.Null(result.CronSchedule);
            Assert.Null(result.ExecutionTimeout);
            Assert.Equal(worker.Client.Options.Namespace, result.Namespace);
            Assert.Null(result.Parent);
            Assert.Null(result.RetryPolicy);
            Assert.Equal(handle.ResultRunId, result.RunId);
            Assert.Null(result.RunTimeout);
            Assert.InRange(
                result.StartTime,
                DateTime.UtcNow - TimeSpan.FromMinutes(5),
                DateTime.UtcNow + TimeSpan.FromMinutes(5));
            Assert.Equal(worker.Options.TaskQueue, result.TaskQueue);
            // TODO(cretz): Can assume default 10 in all test servers?
            Assert.Equal(TimeSpan.FromSeconds(10), result.TaskTimeout);
            Assert.Equal(handle.Id, result.WorkflowId);
            Assert.Equal("InfoWorkflow", result.WorkflowType);
        });
    }

    public record HistoryInfo(int HistoryLength, int HistorySize, bool ContinueAsNewSuggested);

    [Workflow]
    public class HistoryInfoWorkflow
    {
        // Just wait forever
        [WorkflowRun]
        public Task RunAsync() => Workflow.WaitConditionAsync(() => false);

        [WorkflowSignal]
        public async Task BunchOfEventsAsync(int count)
        {
            // Create a lot of one-day timers
            for (var i = 0; i < count; i++)
            {
                _ = Workflow.DelayAsync(TimeSpan.FromDays(1));
            }
        }

        [WorkflowQuery]
        public HistoryInfo HistoryInfo => new(
            HistoryLength: Workflow.CurrentHistoryLength,
            HistorySize: Workflow.CurrentHistorySize,
            ContinueAsNewSuggested: Workflow.ContinueAsNewSuggested);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_HistoryInfo_IsAccurate()
    {
        await ExecuteWorkerAsync<HistoryInfoWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                (HistoryInfoWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Issue query before anything else, which should mean only a history size of 3, at
            // least 100 bytes of history, and no continue as new suggestion
            var origInfo = await handle.QueryAsync(wf => wf.HistoryInfo);
            Assert.Equal(3, origInfo.HistoryLength);
            Assert.True(origInfo.HistorySize > 100);
            Assert.False(origInfo.ContinueAsNewSuggested);

            // Now send a lot of events
            await handle.SignalAsync(wf =>
                wf.BunchOfEventsAsync(WorkflowEnvironment.ContinueAsNewSuggestedHistoryCount));
            // Send one more event to trigger the WFT update. We have to do this because just a
            // query will have a stale representation of history counts, but signal forces a new
            // WFT.
            await handle.SignalAsync(wf => wf.BunchOfEventsAsync(1));
            await AssertMore.EventuallyAsync(async () =>
            {
                var eventCount = (await handle.FetchHistoryAsync()).Events.Count;
                Assert.True(
                    eventCount > WorkflowEnvironment.ContinueAsNewSuggestedHistoryCount,
                    $"We sent at least {WorkflowEnvironment.ContinueAsNewSuggestedHistoryCount} events but " +
                    $"history length said it was only {eventCount}");
            });
            var newInfo = await handle.QueryAsync(wf => wf.HistoryInfo);
            Assert.True(
                newInfo.HistoryLength > WorkflowEnvironment.ContinueAsNewSuggestedHistoryCount,
                $"We sent at least {WorkflowEnvironment.ContinueAsNewSuggestedHistoryCount} events but " +
                $"history length said it was only {newInfo.HistoryLength}");
            Assert.True(newInfo.HistorySize > origInfo.HistorySize);
            Assert.True(newInfo.ContinueAsNewSuggested);
        });
    }

    [Workflow]
    public class MultiParamWorkflow
    {
        [WorkflowRun]
        public Task<string> RunAsync(string param1, string param2) => Task.FromResult($"{param1}:{param2}");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_MultiParam_Succeeds()
    {
        await ExecuteWorkerAsync<MultiParamWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync<string>(
                "MultiParamWorkflow",
                new string[] { "foo", "bar" },
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("foo:bar", result);
        });
    }

    [Workflow]
    public class DefaultParamWorkflow
    {
        [WorkflowRun]
        public Task<string> RunAsync(string param1, string param2 = "default") =>
            Task.FromResult($"{param1}:{param2}");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_DefaultParam_Succeeds()
    {
        await ExecuteWorkerAsync<DefaultParamWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync<string>(
                "DefaultParamWorkflow",
                new string[] { "foo" },
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("foo:default", result);
        });
    }

    [Workflow]
    public class TimerWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(Input input)
        {
            using var cancelSource = new CancellationTokenSource();
            if (input.CancelBefore)
            {
                cancelSource.Cancel();
            }
            else if (input.CancelAfterMS > 0)
            {
                _ = Workflow.DelayAsync(input.CancelAfterMS).ContinueWith(_ => cancelSource.Cancel());
            }
            try
            {
                await Workflow.DelayAsync(input.DelayMS, cancelSource.Token);
                return "done";
            }
            catch (TaskCanceledException)
            {
                return "cancelled";
            }
        }

        public record Input(
            int DelayMS,
            bool CancelBefore = false,
            int CancelAfterMS = 0);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Timer_Succeeds()
    {
        await ExecuteWorkerAsync<TimerWorkflow>(async worker =>
        {
            var arg = new TimerWorkflow.Input(DelayMS: 100);
            var handle = await Env.Client.StartWorkflowAsync(
                (TimerWorkflow wf) => wf.RunAsync(arg),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("done", await handle.GetResultAsync());
            // Check history has a timer start and a timer fire
            bool foundStart = false, foundFire = false;
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                if (evt.TimerStartedEventAttributes != null)
                {
                    Assert.False(foundStart);
                    foundStart = true;
                    Assert.Equal(
                        TimeSpan.FromMilliseconds(100),
                        evt.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan());
                }
                else if (evt.TimerFiredEventAttributes != null)
                {
                    Assert.False(foundFire);
                    foundFire = true;
                }
            }
            Assert.True(foundStart);
            Assert.True(foundFire);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TimerCancelBefore_ProperlyCanceled()
    {
        await ExecuteWorkerAsync<TimerWorkflow>(async worker =>
        {
            var arg = new TimerWorkflow.Input(DelayMS: 10000, CancelBefore: true);
            var handle = await Env.Client.StartWorkflowAsync(
                (TimerWorkflow wf) => wf.RunAsync(arg),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("cancelled", await handle.GetResultAsync());
            // Make sure no timer event in history
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                Assert.Null(evt.TimerStartedEventAttributes);
            }
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TimerCancelAfter_ProperlyCanceled()
    {
        await ExecuteWorkerAsync<TimerWorkflow>(async worker =>
        {
            var arg = new TimerWorkflow.Input(DelayMS: 10000, CancelAfterMS: 100);
            var handle = await Env.Client.StartWorkflowAsync(
                (TimerWorkflow wf) => wf.RunAsync(arg),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("cancelled", await handle.GetResultAsync());
            // Make sure there is a timer cancelled in history
            var found = false;
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                found = evt.TimerCanceledEventAttributes != null;
                if (found)
                {
                    break;
                }
            }
            Assert.True(found);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TimerInfinite_NeverCreatesTask()
    {
        await ExecuteWorkerAsync<TimerWorkflow>(async worker =>
        {
            var arg = new TimerWorkflow.Input(DelayMS: Timeout.Infinite, CancelAfterMS: 100);
            var handle = await Env.Client.StartWorkflowAsync(
                (TimerWorkflow wf) => wf.RunAsync(arg),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("cancelled", await handle.GetResultAsync());
            // Only expect the one cancel timer, not the infinite one
            var timerCount = 0;
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                if (evt.TimerStartedEventAttributes != null)
                {
                    timerCount++;
                }
            }
            Assert.Equal(1, timerCount);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TimerNegative_FailsTask()
    {
        await ExecuteWorkerAsync<TimerWorkflow>(async worker =>
        {
            var arg = new TimerWorkflow.Input(DelayMS: -50);
            var handle = await Env.Client.StartWorkflowAsync(
                (TimerWorkflow wf) => wf.RunAsync(arg),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Confirm WFT failure
            await AssertTaskFailureContainsEventuallyAsync(handle, "duration cannot be less than 0");
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TimerZero_LikeOneMillisecond()
    {
        await ExecuteWorkerAsync<TimerWorkflow>(async worker =>
        {
            var arg = new TimerWorkflow.Input(DelayMS: 0);
            var handle = await Env.Client.StartWorkflowAsync(
                (TimerWorkflow wf) => wf.RunAsync(arg),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("done", await handle.GetResultAsync());
            // Make sure the timer was set to 1ms
            var found = false;
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                if (evt.TimerStartedEventAttributes != null)
                {
                    found = true;
                    Assert.Equal(
                        TimeSpan.FromMilliseconds(1),
                        evt.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan());
                }
            }
            Assert.True(found);
        });
    }

    [Workflow]
    public class CancelWorkflow
    {
        public static TaskCompletionSource? ActivityStarted { get; set; }

        private bool childWaiting;

        [Activity]
        public static async Task<string> SwallowCancelActivityAsync()
        {
            ActivityStarted!.SetResult();
            while (!ActivityExecutionContext.Current.CancellationToken.IsCancellationRequested)
            {
                ActivityExecutionContext.Current.Heartbeat();
                await Task.Delay(100);
            }
            return "cancelled";
        }

        [Workflow]
        public class SwallowCancelChildWorkflow
        {
            [WorkflowRun]
            public async Task<string> RunAsync()
            {
                try
                {
                    await Workflow.DelayAsync(Timeout.Infinite);
                    throw new InvalidOperationException("Unexpected timer success");
                }
                catch (OperationCanceledException)
                {
                    return "cancelled";
                }
            }
        }

        [WorkflowRun]
        public async Task<string> RunAsync(Scenario scenario)
        {
            switch (scenario)
            {
                case Scenario.Timer:
                    await Workflow.DelayAsync(30000);
                    break;
                case Scenario.TimerIgnoreCancel:
                    // Wait for both cancellation and timer
                    var timerTask = Workflow.DelayAsync(100, CancellationToken.None);
                    try
                    {
                        await timerTask.WaitAsync(Workflow.CancellationToken);
                        Assert.Fail("Didn't fail");
                    }
                    catch (TaskCanceledException)
                    {
                    }
                    await timerTask;
                    break;
                case Scenario.TimerWaitAndIgnore:
                    await Task.WhenAny(Workflow.DelayAsync(Timeout.Infinite, Workflow.CancellationToken));
                    break;
                case Scenario.Activity:
                    await Workflow.ExecuteActivityAsync(
                        () => SwallowCancelActivityAsync(),
                        new()
                        {
                            ScheduleToCloseTimeout = TimeSpan.FromHours(1),
                            HeartbeatTimeout = TimeSpan.FromSeconds(2),
                        });
                    break;
                case Scenario.ActivityWaitAndIgnore:
                    var actRes = await Workflow.ExecuteActivityAsync(
                        () => SwallowCancelActivityAsync(),
                        new()
                        {
                            ScheduleToCloseTimeout = TimeSpan.FromHours(1),
                            HeartbeatTimeout = TimeSpan.FromSeconds(2),
                            CancellationType = ActivityCancellationType.WaitCancellationCompleted,
                        });
                    Assert.Equal("cancelled", actRes);
                    break;
                case Scenario.LocalActivity:
                    await Workflow.ExecuteLocalActivityAsync(
                        () => SwallowCancelActivityAsync(),
                        new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                    break;
                case Scenario.ChildTryCancel:
                    var childHandle1 = await Workflow.StartChildWorkflowAsync(
                        (SwallowCancelChildWorkflow wf) => wf.RunAsync(),
                        new() { CancellationType = ChildWorkflowCancellationType.TryCancel });
                    childWaiting = true;
                    await childHandle1.GetResultAsync();
                    break;
                case Scenario.ChildWaitAndIgnore:
                    var childHandle2 = await Workflow.StartChildWorkflowAsync(
                        (SwallowCancelChildWorkflow wf) => wf.RunAsync());
                    childWaiting = true;
                    var childRes = await childHandle2.GetResultAsync();
                    Assert.Equal("cancelled", childRes);
                    break;
                default:
                    throw new NotImplementedException();
            }
            return "done";
        }

        [WorkflowQuery]
        public bool ChildWaiting() => childWaiting;

        public enum Scenario
        {
            Timer,
            TimerIgnoreCancel,
            TimerWaitAndIgnore,
            Activity,
            ActivityWaitAndIgnore,
            LocalActivity,
            ChildTryCancel,
            ChildWaitAndIgnore,
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Cancel_ProperlyCanceled()
    {
        Task AssertProperlyCanceled(
            CancelWorkflow.Scenario scenario,
            Func<WorkflowHandle<CancelWorkflow>, Task>? waitFor = null,
            Action<WorkflowHandle>? additionalAssertions = null) =>
            ExecuteWorkerAsync<CancelWorkflow>(
                async worker =>
                {
                    CancelWorkflow.ActivityStarted = new();
                    var handle = await Env.Client.StartWorkflowAsync(
                        (CancelWorkflow wf) => wf.RunAsync(scenario),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                        {
                            // Lowering for quicker LA result
                            TaskTimeout = TimeSpan.FromSeconds(2),
                        });
                    if (waitFor != null)
                    {
                        await waitFor(handle);
                    }
                    else
                    {
                        await AssertMore.StartedEventuallyAsync(handle);
                    }
                    await handle.CancelAsync();
                    var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                        () => handle.GetResultAsync());
                    Assert.IsType<CanceledFailureException>(exc.InnerException);
                    additionalAssertions?.Invoke(handle);
                },
                new TemporalWorkerOptions().
                    AddActivity(CancelWorkflow.SwallowCancelActivityAsync).
                    AddWorkflow<CancelWorkflow.SwallowCancelChildWorkflow>());

        // TODO(cretz): wait condition, external signal, etc
        await AssertProperlyCanceled(CancelWorkflow.Scenario.Timer);
        await AssertProperlyCanceled(
            CancelWorkflow.Scenario.Activity, _ => CancelWorkflow.ActivityStarted!.Task);
        await AssertProperlyCanceled(
            CancelWorkflow.Scenario.LocalActivity, _ => CancelWorkflow.ActivityStarted!.Task);
        await AssertProperlyCanceled(
            CancelWorkflow.Scenario.ChildTryCancel, handle =>
                AssertMore.EqualEventuallyAsync(
                    true,
                    () => handle.QueryAsync(wf => wf.ChildWaiting())));
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Cancel_ProperlyIgnored()
    {
        Task AssertProperlyIgnored(
            CancelWorkflow.Scenario scenario,
            Func<WorkflowHandle<CancelWorkflow>, Task>? waitFor = null,
            Action<WorkflowHandle>? additionalAssertions = null) =>
            ExecuteWorkerAsync<CancelWorkflow>(
                async worker =>
                {
                    CancelWorkflow.ActivityStarted = new();
                    var handle = await Env.Client.StartWorkflowAsync(
                        (CancelWorkflow wf) => wf.RunAsync(scenario),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                    if (waitFor != null)
                    {
                        await waitFor(handle);
                    }
                    else
                    {
                        await AssertMore.StartedEventuallyAsync(handle);
                    }
                    await handle.CancelAsync();
                    Assert.Equal("done", await handle.GetResultAsync());
                    additionalAssertions?.Invoke(handle);
                },
                new TemporalWorkerOptions().
                    AddActivity(CancelWorkflow.SwallowCancelActivityAsync).
                    AddWorkflow<CancelWorkflow.SwallowCancelChildWorkflow>());

        // TODO(cretz): Test external signal, etc
        await AssertProperlyIgnored(CancelWorkflow.Scenario.TimerIgnoreCancel);
        await AssertProperlyIgnored(CancelWorkflow.Scenario.TimerWaitAndIgnore);
        await AssertProperlyIgnored(
            CancelWorkflow.Scenario.ActivityWaitAndIgnore,
            _ => CancelWorkflow.ActivityStarted!.Task);
        await AssertProperlyIgnored(
            CancelWorkflow.Scenario.ChildWaitAndIgnore, handle =>
                AssertMore.EqualEventuallyAsync(
                    true,
                    () => handle.QueryAsync(wf => wf.ChildWaiting())));
    }

    [Workflow]
    public class AssertWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync() => Assert.Fail("Oh no");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Assert_FailsWorkflow()
    {
        await ExecuteWorkerAsync<AssertWorkflow>(async worker =>
        {
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                Env.Client.ExecuteWorkflowAsync(
                    (AssertWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
            var exc2 = Assert.IsType<ApplicationFailureException>(exc.InnerException);
            Assert.Equal("Assertion failed", exc2.Message);
            var exc3 = Assert.IsType<ApplicationFailureException>(exc2.InnerException);
            Assert.Contains("Oh no", exc3.Message);
        });
    }

    [Workflow]
    public class SignalWorkflow
    {
        private readonly List<string> events = new();

        // Just wait forever
        [WorkflowRun]
        public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);

        [WorkflowSignal]
        public async Task Signal1Async(string arg) => events.Add($"Signal1: {arg}");

        [WorkflowSignal("custom-name")]
        public async Task SignalCustomAsync(string arg) => events.Add($"SignalCustom: {arg}");

        [WorkflowSignal]
        public async Task AddSignalHandlersAsync(IReadOnlyCollection<string> names)
        {
            foreach (var name in names)
            {
                async Task HandleSignalAsync(string arg) =>
                    events.Add($"AddSignalHandlers: {name} - {arg}");
                Workflow.Signals[name] = WorkflowSignalDefinition.CreateWithoutAttribute(
                    name, HandleSignalAsync);
            }
        }

        [WorkflowSignal]
        public async Task FailWorkflowAsync() => throw new ApplicationFailureException("Oh no");

        [WorkflowQuery]
        public IList<string> Events() => events;
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Signals_ProperlyHandled()
    {
        await ExecuteWorkerAsync<SignalWorkflow>(async worker =>
        {
            // Start the workflow with a signal
            var handle = await Env.Client.StartWorkflowAsync(
                (SignalWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                {
                    StartSignal = "Signal1",
                    StartSignalArgs = new[] { "signalval1" },
                });
            // Add one w/ custom name
            await handle.SignalAsync(wf => wf.SignalCustomAsync("signalval2"));
            // Add a couple more to be buffered
            await handle.SignalAsync("latesig1", new[] { "signalval3" });
            await handle.SignalAsync("latesig2", new[] { "signalval4" });
            // Add the signal handlers
            await handle.SignalAsync(
                wf => wf.AddSignalHandlersAsync(new[] { "latesig1", "latesig2", "latesig3" }));
            // Add another to a late-bound signal
            await handle.SignalAsync("latesig3", new[] { "signalval5" });
            // Confirm all events are as expected
            Assert.Equal(
                new List<string>
                {
                    "Signal1: signalval1",
                    "SignalCustom: signalval2",
                    "AddSignalHandlers: latesig1 - signalval3",
                    "AddSignalHandlers: latesig2 - signalval4",
                    "AddSignalHandlers: latesig3 - signalval5",
                },
                await handle.QueryAsync(wf => wf.Events()));

            // Now send a signal that fails the entire workflow
            await handle.SignalAsync(wf => wf.FailWorkflowAsync());
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => handle.GetResultAsync());
            var exc2 = Assert.IsType<ApplicationFailureException>(exc.InnerException);
            Assert.Equal("Oh no", exc2.Message);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Signals_SignalWithStart()
    {
        await ExecuteWorkerAsync<SignalWorkflow>(async worker =>
        {
            // Start the workflow with a signal
            var options = new WorkflowOptions(
                id: $"workflow-{Guid.NewGuid()}",
                taskQueue: worker.Options.TaskQueue!);
            options.SignalWithStart((SignalWorkflow wf) => wf.Signal1Async("signalval1"));
            var handle = await Env.Client.StartWorkflowAsync(
                (SignalWorkflow wf) => wf.RunAsync(),
                options);
            // Confirm signal received
            Assert.Equal(
                new List<string>
                {
                    "Signal1: signalval1",
                },
                await handle.QueryAsync(wf => wf.Events()));
            // Do it again, confirm signal received on same workflow
            options.SignalWithStart((SignalWorkflow wf) => wf.Signal1Async("signalval2"));
            var newHandle = await Env.Client.StartWorkflowAsync(
                (SignalWorkflow wf) => wf.RunAsync(),
                options);
            Assert.Equal(handle.ResultRunId, newHandle.ResultRunId);
            // Confirm signal received
            Assert.Equal(
                new List<string>
                {
                    "Signal1: signalval1",
                    "Signal1: signalval2",
                },
                await handle.QueryAsync(wf => wf.Events()));
        });
    }

    [Workflow]
    public class BadSignalArgsDroppedWorkflow
    {
        [WorkflowRun]
        public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);

        [WorkflowSignal]
        public async Task SomeSignalAsync(string arg) => SignalArgs.Add(arg);

        [WorkflowQuery]
        public IList<string> SignalArgs { get; } = new List<string>();
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_BadSignalArgs_ProperlyDropped()
    {
        await ExecuteWorkerAsync<BadSignalArgsDroppedWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                (BadSignalArgsDroppedWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Send 4 signals, the first and third being bad
            await handle.SignalAsync("SomeSignal", new object?[] { 123 });
            await handle.SignalAsync("SomeSignal", new object?[] { "value1" });
            await handle.SignalAsync("SomeSignal", new object?[] { false });
            await handle.SignalAsync("SomeSignal", new object?[] { "value2" });
            Assert.Equal(
                new List<string> { "value1", "value2" },
                await handle.QueryAsync(wf => wf.SignalArgs));
        });
    }

    [Workflow]
    public class QueryWorkflow
    {
        // Just wait forever
        [WorkflowRun]
        public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);

        [WorkflowQuery]
        public string QuerySimple(string arg) => $"QuerySimple: {arg}";

        [WorkflowQuery("custom-name")]
        public string QueryCustom(string arg) => $"QueryCustom: {arg}";

        [WorkflowQuery]
        public string QueryProperty => "QueryProperty";

        [WorkflowQuery]
        public string QueryNoArg() => "QueryNoArg";

        [WorkflowSignal]
        public async Task AddQueryHandlerAsync(string name) =>
            Workflow.Queries[name] = WorkflowQueryDefinition.CreateWithoutAttribute(
                name, (string arg) => $"AddQueryHandler: {name} - {arg}");

        [WorkflowQuery]
        public string QueryFail() => throw new InvalidOperationException("Query fail");

        [WorkflowQuery]
        public string QueryMakingCommands()
        {
            _ = Workflow.DelayAsync(5000);
            return "unreachable";
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Queries_ProperlyHandled()
    {
        await ExecuteWorkerAsync<QueryWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (QueryWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Basic queries
            Assert.Equal(
                "QuerySimple: foo",
                await handle.QueryAsync(wf => wf.QuerySimple("foo")));
            Assert.Equal(
                "QueryCustom: bar",
                await handle.QueryAsync(wf => wf.QueryCustom("bar")));
            Assert.Equal(
                "QueryProperty",
                await handle.QueryAsync(wf => wf.QueryProperty));
            // Non-existent query
            var exc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                () => handle.QueryAsync<string>("some-query", new[] { "some-arg" }));
            Assert.Contains(
                "known queries: [custom-name QueryFail QueryMakingCommands QueryNoArg QueryProperty QuerySimple]",
                exc.Message);
            // Add that non-existent query then try again
            await handle.SignalAsync(wf => wf.AddQueryHandlerAsync("some-query"));
            Assert.Equal(
                "AddQueryHandler: some-query - some-arg",
                await handle.QueryAsync<string>("some-query", new[] { "some-arg" }));
            // Query fail
            var exc2 = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                () => handle.QueryAsync(wf => wf.QueryFail()));
            Assert.Equal("Query fail", exc2.Message);
            // Make commands in a query
            var exc3 = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                () => handle.QueryAsync(wf => wf.QueryMakingCommands()));
            Assert.Contains("created workflow commands", exc3.Message);
            // Access method as property
            var exc4 = await Assert.ThrowsAsync<ArgumentException>(
                () => handle.QueryAsync<Func<string>>(wf => wf.QueryNoArg));
            Assert.Contains("must be a single method call or property access", exc4.Message);
        });
    }

    [Workflow]
    public class PropertyWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync(string value) => Value = value;

        [WorkflowQuery]
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Properties_ProperlySupported()
    {
        await ExecuteWorkerAsync<PropertyWorkflow>(async worker =>
        {
            // Run the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (PropertyWorkflow wf) => wf.RunAsync("some string"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await handle.GetResultAsync();
            Assert.Equal("some string", await handle.QueryAsync(wf => wf.Value));
        });
    }

    [Workflow]
    public class MiscHelpersWorkflow
    {
        private static readonly List<bool> EventsForIsReplaying = new();

        private string? completeWorkflow;

        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            Workflow.Logger.LogInformation("Some log {Foo}", "Bar");
            EventsForIsReplaying.Add(Workflow.Unsafe.IsReplaying);
            await Workflow.WaitConditionAsync(() => completeWorkflow != null);
            return completeWorkflow!;
        }

        [WorkflowSignal]
        public async Task CompleteWorkflow(string val) => completeWorkflow = val;

        [WorkflowQuery]
        public DateTime CurrentTime() => Workflow.UtcNow;

        [WorkflowQuery]
        public int Random(int max) => Workflow.Random.Next(max);

        [WorkflowQuery]
        public string NewGuid() => Workflow.NewGuid().ToString();

        [WorkflowQuery]
        public bool[] GetEventsForIsReplaying() => EventsForIsReplaying.ToArray();
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_MiscHelpers_Succeed()
    {
        // Run one worker doing test
        var workflowId = string.Empty;
        var taskQueue = string.Empty;
        var loggerFactory = new TestUtils.LogCaptureFactory(LoggerFactory);
        await ExecuteWorkerAsync<MiscHelpersWorkflow>(
            async worker =>
            {
                // Start the workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    (MiscHelpersWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                workflowId = handle.Id;
                taskQueue = worker.Options.TaskQueue!;
                Assert.InRange(
                    await handle.QueryAsync(wf => wf.CurrentTime()),
                    DateTime.UtcNow - TimeSpan.FromMinutes(5),
                    DateTime.UtcNow + TimeSpan.FromMinutes(5));
                Assert.InRange(await handle.QueryAsync(wf => wf.Random(3)), 0, 2);
                // Check GUID is parseable and shows 4 as UUID version
                var guid = await handle.QueryAsync(wf => wf.NewGuid());
                Assert.True(Guid.TryParseExact(guid, "D", out _));
                Assert.Equal('4', guid[14]);
                // Mark workflow complete and wait on result
                await handle.SignalAsync(wf => wf.CompleteWorkflow("done!"));
                Assert.Equal("done!", await handle.GetResultAsync());
                // Confirm log is present
                Assert.Contains(loggerFactory.Logs, entry => entry.Formatted == "Some log Bar");
                // Now clear and issue query and confirm log is not present
                loggerFactory.ClearLogs();
                Assert.InRange(await handle.QueryAsync(wf => wf.Random(3)), 0, 2);
                Assert.DoesNotContain(
                    loggerFactory.Logs, entry => entry.Formatted == "Some log Bar");
            },
            new()
            {
                LoggerFactory = loggerFactory,
            });

        // Run the worker again with a query so we can force replay
        await ExecuteWorkerAsync<MiscHelpersWorkflow>(
            async worker =>
            {
                // Now query after close to check that is replaying worked
                var isReplayingValues = await Env.Client.GetWorkflowHandle<MiscHelpersWorkflow>(
                    workflowId).QueryAsync(wf => wf.GetEventsForIsReplaying());
                Assert.Equal(new[] { false, true }, isReplayingValues);
            },
            new(taskQueue: taskQueue));
    }

    [Workflow]
    public class WaitConditionWorkflow
    {
        private readonly CancellationTokenSource cancelSource;
        private bool shouldProceed;

        public WaitConditionWorkflow() =>
            cancelSource = CancellationTokenSource.CreateLinkedTokenSource(Workflow.CancellationToken);

        [WorkflowRun]
        public Task<bool> RunAsync(int timeoutMs) =>
            Workflow.WaitConditionAsync(() => shouldProceed, timeoutMs, cancelSource.Token);

        [WorkflowSignal]
        public async Task ProceedAsync() => shouldProceed = true;

        [WorkflowSignal]
        public async Task CancelWaitAsync() => cancelSource.Cancel();
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WaitCondition_Succeeds()
    {
        await ExecuteWorkerAsync<WaitConditionWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (WaitConditionWorkflow wf) => wf.RunAsync(Timeout.Infinite),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Tell to proceed and confirm it does
            await handle.SignalAsync(wf => wf.ProceedAsync());
            Assert.True(await handle.GetResultAsync());
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WaitConditionManualCancel_ProperlyCancels()
    {
        await ExecuteWorkerAsync<WaitConditionWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (WaitConditionWorkflow wf) => wf.RunAsync(Timeout.Infinite),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Cancel and confirm it does
            await handle.SignalAsync(wf => wf.CancelWaitAsync());
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => handle.GetResultAsync());
            var exc2 = Assert.IsType<ApplicationFailureException>(exc.InnerException);
            Assert.Contains("canceled", exc2.Message);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WaitConditionWorkflowCancel_ProperlyCancels()
    {
        await ExecuteWorkerAsync<WaitConditionWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (WaitConditionWorkflow wf) => wf.RunAsync(Timeout.Infinite),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Confirm query says it's running
            await AssertMore.StartedEventuallyAsync(handle);
            // Cancel workflow and confirm it does
            await handle.CancelAsync();
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => handle.GetResultAsync());
            Assert.IsType<CanceledFailureException>(exc.InnerException);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WaitConditionTimeout_ProperlyTimesOut()
    {
        await ExecuteWorkerAsync<WaitConditionWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (WaitConditionWorkflow wf) => wf.RunAsync(10),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Wait for timeout
            Assert.False(await handle.GetResultAsync());
        });
    }

    [Workflow]
    public class DeadlockWorkflow
    {
        [WorkflowRun]
#pragma warning disable CA1849 // We are using Thread.Sleep on purpose
        public async Task RunAsync() => Thread.Sleep(4000);
#pragma warning restore CA1849
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Deadlock_ProperlyFails()
    {
        await ExecuteWorkerAsync<DeadlockWorkflow>(
            async worker =>
            {
                // Start the workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    (DeadlockWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await AssertTaskFailureContainsEventuallyAsync(handle, "deadlocked");
            },
            // Disable task tracing so we can add delay in there
            new() { DisableWorkflowTracingEventListener = true });
    }

    [Workflow]
    public class SearchAttributesWorkflow
    {
        public static readonly SearchAttributeCollection AttributesInitial = new SearchAttributeCollection.Builder().
            Set(AttrBool, true).
            Set(AttrDateTime, new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)).
            Set(AttrDouble, 123.45).
            Set(AttrKeyword, "SomeKeyword").
            // TODO(cretz): Fix after Temporal dev server upgraded
            // Set(AttrKeywordList, new[] { "SomeKeyword1", "SomeKeyword2" }).
            Set(AttrLong, 678).
            Set(AttrText, "SomeText").
            ToSearchAttributeCollection();

        // Update half, remove half
        public static readonly SearchAttributeUpdate[] AttributeFirstUpdates = new SearchAttributeUpdate[]
        {
            AttrBool.ValueSet(false),
            AttrDateTime.ValueSet(new DateTimeOffset(2002, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            AttrDouble.ValueSet(234.56),
            AttrKeyword.ValueUnset(),
            AttrLong.ValueUnset(),
            AttrText.ValueUnset(),
        };

        public static readonly SearchAttributeCollection AttributesFirstUpdated = new SearchAttributeCollection.Builder().
            Set(AttrBool, false).
            Set(AttrDateTime, new DateTimeOffset(2002, 1, 1, 0, 0, 0, TimeSpan.Zero)).
            Set(AttrDouble, 234.56).
            ToSearchAttributeCollection();

        // Update/remove other half
        public static readonly SearchAttributeUpdate[] AttributeSecondUpdates = new SearchAttributeUpdate[]
        {
            AttrBool.ValueUnset(),
            AttrDateTime.ValueUnset(),
            AttrDouble.ValueUnset(),
            AttrKeyword.ValueSet("AnotherKeyword"),
            AttrLong.ValueSet(789),
            AttrText.ValueSet("SomeOtherText"),
        };

        public static readonly SearchAttributeCollection AttributesSecondUpdated = new SearchAttributeCollection.Builder().
            Set(AttrKeyword, "AnotherKeyword").
            Set(AttrLong, 789).
            Set(AttrText, "SomeOtherText").
            ToSearchAttributeCollection();

        public static void AssertAttributesEqual(
            SearchAttributeCollection expected, SearchAttributeCollection actual) =>
            Assert.Equal(
                expected.UntypedValues.Where(kvp => kvp.Key.Name.StartsWith("DotNet")),
                actual.UntypedValues.Where(kvp => kvp.Key.Name.StartsWith("DotNet")));

        private bool proceed;

        [WorkflowRun]
        public async Task RunAsync()
        {
            // Check initial
            AssertAttributesEqual(AttributesInitial, Workflow.TypedSearchAttributes);
            // Wait, then update and check
            await Workflow.WaitConditionAsync(() => proceed);
            proceed = false;
            Workflow.UpsertTypedSearchAttributes(AttributeFirstUpdates);
            AssertAttributesEqual(AttributesFirstUpdated, Workflow.TypedSearchAttributes);
            // Wait, then update and check
            await Workflow.WaitConditionAsync(() => proceed);
            proceed = false;
            Workflow.UpsertTypedSearchAttributes(AttributeSecondUpdates);
            AssertAttributesEqual(AttributesSecondUpdated, Workflow.TypedSearchAttributes);
        }

        [WorkflowSignal]
        public async Task ProceedAsync() => proceed = true;
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_SearchAttributes_ProperlyUpserted()
    {
        await EnsureSearchAttributesPresentAsync();
        await ExecuteWorkerAsync<SearchAttributesWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (SearchAttributesWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                {
                    TypedSearchAttributes = SearchAttributesWorkflow.AttributesInitial,
                });
            // Confirm description shows initial
            SearchAttributesWorkflow.AssertAttributesEqual(
                SearchAttributesWorkflow.AttributesInitial,
                (await handle.DescribeAsync()).TypedSearchAttributes);
            // Tell workflow to proceed and confirm next values
            await handle.SignalAsync(wf => wf.ProceedAsync());
            await AssertMore.EventuallyAsync(async () =>
                SearchAttributesWorkflow.AssertAttributesEqual(
                    SearchAttributesWorkflow.AttributesFirstUpdated,
                    (await handle.DescribeAsync()).TypedSearchAttributes));
            // Tell workflow to proceed and confirm next values
            await handle.SignalAsync(wf => wf.ProceedAsync());
            await AssertMore.EventuallyAsync(async () =>
                SearchAttributesWorkflow.AssertAttributesEqual(
                    SearchAttributesWorkflow.AttributesSecondUpdated,
                    (await handle.DescribeAsync()).TypedSearchAttributes));
        });
    }

    [Workflow]
    public class MemoWorkflow
    {
        public static readonly Dictionary<string, object> MemoInitial = new()
        {
            ["foo"] = "fooval",
            ["bar"] = "barval",
        };

        public static readonly MemoUpdate[] MemoUpdates = new[]
        {
            MemoUpdate.ValueSet("foo", "newfooval"),
            MemoUpdate.ValueUnset("bar"),
            MemoUpdate.ValueSet("baz", "bazval"),
        };

        public static readonly Dictionary<string, object> MemoUpdated = new()
        {
            ["foo"] = "newfooval",
            ["baz"] = "bazval",
        };

        private bool proceed;

        [WorkflowRun]
        public async Task RunAsync()
        {
            // Check initial
            Assert.Equal(MemoInitial, Workflow.Memo.ToDictionary(
                kvp => kvp.Key,
                kvp => (object)Workflow.PayloadConverter.ToValue<string>(kvp.Value)));
            // Wait, then update and check
            await Workflow.WaitConditionAsync(() => proceed);
            Workflow.UpsertMemo(MemoUpdates);
            Assert.Equal(MemoUpdated, Workflow.Memo.ToDictionary(
                kvp => kvp.Key,
                kvp => (object)Workflow.PayloadConverter.ToValue<string>(kvp.Value)));
        }

        [WorkflowSignal]
        public async Task ProceedAsync() => proceed = true;
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Memo_ProperlyUpserted()
    {
        await ExecuteWorkerAsync<MemoWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (MemoWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                {
                    Memo = MemoWorkflow.MemoInitial,
                });
            // Confirm description shows initial
            async Task<Dictionary<string, object>> GetCurrentMemoAsync()
            {
                var desc = await handle.DescribeAsync();
                var dict = new Dictionary<string, object>(desc.Memo.Count);
                foreach (var kvp in desc.Memo)
                {
                    dict[kvp.Key] = await kvp.Value.ToValueAsync<string>();
                }
                return dict;
            }
            Assert.Equal(MemoWorkflow.MemoInitial, await GetCurrentMemoAsync());
            // Tell workflow to proceed and confirm next values
            await handle.SignalAsync(wf => wf.ProceedAsync());
            await AssertMore.EventuallyAsync(async () =>
                Assert.Equal(MemoWorkflow.MemoUpdated, await GetCurrentMemoAsync()));
        });
    }

    [Workflow]
    public class ContinueAsNewWorkflow
    {
        [WorkflowRun]
        public async Task<IList<string>> RunAsync(IList<string> pastRunIds)
        {
            // Check memo and retry policy
            Assert.Equal(
                pastRunIds.Count,
                Workflow.PayloadConverter.ToValue<int>(Workflow.Memo["PastRunIdCount"]));
            Assert.Equal(pastRunIds.Count + 1000, Workflow.Info.RetryPolicy?.MaximumAttempts);

            if (pastRunIds.Count == 5)
            {
                return pastRunIds;
            }
            if (Workflow.Info.ContinuedRunId is string contRunId)
            {
                pastRunIds.Add(contRunId);
            }
            throw Workflow.CreateContinueAsNewException(
                (ContinueAsNewWorkflow wf) => wf.RunAsync(pastRunIds),
                new()
                {
                    Memo = new Dictionary<string, object>
                    {
                        ["PastRunIdCount"] = pastRunIds.Count,
                    },
                    RetryPolicy = new() { MaximumAttempts = pastRunIds.Count + 1000 },
                });
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_ContinueAsNew_ProperlyContinues()
    {
        await ExecuteWorkerAsync<ContinueAsNewWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                (ContinueAsNewWorkflow wf) => wf.RunAsync(new List<string>()),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                {
                    Memo = new Dictionary<string, object> { ["PastRunIdCount"] = 0 },
                    RetryPolicy = new() { MaximumAttempts = 1000 },
                });
            var result = await handle.GetResultAsync();
            Assert.Equal(5, result.Count);
            Assert.Equal(handle.FirstExecutionRunId, result[0]);
        });
    }

    [Workflow]
    public class SimpleActivityWorkflow
    {
        [Activity]
        public static string ResultNoArgSync() => "1";

        [Activity]
        public static string ResultWithArgSync(string arg)
        {
            Assert.Equal("2", arg);
            return "3";
        }

        [Activity]
        public static void NoResultNoArgSync()
        {
        }

        [Activity]
        public static void NoResultWithArgSync(string arg) => Assert.Equal("4", arg);

        [Activity]
        public static async Task<string> ResultNoArgAsync() => "5";

        [Activity]
        public static async Task<string> ResultWithArgAsync(string arg)
        {
            Assert.Equal("6", arg);
            return "7";
        }

        [Activity]
        public static Task NoResultNoArgAsync() => Task.CompletedTask;

        [Activity]
        public static async Task NoResultWithArgAsync(string arg) => Assert.Equal("8", arg);

        [Activity]
        public static string ResultMultiArgSync(string arg1, string arg2)
        {
            Assert.Equal("9", arg1);
            Assert.Equal("10", arg2);
            return "11";
        }

        [Activity]
        public static void NoResultMultiArgSync(string arg1, string arg2)
        {
            Assert.Equal("12", arg1);
            Assert.Equal("13", arg2);
        }

        [WorkflowRun]
        public async Task RunAsync(bool local)
        {
#pragma warning disable SA1118 // Don't want so many lines
            // Intentionally not making options var because I want to confirm new() { ... } works
            if (local)
            {
                // Static
                Assert.Equal("1", await Workflow.ExecuteLocalActivityAsync(
                    () => ResultNoArgSync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                Assert.Equal("3", await Workflow.ExecuteLocalActivityAsync(
                    () => ResultWithArgSync("2"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteLocalActivityAsync(
                    () => NoResultNoArgSync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.ExecuteLocalActivityAsync(
                    () => NoResultWithArgSync("4"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                Assert.Equal("5", await Workflow.ExecuteLocalActivityAsync(
                    () => ResultNoArgAsync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                Assert.Equal("7", await Workflow.ExecuteLocalActivityAsync(
                    () => ResultWithArgAsync("6"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteLocalActivityAsync(
                    () => NoResultNoArgAsync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.ExecuteLocalActivityAsync(
                    () => NoResultWithArgAsync("8"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                Assert.Equal("11", await Workflow.ExecuteLocalActivityAsync<string>(
                    ActivityDefinition.Create(ResultMultiArgSync).Name!,
                    new object?[] { "9", "10" },
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteLocalActivityAsync(
                    ActivityDefinition.Create(NoResultMultiArgSync).Name!,
                    new object?[] { "12", "13" },
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });

                // Instance
                Assert.Equal("1", await Workflow.ExecuteLocalActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceResultNoArgSync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                Assert.Equal("3", await Workflow.ExecuteLocalActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceResultWithArgSync("2"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteLocalActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceNoResultNoArgSync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.ExecuteLocalActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceNoResultWithArgSync("4"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                Assert.Equal("5", await Workflow.ExecuteLocalActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceResultNoArgAsync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                Assert.Equal("7", await Workflow.ExecuteLocalActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceResultWithArgAsync("6"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteLocalActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceNoResultNoArgAsync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.ExecuteLocalActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceNoResultWithArgAsync("8"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
            }
            else
            {
                // Static
                Assert.Equal("1", await Workflow.ExecuteActivityAsync(
                    () => ResultNoArgSync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                Assert.Equal("3", await Workflow.ExecuteActivityAsync(
                    () => ResultWithArgSync("2"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteActivityAsync(
                    () => NoResultNoArgSync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.ExecuteActivityAsync(
                    () => NoResultWithArgSync("4"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                Assert.Equal("5", await Workflow.ExecuteActivityAsync(
                    () => ResultNoArgAsync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                Assert.Equal("7", await Workflow.ExecuteActivityAsync(
                    () => ResultWithArgAsync("6"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteActivityAsync(
                    () => NoResultNoArgAsync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.ExecuteActivityAsync(
                    () => NoResultWithArgAsync("8"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                Assert.Equal("11", await Workflow.ExecuteActivityAsync<string>(
                    ActivityDefinition.Create(ResultMultiArgSync).Name!,
                    new object?[] { "9", "10" },
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteActivityAsync(
                    ActivityDefinition.Create(NoResultMultiArgSync).Name!,
                    new object?[] { "12", "13" },
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });

                // Instance
                Assert.Equal("1", await Workflow.ExecuteActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceResultNoArgSync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                Assert.Equal("3", await Workflow.ExecuteActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceResultWithArgSync("2"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceNoResultNoArgSync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.ExecuteActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceNoResultWithArgSync("4"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                Assert.Equal("5", await Workflow.ExecuteActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceResultNoArgAsync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                Assert.Equal("7", await Workflow.ExecuteActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceResultWithArgAsync("6"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceNoResultNoArgAsync(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.ExecuteActivityAsync(
                    (SimpleActivityInstance act) => act.InstanceNoResultWithArgAsync("8"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
            }
#pragma warning restore SA1118
        }
    }

    public class SimpleActivityInstance
    {
        [Activity]
        public string InstanceResultNoArgSync() => "1";

        [Activity]
        public string InstanceResultWithArgSync(string arg)
        {
            Assert.Equal("2", arg);
            return "3";
        }

        [Activity]
        public void InstanceNoResultNoArgSync()
        {
        }

        [Activity]
        public void InstanceNoResultWithArgSync(string arg) => Assert.Equal("4", arg);

        [Activity]
        public async Task<string> InstanceResultNoArgAsync() => "5";

        [Activity]
        public async Task<string> InstanceResultWithArgAsync(string arg)
        {
            Assert.Equal("6", arg);
            return "7";
        }

        [Activity]
        public Task InstanceNoResultNoArgAsync() => Task.CompletedTask;

        [Activity]
        public async Task InstanceNoResultWithArgAsync(string arg) => Assert.Equal("8", arg);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteWorkflowAsync_SimpleActivity_ExecutesProperly(bool local)
    {
        var act = new SimpleActivityInstance();
        await ExecuteWorkerAsync<SimpleActivityWorkflow>(
            async worker =>
            {
                var handle = await Env.Client.StartWorkflowAsync(
                    (SimpleActivityWorkflow wf) => wf.RunAsync(local),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await handle.GetResultAsync();
                var activities = new List<string>();
                await foreach (var evt in handle.FetchHistoryEventsAsync())
                {
                    if (local)
                    {
                        var attr = evt.MarkerRecordedEventAttributes;
                        if (attr != null)
                        {
                            Assert.Equal("core_local_activity", attr.MarkerName);
                            var data = await DataConverter.Default.ToValueAsync<System.Text.Json.JsonElement>(
                                attr.Details["data"].Payloads_.Single());
                            activities.Add(data.GetProperty("activity_type").GetString()!);
                        }
                    }
                    else
                    {
                        var attr = evt.ActivityTaskScheduledEventAttributes;
                        if (attr != null)
                        {
                            activities.Add(attr.ActivityType.Name);
                        }
                    }
                }
                Assert.Equal(
                    new List<string>()
                    {
                        "ResultNoArgSync",
                        "ResultWithArgSync",
                        "NoResultNoArgSync",
                        "NoResultWithArgSync",
                        "ResultNoArg",
                        "ResultWithArg",
                        "NoResultNoArg",
                        "NoResultWithArg",
                        "ResultMultiArgSync",
                        "NoResultMultiArgSync",
                        "InstanceResultNoArgSync",
                        "InstanceResultWithArgSync",
                        "InstanceNoResultNoArgSync",
                        "InstanceNoResultWithArgSync",
                        "InstanceResultNoArg",
                        "InstanceResultWithArg",
                        "InstanceNoResultNoArg",
                        "InstanceNoResultWithArg",
                    },
                    activities);
            },
            new TemporalWorkerOptions().
                AddActivity(SimpleActivityWorkflow.ResultNoArgSync).
                AddActivity(SimpleActivityWorkflow.ResultWithArgSync).
                AddActivity(SimpleActivityWorkflow.NoResultNoArgSync).
                AddActivity(SimpleActivityWorkflow.NoResultWithArgSync).
                AddActivity(SimpleActivityWorkflow.ResultNoArgAsync).
                AddActivity(SimpleActivityWorkflow.ResultWithArgAsync).
                AddActivity(SimpleActivityWorkflow.NoResultNoArgAsync).
                AddActivity(SimpleActivityWorkflow.NoResultWithArgAsync).
                AddActivity(SimpleActivityWorkflow.ResultMultiArgSync).
                AddActivity(SimpleActivityWorkflow.NoResultMultiArgSync).
                AddActivity(act.InstanceResultNoArgSync).
                AddActivity(act.InstanceResultWithArgSync).
                AddActivity(act.InstanceNoResultNoArgSync).
                AddActivity(act.InstanceNoResultWithArgSync).
                AddActivity(act.InstanceResultNoArgAsync).
                AddActivity(act.InstanceResultWithArgAsync).
                AddActivity(act.InstanceNoResultNoArgAsync).
                AddActivity(act.InstanceNoResultWithArgAsync));
    }

    [Workflow]
    public class TimeoutActivityWorkflow
    {
        [Activity]
        public static Task RunUntilCanceledAsync() =>
            Task.Delay(Timeout.Infinite, ActivityExecutionContext.Current.CancellationToken);

        [WorkflowRun]
        public async Task RunAsync(bool local)
        {
            // Timeout after 10ms w/ no retry
            if (local)
            {
                await Workflow.ExecuteLocalActivityAsync(
                    () => RunUntilCanceledAsync(),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromMilliseconds(10),
                        RetryPolicy = new() { MaximumAttempts = 1 },
                    });
            }
            else
            {
                await Workflow.ExecuteActivityAsync(
                    () => RunUntilCanceledAsync(),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromMilliseconds(10),
                        RetryPolicy = new() { MaximumAttempts = 1 },
                    });
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteWorkflowAsync_TimeoutActivity_TimesOut(bool local)
    {
        await ExecuteWorkerAsync<TimeoutActivityWorkflow>(
            async worker =>
            {
                var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        (TimeoutActivityWorkflow wf) => wf.RunAsync(local),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
                var actExc = Assert.IsType<ActivityFailureException>(wfExc.InnerException);
                var toExc = Assert.IsType<TimeoutFailureException>(actExc.InnerException);
                Assert.Equal(TimeoutType.StartToClose, toExc.TimeoutType);
            },
            new TemporalWorkerOptions().AddActivity(TimeoutActivityWorkflow.RunUntilCanceledAsync));
    }

    [Workflow]
    public class CancelActivityWorkflow
    {
        [Activity]
        public static Task RunUntilCanceledAsync() =>
            Task.Delay(Timeout.Infinite, ActivityExecutionContext.Current.CancellationToken);

        [WorkflowRun]
        public async Task RunAsync(Input input)
        {
            using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                Workflow.CancellationToken);
            // Cancel before if desired
            if (input.BeforeStart)
            {
                tokenSource.Cancel();
            }
            Task activity;
            if (input.Local)
            {
                activity = Workflow.ExecuteLocalActivityAsync(
                    () => RunUntilCanceledAsync(),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromMinutes(10),
                        RetryPolicy = new() { MaximumAttempts = 1 },
                        CancellationToken = tokenSource.Token,
                    });
            }
            else
            {
                activity = Workflow.ExecuteActivityAsync(
                    () => RunUntilCanceledAsync(),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromMinutes(10),
                        RetryPolicy = new() { MaximumAttempts = 1 },
                        CancellationToken = tokenSource.Token,
                    });
            }
            // If not cancel before, wait one ms (i.e. roll the task over), then cancel
            if (!input.BeforeStart)
            {
                await Workflow.DelayAsync(1);
                tokenSource.Cancel();
            }
            await activity;
        }

        public record Input(bool Local, bool BeforeStart);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteWorkflowAsync_CancelActivity_Cancels(bool local)
    {
        await ExecuteWorkerAsync<CancelActivityWorkflow>(
            async worker =>
            {
                // Regular cancel after start
                var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        (CancelActivityWorkflow wf) => wf.RunAsync(new(local, false)),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                        {
                            // Lowering for quicker LA result
                            TaskTimeout = TimeSpan.FromSeconds(2),
                        }));
                var actExc = Assert.IsType<ActivityFailureException>(wfExc.InnerException);
                Assert.IsType<CanceledFailureException>(actExc.InnerException);

                // Cancel before start
                wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        (CancelActivityWorkflow wf) => wf.RunAsync(new(local, true)),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                        {
                            // Lowering for quicker LA result
                            TaskTimeout = TimeSpan.FromSeconds(2),
                        }));
                var cancelExc = Assert.IsType<CanceledFailureException>(wfExc.InnerException);
                Assert.Contains("cancelled before scheduled", cancelExc.Message);
            },
            new TemporalWorkerOptions().AddActivity(CancelActivityWorkflow.RunUntilCanceledAsync));
    }

    [Workflow]
    public class SimpleChildWorkflow
    {
        [Workflow]
        public class ResultNoArg
        {
            [WorkflowRun]
            public async Task<string> RunAsync() => "1";
        }

        [Workflow]
        public class ResultWithArg
        {
            [WorkflowRun]
            public async Task<string> RunAsync(string arg)
            {
                Assert.Equal("2", arg);
                return "3";
            }
        }

        [Workflow]
        public class NoResultNoArg
        {
            [WorkflowRun]
            public Task RunAsync() => Task.CompletedTask;
        }

        [Workflow]
        public class NoResultWithArg
        {
            [WorkflowRun]
            public async Task RunAsync(string arg) => Assert.Equal("4", arg);
        }

        [Workflow]
        public class ResultMultiArg
        {
            [WorkflowRun]
            public async Task<string> RunAsync(string arg1, string arg2)
            {
                Assert.Equal("5", arg1);
                Assert.Equal("6", arg2);
                return "7";
            }
        }

        [Workflow]
        public class NoResultMultiArg
        {
            [WorkflowRun]
            public async Task RunAsync(string arg1, string arg2)
            {
                Assert.Equal("8", arg1);
                Assert.Equal("9", arg2);
            }
        }

        [WorkflowRun]
        public async Task RunAsync()
        {
#pragma warning disable SA1118 // Don't want so many lines
            // Intentionally not making options var because I want to confirm new() { ... } works
            Assert.Equal("1", await Workflow.ExecuteChildWorkflowAsync(
                (ResultNoArg wf) => wf.RunAsync(), new() { RunTimeout = TimeSpan.FromHours(1) }));
            Assert.Equal("3", await Workflow.ExecuteChildWorkflowAsync(
                (ResultWithArg wf) => wf.RunAsync("2"), new() { RunTimeout = TimeSpan.FromHours(1) }));
            await Workflow.ExecuteChildWorkflowAsync(
                (NoResultNoArg wf) => wf.RunAsync(), new() { RunTimeout = TimeSpan.FromHours(1) });
            await Workflow.ExecuteChildWorkflowAsync(
                (NoResultWithArg wf) => wf.RunAsync("4"), new() { RunTimeout = TimeSpan.FromHours(1) });
            Assert.Equal("7", await Workflow.ExecuteChildWorkflowAsync<string>(
                WorkflowDefinition.Create(typeof(ResultMultiArg)).Name!,
                new object?[] { "5", "6" },
                new() { RunTimeout = TimeSpan.FromHours(1) }));
            await Workflow.ExecuteChildWorkflowAsync(
                WorkflowDefinition.Create(typeof(NoResultMultiArg)).Name!,
                new object?[] { "8", "9" },
                new() { RunTimeout = TimeSpan.FromHours(1) });
#pragma warning restore SA1118
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_SimpleChild_ExecutesProperly()
    {
        await ExecuteWorkerAsync<SimpleChildWorkflow>(
            async worker =>
            {
                var handle = await Env.Client.StartWorkflowAsync(
                    (SimpleChildWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await handle.GetResultAsync();
                var children = new List<string>();
                await foreach (var evt in handle.FetchHistoryEventsAsync())
                {
                    var attr = evt.ChildWorkflowExecutionStartedEventAttributes;
                    if (attr != null)
                    {
                        children.Add(attr.WorkflowType.Name);
                    }
                }
                Assert.Equal(
                    new List<string>()
                    {
                        "ResultNoArg",
                        "ResultWithArg",
                        "NoResultNoArg",
                        "NoResultWithArg",
                        "ResultMultiArg",
                        "NoResultMultiArg",
                    },
                    children);
            },
            new TemporalWorkerOptions().
                AddWorkflow<SimpleChildWorkflow.ResultNoArg>().
                AddWorkflow<SimpleChildWorkflow.ResultWithArg>().
                AddWorkflow<SimpleChildWorkflow.NoResultNoArg>().
                AddWorkflow<SimpleChildWorkflow.NoResultWithArg>().
                AddWorkflow<SimpleChildWorkflow.ResultMultiArg>().
                AddWorkflow<SimpleChildWorkflow.NoResultMultiArg>());
    }

    [Workflow]
    public class TimeoutChildWorkflow
    {
        [Workflow]
        public class ChildWorkflow
        {
            [WorkflowRun]
            public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);
        }

        [WorkflowRun]
        public Task RunAsync() =>
            // Timeout after 10ms
            Workflow.ExecuteChildWorkflowAsync(
                (ChildWorkflow wf) => wf.RunAsync(),
                new() { RunTimeout = TimeSpan.FromMilliseconds(10) });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TimeoutChild_TimesOut()
    {
        await ExecuteWorkerAsync<TimeoutChildWorkflow>(
            async worker =>
            {
                var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        (TimeoutChildWorkflow wf) => wf.RunAsync(),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
                var actExc = Assert.IsType<ChildWorkflowFailureException>(wfExc.InnerException);
                var toExc = Assert.IsType<TimeoutFailureException>(actExc.InnerException);
                Assert.Equal(TimeoutType.StartToClose, toExc.TimeoutType);
            },
            new TemporalWorkerOptions().AddWorkflow<TimeoutChildWorkflow.ChildWorkflow>());
    }

    [Workflow]
    public class CancelChildWorkflow
    {
        [Workflow]
        public class ChildWorkflow
        {
            [WorkflowRun]
            public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);
        }

        [WorkflowRun]
        public async Task RunAsync(bool beforeStart)
        {
            using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                Workflow.CancellationToken);
            // Cancel before if desired
            if (beforeStart)
            {
                tokenSource.Cancel();
            }
            var handle = await Workflow.StartChildWorkflowAsync(
                (ChildWorkflow wf) => wf.RunAsync(),
                new() { CancellationToken = tokenSource.Token });
            // If not cancel before, cancel now
            if (!beforeStart)
            {
                tokenSource.Cancel();
            }
            await handle.GetResultAsync();
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_CancelChild_Cancels()
    {
        await ExecuteWorkerAsync<CancelChildWorkflow>(
            async worker =>
            {
                // Regular cancel after start
                var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        (CancelChildWorkflow wf) => wf.RunAsync(false),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
                var childExc = Assert.IsType<ChildWorkflowFailureException>(wfExc.InnerException);
                Assert.IsType<CanceledFailureException>(childExc.InnerException);

                // Cancel before start
                wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        (CancelChildWorkflow wf) => wf.RunAsync(true),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
                var cancelExc = Assert.IsType<CanceledFailureException>(wfExc.InnerException);
                Assert.Contains("cancelled before scheduled", cancelExc.Message);
            },
            new TemporalWorkerOptions().AddWorkflow<CancelChildWorkflow.ChildWorkflow>());
    }

    [Workflow]
    public class SignalChildWorkflow
    {
        [Workflow]
        public class ChildWorkflow
        {
            private string lastSignal = "<unset>";

            [WorkflowRun]
            public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);

            [WorkflowSignal]
            public async Task SomeSignalAsync(string value) => lastSignal = value;

            [WorkflowQuery]
            public string LastSignal() => lastSignal;
        }

        private ChildWorkflowHandle<ChildWorkflow>? child;

        [WorkflowRun]
        public async Task RunAsync()
        {
            child = await Workflow.StartChildWorkflowAsync((ChildWorkflow wf) => wf.RunAsync());
            await Workflow.DelayAsync(Timeout.Infinite);
        }

        [WorkflowSignal]
        public Task SignalChildAsync(string value)
        {
            if (child == null)
            {
                throw new ApplicationFailureException("Child not started");
            }
            return child.SignalAsync(wf => wf.SomeSignalAsync(value));
        }

        [WorkflowQuery]
        public string? ChildId() => child?.Id;
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_SignalChild_SignalsProperly()
    {
        await ExecuteWorkerAsync<SignalChildWorkflow>(
            async worker =>
            {
                // Start and wait for child to have started
                var handle = await Env.Client.StartWorkflowAsync(
                    (SignalChildWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                var childId = await AssertMore.EventuallyAsync(async () =>
                {
                    var childId = await handle.QueryAsync(wf => wf.ChildId());
                    Assert.NotNull(childId);
                    return childId!;
                });

                // Signal and wait for signal received
                await handle.SignalAsync(wf => wf.SignalChildAsync("some value"));
                await AssertMore.EqualEventuallyAsync(
                    "some value",
                    () => Env.Client.GetWorkflowHandle<SignalChildWorkflow.ChildWorkflow>(childId).
                        QueryAsync(wf => wf.LastSignal()));
            },
            new TemporalWorkerOptions().AddWorkflow<SignalChildWorkflow.ChildWorkflow>());
    }

    [Workflow]
    public class AlreadyStartedChildWorkflow
    {
        [Workflow]
        public class ChildWorkflow
        {
            [WorkflowRun]
            public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);
        }

        [WorkflowRun]
        public async Task RunAsync()
        {
            // Try to start a child workflow twice with the same ID
            var handle = await Workflow.StartChildWorkflowAsync(
                (ChildWorkflow wf) => wf.RunAsync());
            await Workflow.StartChildWorkflowAsync(
                (ChildWorkflow wf) => wf.RunAsync(), new() { Id = handle.Id });
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_AlreadyStartedChild_FailsProperly()
    {
        await ExecuteWorkerAsync<AlreadyStartedChildWorkflow>(
            async worker =>
            {
                // Regular cancel after start
                var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        (AlreadyStartedChildWorkflow wf) => wf.RunAsync(),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
                Assert.IsType<FailureException>(wfExc.InnerException);
                Assert.Contains("already started", wfExc.InnerException.Message);
            },
            new TemporalWorkerOptions().AddWorkflow<AlreadyStartedChildWorkflow.ChildWorkflow>());
    }

    [Workflow]
    public class ExternalWorkflow
    {
        [Workflow]
        public class OtherWorkflow
        {
            private string lastSignal = "<unset>";

            [WorkflowRun]
            public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);

            [WorkflowSignal]
            public async Task SignalAsync(string value) => lastSignal = value;

            [WorkflowQuery]
            public string LastSignal() => lastSignal;
        }

        [WorkflowRun]
        public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);

        [WorkflowSignal]
        public Task SignalExternalAsync(string otherId) =>
            Workflow.GetExternalWorkflowHandle<OtherWorkflow>(otherId).SignalAsync(
                wf => wf.SignalAsync("external signal"));

        [WorkflowSignal]
        public Task CancelExternalAsync(string otherId) =>
            Workflow.GetExternalWorkflowHandle(otherId).CancelAsync();
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_External_SignalAndCancelSucceed()
    {
        await ExecuteWorkerAsync<ExternalWorkflow>(
            async worker =>
            {
                // Start other workflow
                var otherHandle = await Env.Client.StartWorkflowAsync(
                    (ExternalWorkflow.OtherWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await AssertMore.StartedEventuallyAsync(otherHandle);

                // Start primary workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    (ExternalWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await AssertMore.StartedEventuallyAsync(handle);

                // Send a signal and confirm received
                await handle.SignalAsync(wf => wf.SignalExternalAsync(otherHandle.Id));
                await AssertMore.EqualEventuallyAsync(
                    "external signal",
                    () => otherHandle.QueryAsync(wf => wf.LastSignal()));

                // Cancel and confirm cancelled
                await handle.SignalAsync(wf => wf.CancelExternalAsync(otherHandle.Id));
                await AssertMore.EventuallyAsync(async () =>
                {
                    var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                        otherHandle.GetResultAsync());
                    Assert.IsType<CanceledFailureException>(exc.InnerException);
                });
            },
            new TemporalWorkerOptions().AddWorkflow<ExternalWorkflow.OtherWorkflow>());
    }

    [Workflow]
    public class StackTraceWorkflow
    {
        [Activity]
        public static async Task WaitCancelActivityAsync()
        {
            while (!ActivityExecutionContext.Current.CancellationToken.IsCancellationRequested)
            {
                ActivityExecutionContext.Current.Heartbeat();
                await Task.Delay(100);
            }
        }

        [Workflow]
        public class WaitForeverWorkflow
        {
            [WorkflowRun]
            public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);
        }

        private string status = "created";

        [WorkflowRun]
        public async Task RunAsync()
        {
            // Start multiple tasks and wait on them all
            await Task.WhenAll(
                Workflow.DelayAsync(TimeSpan.FromHours(2)),
                Workflow.ExecuteActivityAsync(
                    () => WaitCancelActivityAsync(),
                    new()
                    {
                        ScheduleToCloseTimeout = TimeSpan.FromHours(2),
                        HeartbeatTimeout = TimeSpan.FromSeconds(2),
                    }),
                Workflow.ExecuteChildWorkflowAsync((WaitForeverWorkflow wf) => wf.RunAsync()),
                WaitForever());
        }

        [WorkflowQuery]
        public string Status() => status;

        private async Task WaitForever()
        {
            status = "waiting";
            await Workflow.WaitConditionAsync(() => false);
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_StackTrace_FailsWhenDisabled()
    {
        await ExecuteWorkerAsync<StackTraceWorkflow>(
            async worker =>
            {
                // Start and wait until "waiting"
                var handle = await Env.Client.StartWorkflowAsync(
                    (StackTraceWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await AssertMore.EqualEventuallyAsync(
                    "waiting", () => handle.QueryAsync(wf => wf.Status()));
                var exc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                    () => handle.QueryAsync<string>("__stack_trace", Array.Empty<object?>()));
                Assert.Contains("stack traces are not enabled", exc.Message);
            },
            new TemporalWorkerOptions().
                AddActivity(StackTraceWorkflow.WaitCancelActivityAsync).
                AddWorkflow<StackTraceWorkflow.WaitForeverWorkflow>());
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_StackTrace_ReportedProperlyWhenEnabled()
    {
        await ExecuteWorkerAsync<StackTraceWorkflow>(
            async worker =>
            {
                // Start and wait until "waiting"
                var handle = await Env.Client.StartWorkflowAsync(
                    (StackTraceWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await AssertMore.EqualEventuallyAsync(
                    "waiting", () => handle.QueryAsync(wf => wf.Status()));
                // Issue stack trace query
                var trace = await handle.QueryAsync<string>("__stack_trace", Array.Empty<object?>());
                // Confirm our four tasks are the only ones there, are in order, and are waiting at
                // the expected spot
                var traces = trace.Split("\n\n");
                Assert.Equal(4, traces.Length);
                Assert.StartsWith(
                    "Task waiting at:\n   at Temporalio.Workflows.Workflow.DelayAsync",
                    traces[0]);
                Assert.StartsWith(
                    "Task waiting at:\n   at Temporalio.Workflows.Workflow.ExecuteActivityAsync",
                    traces[1]);
                Assert.StartsWith(
                    "Task waiting at:\n   at Temporalio.Workflows.Workflow.StartChildWorkflowAsync",
                    traces[2]);
                Assert.StartsWith(
                    "Task waiting at:\n   at Temporalio.Workflows.Workflow.WaitConditionAsync",
                    traces[3]);
            },
            new TemporalWorkerOptions() { WorkflowStackTrace = WorkflowStackTrace.Normal }.
                AddActivity(StackTraceWorkflow.WaitCancelActivityAsync).
                AddWorkflow<StackTraceWorkflow.WaitForeverWorkflow>());
    }

    public abstract class PatchWorkflowBase
    {
        [Activity]
        public static string PrePatchActivity() => "pre-patch";

        [Activity]
        public static string PostPatchActivity() => "post-patch";

        protected string ActivityResult { get; set; } = "<unset>";

        [WorkflowQuery]
        public string GetResult() => ActivityResult;

        [Workflow("PatchWorkflow")]
        public class PrePatchWorkflow : PatchWorkflowBase
        {
            [WorkflowRun]
            public async Task RunAsync() => ActivityResult = await Workflow.ExecuteActivityAsync(
                () => PrePatchActivity(),
                new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
        }

        [Workflow]
        public class PatchWorkflow : PatchWorkflowBase
        {
            [WorkflowRun]
            public async Task RunAsync()
            {
                if (Workflow.Patched("my-patch"))
                {
                    ActivityResult = await Workflow.ExecuteActivityAsync(
                        () => PostPatchActivity(),
                        new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
                }
                else
                {
                    ActivityResult = await Workflow.ExecuteActivityAsync(
                        () => PrePatchActivity(),
                        new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
                }
            }
        }

        [Workflow("PatchWorkflow")]
        public class DeprecatePatchWorkflow : PatchWorkflowBase
        {
            [WorkflowRun]
            public async Task RunAsync()
            {
                Workflow.DeprecatePatch("my-patch");
                ActivityResult = await Workflow.ExecuteActivityAsync(
                    () => PostPatchActivity(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
            }
        }

        [Workflow("PatchWorkflow")]
        public class PostPatchWorkflow : PatchWorkflowBase
        {
            [WorkflowRun]
            public async Task RunAsync() => ActivityResult = await Workflow.ExecuteActivityAsync(
                () => PostPatchActivity(),
                new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Patched_ProperlyHandled()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddActivity(PatchWorkflowBase.PrePatchActivity).
            AddActivity(PatchWorkflowBase.PostPatchActivity);
        async Task<string> ExecuteWorkflowAsync(string id)
        {
            var handle = await Env.Client.StartWorkflowAsync(
                (PatchWorkflowBase.PatchWorkflow wf) => wf.RunAsync(),
                new(id, taskQueue: workerOptions.TaskQueue!));
            await handle.GetResultAsync();
            return await handle.QueryAsync(wf => wf.GetResult());
        }
        Task<string> QueryWorkflowAsync(string id) =>
            Env.Client.GetWorkflowHandle<PatchWorkflowBase>(id).QueryAsync(wf => wf.GetResult());

        // Run pre-patch workflow
        var prePatchId = $"workflow-{Guid.NewGuid()}";
        await ExecuteWorkerAsync<PatchWorkflowBase.PrePatchWorkflow>(
            async worker =>
            {
                Assert.Equal("pre-patch", await ExecuteWorkflowAsync(prePatchId));
            },
            workerOptions);

        // Patch workflow and confirm pre-patch and patched work
        var patchedId = $"workflow-{Guid.NewGuid()}";
        await ExecuteWorkerAsync<PatchWorkflowBase.PatchWorkflow>(
            async worker =>
            {
                Assert.Equal("post-patch", await ExecuteWorkflowAsync(patchedId));
                Assert.Equal("pre-patch", await QueryWorkflowAsync(prePatchId));
            },
            workerOptions);

        // Deprecate patch and confirm patched and deprecated work, but not pre-patch
        var deprecatePatchId = $"workflow-{Guid.NewGuid()}";
        await ExecuteWorkerAsync<PatchWorkflowBase.DeprecatePatchWorkflow>(
            async worker =>
            {
                Assert.Equal("post-patch", await ExecuteWorkflowAsync(deprecatePatchId));
                Assert.Equal("post-patch", await QueryWorkflowAsync(patchedId));
                var exc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                    () => QueryWorkflowAsync(prePatchId));
                Assert.Contains("Nondeterminism", exc.Message);
            },
            workerOptions);

        // Remove patch and confirm post patch and deprecated work, but not pre-patch or patched
        var postPatchPatchId = $"workflow-{Guid.NewGuid()}";
        await ExecuteWorkerAsync<PatchWorkflowBase.PostPatchWorkflow>(
            async worker =>
            {
                Assert.Equal("post-patch", await ExecuteWorkflowAsync(postPatchPatchId));
                Assert.Equal("post-patch", await QueryWorkflowAsync(deprecatePatchId));
                var exc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                    () => QueryWorkflowAsync(prePatchId));
                Assert.Contains("Nondeterminism", exc.Message);
                exc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                    () => QueryWorkflowAsync(patchedId));
                Assert.Contains("Nondeterminism", exc.Message);
            },
            workerOptions);
    }

    [Workflow]
    public class HeadersWithCodecWorkflow
    {
        public enum Kind
        {
            Normal,
            Child,
            Continued,
        }

        private bool done;

        [WorkflowRun]
        public async Task RunAsync(Kind kind)
        {
            // Just continue as new
            if (kind == Kind.Normal)
            {
                throw Workflow.CreateContinueAsNewException(
                    (HeadersWithCodecWorkflow wf) => wf.RunAsync(Kind.Continued));
            }
            // Activity, local activity, child, signal child in both ways
            if (kind == Kind.Continued)
            {
                await Workflow.ExecuteActivityAsync(
                    () => DoThing(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
                await Workflow.ExecuteLocalActivityAsync(
                    () => DoThing(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
                var handle = await Workflow.StartChildWorkflowAsync(
                    (HeadersWithCodecWorkflow wf) => wf.RunAsync(Kind.Child));
                await handle.SignalAsync(wf => wf.SignalAsync(false));
                await Workflow.GetExternalWorkflowHandle<HeadersWithCodecWorkflow>(handle.Id).
                    SignalAsync(wf => wf.SignalAsync(true));
                await handle.GetResultAsync();
            }
            // Wait for done
            await Workflow.WaitConditionAsync(() => done);
        }

        [WorkflowSignal]
        public async Task SignalAsync(bool done) => this.done = done;

        [WorkflowQuery]
        public string Query() => string.Empty;

        [WorkflowUpdate]
        public async Task<string> UpdateAsync(string param) => param;

        [WorkflowUpdateValidator(nameof(UpdateAsync))]
        public void ValidateUpdate(string param)
        {
        }

        [Activity]
        public static void DoThing()
        {
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_HeadersWithCodec_EncodedProperly()
    {
        // Create client with interceptor and codec
        var inOperations = new List<(string InOp, string OutOp)>();
        var intercept = new HeaderCallbackInterceptor()
        {
            OnOutbound = name => new Dictionary<string, Payload>()
            {
                ["operation"] = DataConverter.Default.PayloadConverter.ToPayload(name),
            },
            OnInbound = (name, headers) => inOperations.Add(
                (name, DataConverter.Default.PayloadConverter.ToValue<string>(headers!["operation"]))),
        };
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Interceptors = new[] { intercept };
        newOptions.DataConverter = DataConverter.Default with
        {
            PayloadCodec = new Converters.Base64PayloadCodec(),
        };
        var client = new TemporalClient(Client.Connection, newOptions);

        await ExecuteWorkerAsync<HeadersWithCodecWorkflow>(
            async worker =>
            {
                // Start workflow, wait for child to be started, send query and signal
                var handle = await client.StartWorkflowAsync(
                    (HeadersWithCodecWorkflow wf) => wf.RunAsync(HeadersWithCodecWorkflow.Kind.Normal),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await AssertMore.ChildStartedEventuallyAsync(handle);
                await handle.QueryAsync(wf => wf.Query());
                await handle.ExecuteUpdateAsync(wf => wf.UpdateAsync("foo"));
                await handle.SignalAsync(wf => wf.SignalAsync(true));
                await handle.GetResultAsync();

                // Sort the operations and confirm all the expected ones are present
                var expectedInOperations = new List<(string InOp, string OutOp)>
                {
                    ("Activity:ExecuteActivity", "Workflow:ScheduleActivity"),
                    ("Activity:ExecuteActivity", "Workflow:ScheduleLocalActivity"),
                    ("Workflow:ExecuteWorkflow", "Client:StartWorkflow"),
                    ("Workflow:ExecuteWorkflow", "Workflow:ContinueAsNew"),
                    ("Workflow:ExecuteWorkflow", "Workflow:StartChildWorkflow"),
                    ("Workflow:HandleQuery", "Client:QueryWorkflow"),
                    ("Workflow:ValidateUpdate", "Client:StartWorkflowUpdate"),
                    ("Workflow:HandleUpdate", "Client:StartWorkflowUpdate"),
                    ("Workflow:HandleSignal", "Client:SignalWorkflow"),
                    ("Workflow:HandleSignal", "Workflow:SignalChildWorkflow"),
                    ("Workflow:HandleSignal", "Workflow:SignalExternalWorkflow"),
                };
                inOperations.Sort();
                expectedInOperations.Sort();
                Assert.Equal(expectedInOperations, inOperations);

                // Also confirm all headers in history are encoded
                var decodedHeaderOperations = new List<string>();
                async Task AddHeaders(params IDictionary<string, Payload>?[] headerSets)
                {
                    foreach (var headers in headerSets)
                    {
                        if (headers != null && headers.TryGetValue("operation", out var value))
                        {
                            decodedHeaderOperations!.Add(
                                await newOptions!.DataConverter.ToValueAsync<string>(value));
                        }
                    }
                }
                // Collect continued run only
                await foreach (var evt in handle.FetchHistoryEventsAsync())
                {
                    await AddHeaders(
                        evt.ActivityTaskScheduledEventAttributes?.Header.Fields,
                        evt.SignalExternalWorkflowExecutionInitiatedEventAttributes?.Header?.Fields,
                        evt.StartChildWorkflowExecutionInitiatedEventAttributes?.Header?.Fields,
                        evt.WorkflowExecutionSignaledEventAttributes?.Header?.Fields,
                        evt.WorkflowExecutionStartedEventAttributes?.Header?.Fields);
                }
                var expectedDecodedHeaderOperations = new List<string>
                {
                    "Client:SignalWorkflow",
                    "Workflow:ContinueAsNew",
                    "Workflow:ScheduleActivity",
                    "Workflow:SignalChildWorkflow",
                    "Workflow:SignalExternalWorkflow",
                    "Workflow:StartChildWorkflow",
                };
                decodedHeaderOperations.Sort();
                expectedDecodedHeaderOperations.Sort();
                Assert.Equal(expectedDecodedHeaderOperations, decodedHeaderOperations);
            },
            new TemporalWorkerOptions().AddActivity(HeadersWithCodecWorkflow.DoThing),
            client);
    }

    public record SimpleValue(string SomeString);

    [Workflow]
    public class RawValueWorkflow
    {
        [Activity]
        public static RawValue DoActivity(IRawValue param)
        {
            Assert.Equal(
                new SimpleValue("to activity"),
                ActivityExecutionContext.Current.PayloadConverter.ToValue<SimpleValue>(param));
            return ActivityExecutionContext.Current.PayloadConverter.ToRawValue(
                new SimpleValue("from activity"));
        }

        private bool finish;

        [WorkflowRun]
        public async Task<RawValue> RunAsync(IRawValue param)
        {
            // Confirm param
            Assert.Equal(
                new SimpleValue("to workflow"),
                Workflow.PayloadConverter.ToValue<SimpleValue>(param));

            // Check activity with raw types
            var rawResult = await Workflow.ExecuteActivityAsync(
                () => DoActivity(Workflow.PayloadConverter.ToRawValue(new SimpleValue("to activity"))),
                new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
            Assert.Equal(
                new SimpleValue("from activity"),
                Workflow.PayloadConverter.ToValue<SimpleValue>(rawResult));

            // Check activity with actual types
            var result = await Workflow.ExecuteActivityAsync<SimpleValue>(
                "DoActivity",
                new[] { new SimpleValue("to activity") },
                new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
            Assert.Equal(new SimpleValue("from activity"), result);

            // Wait for finish and return value
            await Workflow.WaitConditionAsync(() => finish);
            return Workflow.PayloadConverter.ToRawValue(new SimpleValue("from workflow"));
        }

        [WorkflowSignal]
        public async Task SignalAsync(IRawValue param)
        {
            Assert.Equal(
                new SimpleValue("to signal"),
                Workflow.PayloadConverter.ToValue<SimpleValue>(param));
        }

        // Intentionally return IRawValue and accept RawValue to confirm they work right
        [WorkflowQuery]
        public IRawValue Query(RawValue param)
        {
            Assert.Equal(
                new SimpleValue("to query"),
                Workflow.PayloadConverter.ToValue<SimpleValue>(param));
            return Workflow.PayloadConverter.ToRawValue(new SimpleValue("from query"));
        }

        [WorkflowSignal]
        public async Task FinishAsync() => finish = true;
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_RawValue_ConvertsProperly()
    {
        var converter = DataConverter.Default.PayloadConverter;
        await ExecuteWorkerAsync<RawValueWorkflow>(
            async worker =>
            {
                // Check workflow with raw types
                var rawHandle = await Env.Client.StartWorkflowAsync(
                    (RawValueWorkflow wf) => wf.RunAsync(converter.ToRawValue(new SimpleValue("to workflow"))),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await rawHandle.SignalAsync(
                    wf => wf.SignalAsync(converter.ToRawValue(new SimpleValue("to signal"))));
                var rawQueryResult = await rawHandle.QueryAsync(
                    wf => wf.Query(converter.ToRawValue(new SimpleValue("to query"))));
                Assert.Equal(new SimpleValue("from query"), converter.ToValue<SimpleValue>(rawQueryResult));
                await rawHandle.SignalAsync(wf => wf.FinishAsync());
                var rawResult = await rawHandle.GetResultAsync();
                Assert.Equal(new SimpleValue("from workflow"), converter.ToValue<SimpleValue>(rawResult));

                // Check workflow with actual types
                var handle = await Env.Client.StartWorkflowAsync(
                    "RawValueWorkflow",
                    new[] { new SimpleValue("to workflow") },
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await handle.SignalAsync("Signal", new[] { new SimpleValue("to signal") });
                var queryResult = await handle.QueryAsync<SimpleValue>("Query", new[] { new SimpleValue("to query") });
                Assert.Equal(new SimpleValue("from query"), queryResult);
                await handle.SignalAsync("Finish", Array.Empty<object?>());
                var result = await rawHandle.GetResultAsync<SimpleValue>();
                Assert.Equal(new SimpleValue("from workflow"), result);
            },
            new TemporalWorkerOptions().AddActivity(RawValueWorkflow.DoActivity));
    }

    [Workflow]
    public interface INonDynamicWorkflow
    {
        [WorkflowQuery]
        IList<string> Events { get; }

        [WorkflowRun]
        Task<string> RunAsync(string arg);

        [WorkflowSignal]
        Task FinishAsync();

        [WorkflowSignal]
        Task SomeSignalAsync(string arg);

        [WorkflowQuery]
        string SomeQuery(string arg);

        [WorkflowUpdate]
        Task<string> SomeUpdateAsync(string arg);
    }

    [Workflow(Dynamic = true)]
    public class DynamicWorkflow
    {
        private bool finish;

        [WorkflowQuery]
        public IList<string> Events { get; } = new List<string>();

        [WorkflowRun]
        public async Task<string> RunAsync(IRawValue[] args)
        {
            Events.Add($"workflow-{Workflow.Info.WorkflowType}: " +
                Workflow.PayloadConverter.ToValue<string>(args.Single()));
            Events.Add(await Workflow.ExecuteActivityAsync(
                () => NonDynamicActivity("activity arg"),
                new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) }));
            await Workflow.WaitConditionAsync(() => finish);
            return "done";
        }

        [WorkflowSignal]
        public async Task FinishAsync() => finish = true;

        [WorkflowSignal(Dynamic = true)]
        public async Task DynamicSignalAsync(string signalName, IRawValue[] args)
        {
            Events.Add($"signal-{signalName}: " +
                Workflow.PayloadConverter.ToValue<string>(args.Single()));
        }

        [WorkflowQuery(Dynamic = true)]
        public string DynamicQuery(string queryName, IRawValue[] args)
        {
            Events.Add($"query-{queryName}: " +
                Workflow.PayloadConverter.ToValue<string>(args.Single()));
            return "done";
        }

        [WorkflowUpdate(Dynamic = true)]
        public async Task<string> DynamicUpdateAsync(string updateName, IRawValue[] args)
        {
            Events.Add($"update-{updateName}: " +
                Workflow.PayloadConverter.ToValue<string>(args.Single()));
            return "done";
        }

        [Activity]
        public static string NonDynamicActivity(string arg) => throw new NotImplementedException();

        [Activity(Dynamic = true)]
        public static string DynamicActivity(IRawValue[] args)
        {
            var type = ActivityExecutionContext.Current.Info.ActivityType;
            var arg = ActivityExecutionContext.Current.PayloadConverter.ToValue<string>(args.Single());
            return $"activity-{type}: {arg}";
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Dynamic_CalledProperly()
    {
        await ExecuteWorkerAsync<DynamicWorkflow>(
            async worker =>
            {
                // Start, send signal, send query, finish (signal), wait for complete, fetch
                // events (signal)
                var handle = await Env.Client.StartWorkflowAsync(
                    (INonDynamicWorkflow wf) => wf.RunAsync("workflow arg"),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await handle.SignalAsync(wf => wf.SomeSignalAsync("signal arg"));
                Assert.Equal("done", await handle.QueryAsync(wf => wf.SomeQuery("query arg")));
                Assert.Equal("done", await handle.ExecuteUpdateAsync(wf => wf.SomeUpdateAsync("update arg")));
                await handle.SignalAsync(wf => wf.FinishAsync());
                Assert.Equal("done", await handle.GetResultAsync());
                Assert.Equal(
                    new List<string>
                    {
                        "activity-NonDynamicActivity: activity arg",
                        "query-SomeQuery: query arg",
                        "signal-SomeSignal: signal arg",
                        "update-SomeUpdate: update arg",
                        "workflow-NonDynamicWorkflow: workflow arg",
                    },
                    (await handle.QueryAsync(wf => wf.Events)).OrderBy(v => v).ToList());
            },
            new TemporalWorkerOptions().AddActivity(DynamicWorkflow.DynamicActivity));
    }

    [Workflow]
    public class DynamicHandlersWorkflow
    {
        [WorkflowQuery]
        public IList<string> Events { get; } = new List<string>();

        [WorkflowRun]
        public Task RunAsync() => Workflow.WaitConditionAsync(() => false);

        [WorkflowSignal]
        public async Task SetHandlersAsync()
        {
            Workflow.DynamicSignal = WorkflowSignalDefinition.CreateWithoutAttribute(
                null, async (string signalName, IRawValue[] args) =>
                {
                    Events.Add($"signal-{signalName}: " +
                        Workflow.PayloadConverter.ToValue<string>(args.Single()));
                });
            Workflow.DynamicQuery = WorkflowQueryDefinition.CreateWithoutAttribute(
                null, (string queryName, IRawValue[] args) =>
                {
                    Events.Add($"query-{queryName}: " +
                        Workflow.PayloadConverter.ToValue<string>(args.Single()));
                    return "done";
                });
            Workflow.DynamicUpdate = WorkflowUpdateDefinition.CreateWithoutAttribute(
                null, async (string updateName, IRawValue[] args) =>
                {
                    Events.Add($"update-{updateName}: " +
                        Workflow.PayloadConverter.ToValue<string>(args.Single()));
                    return "done";
                });
        }

        [WorkflowSignal]
        public async Task UnsetHandlersAsync()
        {
            Workflow.DynamicSignal = null;
            Workflow.DynamicQuery = null;
            Workflow.DynamicUpdate = null;
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_DynamicHandlers_AddedRemovedProperly()
    {
        await ExecuteWorkerAsync<DynamicHandlersWorkflow>(
            async worker =>
            {
                // Start
                var handle = await Env.Client.StartWorkflowAsync(
                    (DynamicHandlersWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

                // Confirm signal/query unhandled
                await handle.SignalAsync("SomeSignal1", new[] { "signal arg 1" });
                var queryExc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                    () => handle.QueryAsync<string>("SomeQuery1", new[] { "query arg 1" }));
                Assert.Contains("not found", queryExc.Message);
                var updateExc = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                    () => handle.ExecuteUpdateAsync<string>("SomeUpdate1", new[] { "update arg 1" }));
                Assert.Contains("not found", updateExc.InnerException?.Message);
                Assert.Empty(await handle.QueryAsync(wf => wf.Events));

                // Add handlers, and confirm all signals are drained to it and it handles
                // queries/updates
                await handle.SignalAsync(wf => wf.SetHandlersAsync());
                await handle.SignalAsync("SomeSignal2", new[] { "signal arg 2" });
                Assert.Equal(
                    "done", await handle.QueryAsync<string>("SomeQuery2", new[] { "query arg 2" }));
                Assert.Equal(
                    "done", await handle.ExecuteUpdateAsync<string>("SomeUpdate1", new[] { "update arg 1" }));
                var expectedEvents = new List<string>
                {
                    "query-SomeQuery2: query arg 2",
                    "signal-SomeSignal1: signal arg 1",
                    "signal-SomeSignal2: signal arg 2",
                    "update-SomeUpdate1: update arg 1",
                };
                Assert.Equal(
                    expectedEvents,
                    (await handle.QueryAsync(wf => wf.Events)).OrderBy(v => v).ToList());

                // Remove handlers and confirm things go back to unhandled
                await handle.SignalAsync(wf => wf.UnsetHandlersAsync());
                await handle.SignalAsync("SomeSignal3", new[] { "signal arg 3" });
                queryExc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                    () => handle.QueryAsync<string>("SomeQuery3", new[] { "query arg 3" }));
                Assert.Contains("not found", queryExc.Message);
                updateExc = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                    () => handle.ExecuteUpdateAsync<string>("SomeUpdate1", new[] { "update arg 1" }));
                Assert.Contains("not found", updateExc.InnerException?.Message);
                Assert.Equal(
                    expectedEvents,
                    (await handle.QueryAsync(wf => wf.Events)).OrderBy(v => v).ToList());
            });
    }

    [Workflow]
    public class TaskEventsWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            // Wait for cancel, then throw a task failure
            try
            {
                await Workflow.DelayAsync(TimeSpan.FromDays(5));
            }
            catch (TaskCanceledException)
            {
                throw new InvalidOperationException("Intentional task failure");
            }
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TaskEvents_AreRecordedProperly()
    {
        // Track events
        var workerOptions = new TemporalWorkerOptions();
        var startingEvents = new ConcurrentQueue<WorkflowTaskStartingEventArgs>();
        var completedEvents = new ConcurrentQueue<WorkflowTaskCompletedEventArgs>();
        EventHandler<WorkflowTaskStartingEventArgs> startingHandler = (_, e) => startingEvents.Enqueue(e);
        EventHandler<WorkflowTaskCompletedEventArgs> completedHandler = (_, e) => completedEvents.Enqueue(e);
        workerOptions.WorkflowTaskStarting += startingHandler;
        workerOptions.WorkflowTaskCompleted += completedHandler;

        // Run worker
        await ExecuteWorkerAsync<TaskEventsWorkflow>(
            async worker =>
            {
                // Remove the handlers to prove that altering event takes no effect after start
                workerOptions.WorkflowTaskStarting -= startingHandler;
                workerOptions.WorkflowTaskCompleted -= completedHandler;

                // Start
                var handle = await Env.Client.StartWorkflowAsync(
                    (TaskEventsWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

                // Wait for timer start to appear in history
                await AssertMore.HasEventEventuallyAsync(handle, e => e.TimerStartedEventAttributes != null);

                // Confirm events
                Assert.Single(startingEvents);
                Assert.Equal("TaskEventsWorkflow", startingEvents.Single().WorkflowDefinition.Name);
                Assert.Equal(handle.Id, startingEvents.Single().WorkflowInfo.WorkflowId);
                Assert.Equal(handle.ResultRunId, startingEvents.Single().WorkflowInfo.RunId);
                Assert.Single(completedEvents);
                Assert.Equal(handle.Id, completedEvents.Single().WorkflowInfo.WorkflowId);
                Assert.Null(completedEvents.Single().TaskFailureException);

                // Now cancel, wait for task failure in history, and confirm task failure appears in
                // events
                await handle.CancelAsync();
                await AssertTaskFailureContainsEventuallyAsync(handle, "Intentional task failure");
                Assert.True(startingEvents.Count >= 2);
                Assert.True(completedEvents.Count >= 2);
                var exc = Assert.IsType<InvalidOperationException>(completedEvents.ElementAt(1).TaskFailureException);
                Assert.Equal("Intentional task failure", exc.Message);
            },
            workerOptions);
    }

    public class DuplicateActivities
    {
        private readonly string returnValue;

        public DuplicateActivities(string returnValue) => this.returnValue = returnValue;

        [Activity]
        public string DoThing() => returnValue;
    }

    [Workflow]
    public class DuplicateActivityWorkflow
    {
        [WorkflowRun]
        public async Task<string[]> RunAsync(string taskQueue1, string taskQueue2)
        {
            return new[]
            {
                await Workflow.ExecuteActivityAsync(
                    (DuplicateActivities act) => act.DoThing(),
                    new()
                    {
                        TaskQueue = taskQueue1,
                        ScheduleToCloseTimeout = TimeSpan.FromHours(1),
                    }),
                await Workflow.ExecuteActivityAsync(
                    (DuplicateActivities act) => act.DoThing(),
                    new()
                    {
                        TaskQueue = taskQueue2,
                        ScheduleToCloseTimeout = TimeSpan.FromHours(1),
                    }),
            };
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_DuplicateActivity_DoesNotCacheInstance()
    {
        await ExecuteWorkerAsync<DuplicateActivityWorkflow>(
            async worker1 =>
            {
                await ExecuteWorkerAsync<DuplicateActivityWorkflow>(
                    async worker2 =>
                    {
                        var ret = await Env.Client.ExecuteWorkflowAsync(
                            (DuplicateActivityWorkflow wf) =>
                                wf.RunAsync(worker1.Options.TaskQueue!, worker2.Options.TaskQueue!),
                            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker1.Options.TaskQueue!));
                        Assert.Equal(new[] { "instance1", "instance2" }, ret);
                    },
                    new TemporalWorkerOptions().AddAllActivities(new DuplicateActivities("instance2")));
            },
            new TemporalWorkerOptions().AddAllActivities(new DuplicateActivities("instance1")));
    }

    public static class CustomMetricsActivities
    {
        [Activity]
        public static void DoActivity()
        {
            var counter = ActivityExecutionContext.Current.MetricMeter.CreateCounter<int>(
                "my-activity-counter",
                "my-activity-unit",
                "my-activity-description");
            counter.Add(12);
            counter.Add(34, new Dictionary<string, object>() { { "my-activity-extra-tag", 12.34 } });
        }
    }

    [Workflow]
    public class CustomMetricsWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            await Workflow.ExecuteActivityAsync(
                () => CustomMetricsActivities.DoActivity(),
                new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });

            var histogram = Workflow.MetricMeter.CreateHistogram<int>(
                "my-workflow-histogram",
                "my-workflow-unit",
                "my-workflow-description");
            histogram.Record(56);
            histogram.
                WithTags(new Dictionary<string, object>() { { "my-workflow-extra-tag", 1234 } }).
                Record(78);
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_CustomMetrics_WorksWithPrometheus()
    {
        // Create a new runtime with a Prometheus server
        var promAddr = $"127.0.0.1:{TestUtils.FreePort()}";
        var runtime = new TemporalRuntime(new()
        {
            Telemetry = new()
            {
                // We'll also test the metric prefix
                Metrics = new() { Prometheus = new(promAddr), MetricPrefix = "foo_" },
            },
        });
        var client = await TemporalClient.ConnectAsync(
            new()
            {
                TargetHost = Client.Connection.Options.TargetHost,
                Namespace = Client.Options.Namespace,
                Runtime = runtime,
            });

        await ExecuteWorkerAsync<CustomMetricsWorkflow>(
            async worker =>
            {
                // Let's record a gauge at the runtime level
                var gauge = runtime.MetricMeter.
                    WithTags(new Dictionary<string, object>() { { "my-runtime-extra-tag", true } }).
                    CreateGauge<int>("my-runtime-gauge", description: "my-runtime-description");
                gauge.Set(90);

                // Run workflow
                await client.ExecuteWorkflowAsync(
                    (CustomMetricsWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

                // Get Prometheus dump
                using var httpClient = new HttpClient();
                var resp = await httpClient.GetAsync(new Uri($"http://{promAddr}/metrics"));
                var body = await resp.Content.ReadAsStringAsync();
                var bodyLines = body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                // Intentionally naive metric checker
                void AssertMetricExists(
                    string name,
                    IEnumerable<KeyValuePair<string, string>> atLeastLabels,
                    int value) => Assert.Contains(bodyLines, line =>
                {
                    // Must have metric name
                    if (!line.StartsWith(name + "{"))
                    {
                        return false;
                    }
                    // Must have labels (which we don't escape in this test)
                    foreach (var pair in atLeastLabels)
                    {
                        if (!line.Contains($"{pair.Key}=\"{pair.Value}\""))
                        {
                            return false;
                        }
                    }
                    return line.EndsWith($" {value}");
                });
                void AssertMetricDescriptionExists(string name, string description) =>
                    Assert.Contains($"# HELP {name} {description}", bodyLines);

                // Check some metrics are as we expect
                AssertMetricDescriptionExists("my_runtime_gauge", "my-runtime-description");
                AssertMetricExists(
                    "my_runtime_gauge",
                    new Dictionary<string, string>()
                    {
                        { "my_runtime_extra_tag", "true" },
                        // Let's also check the global service name label
                        { "service_name", "temporal-core-sdk" },
                    },
                    90);
                AssertMetricDescriptionExists(
                    "my_workflow_histogram", "my-workflow-description");
                AssertMetricExists(
                    "my_workflow_histogram_sum",
                    new Dictionary<string, string>(),
                    56);
                AssertMetricExists(
                    "my_workflow_histogram_sum",
                    new Dictionary<string, string>() { { "my_workflow_extra_tag", "1234" } },
                    78);
                AssertMetricDescriptionExists(
                    "my_activity_counter", "my-activity-description");
                AssertMetricExists(
                    "my_activity_counter",
                    new Dictionary<string, string>(),
                    12);
                AssertMetricExists(
                    "my_activity_counter",
                    new Dictionary<string, string>() { { "my_activity_extra_tag", "12.34" } },
                    34);

                // Also check a Temporal metric got its prefix
                AssertMetricExists(
                    "foo_workflow_completed",
                    new Dictionary<string, string>() { { "workflow_type", "CustomMetricsWorkflow" } },
                    1);
            },
            new TemporalWorkerOptions().AddActivity(CustomMetricsActivities.DoActivity),
            client);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_CustomMetrics_WorksWithCustomMeter()
    {
        // Create runtime/client with capturing meter
        var meter = new TestUtils.CaptureMetricMeter();
        var runtime = new TemporalRuntime(new()
        {
            Telemetry = new()
            {
                Metrics = new() { CustomMetricMeter = meter, MetricPrefix = "some-prefix_" },
            },
        });
        var client = await TemporalClient.ConnectAsync(
            new()
            {
                TargetHost = Client.Connection.Options.TargetHost,
                Namespace = Client.Options.Namespace,
                Runtime = runtime,
            });

        // Run workflow
        var taskQueue = string.Empty;
        await ExecuteWorkerAsync<CustomMetricsWorkflow>(
            async worker =>
            {
                taskQueue = worker.Options.TaskQueue!;
                await client.ExecuteWorkflowAsync(
                    (CustomMetricsWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            },
            new TemporalWorkerOptions().AddActivity(CustomMetricsActivities.DoActivity),
            client);
        // Workflow histogram with some extra sanity checks
        var metric = Assert.Single(meter.Metrics, m =>
            m.Name == "my-workflow-histogram" &&
            m.Unit == "my-workflow-unit" &&
            m.Description == "my-workflow-description");
        Assert.Single(metric.Values, v =>
            v.Tags.Contains(new("namespace", client.Options.Namespace)) &&
            v.Tags.Contains(new("task_queue", taskQueue)) &&
            v.Tags.Contains(new("workflow_type", "CustomMetricsWorkflow")) &&
            !v.Tags.ContainsKey("my-workflow-extra-tag") &&
            (long)v.Value == 56);
        Assert.Single(metric.Values, v =>
            v.Tags.Contains(new("my-workflow-extra-tag", 1234L)) &&
            (long)v.Value == 78);
        // Activity counter
        metric = Assert.Single(meter.Metrics, m =>
            m.Name == "my-activity-counter" &&
            m.Unit == "my-activity-unit" &&
            m.Description == "my-activity-description");
        Assert.Single(metric.Values, v =>
            v.Tags.Contains(new("namespace", client.Options.Namespace)) &&
            v.Tags.Contains(new("task_queue", taskQueue)) &&
            v.Tags.Contains(new("activity_type", "DoActivity")) &&
            !v.Tags.ContainsKey("my-activity-extra-tag") &&
            (long)v.Value == 12);
        Assert.Single(metric.Values, v =>
            v.Tags.Contains(new("my-activity-extra-tag", 12.34D)) &&
            (long)v.Value == 34);
        // Check Temporal metric
        metric = Assert.Single(meter.Metrics, m => m.Name == "some-prefix_workflow_completed");
        Assert.Single(metric.Values, v =>
            v.Tags.Contains(new("workflow_type", "CustomMetricsWorkflow")) &&
            (long)v.Value == 1);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_CustomMetrics_FloatsAndDurations()
    {
        var timeSpan = new TimeSpan(2, 0, 0, 3, 4);
        async Task<TestUtils.CaptureMetricMeter> DoStuffAsync(CustomMetricMeterOptions.DurationFormat durationFormat)
        {
            var meter = new TestUtils.CaptureMetricMeter();
            var runtime = new TemporalRuntime(new()
            {
                Telemetry = new()
                {
                    Metrics = new()
                    {
                        CustomMetricMeter = meter,
                        CustomMetricMeterOptions = new() { HistogramDurationFormat = durationFormat },
                    },
                },
            });
            var client = await TemporalClient.ConnectAsync(
                new()
                {
                    TargetHost = Client.Connection.Options.TargetHost,
                    Namespace = Client.Options.Namespace,
                    Runtime = runtime,
                });
            var taskQueue = string.Empty;
            await ExecuteWorkerAsync<SimpleWorkflow>(
                async worker =>
                {
                    taskQueue = worker.Options.TaskQueue!;
                    await client.ExecuteWorkflowAsync(
                        (SimpleWorkflow wf) => wf.RunAsync("Temporal"),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue));
                },
                client: client);
            // Also, add some manual types beyond the defaults tested in other tests
            runtime.MetricMeter.CreateHistogram<double>("my-histogram-float").Record(1.23);
            runtime.MetricMeter.CreateHistogram<TimeSpan>("my-histogram-duration").Record(timeSpan);
            runtime.MetricMeter.CreateGauge<double>("my-gauge-float").Set(4.56);
            return meter;
        }

        // Do stuff with ms duration format, check metrics
        var meter = await DoStuffAsync(CustomMetricMeterOptions.DurationFormat.IntegerMilliseconds);
        Assert.Single(
            Assert.Single(meter.Metrics, v =>
                v.Name == "temporal_workflow_task_execution_latency" && v.Unit == "ms").Values,
            v => v.Value is long val);
        Assert.Single(
            Assert.Single(meter.Metrics, v => v.Name == "my-histogram-float").Values,
            v => v.Value is double val && val == 1.23);
        Assert.Single(
            Assert.Single(meter.Metrics, v => v.Name == "my-histogram-duration").Values,
            v => v.Value is long val && val == (long)timeSpan.TotalMilliseconds);
        Assert.Single(
            Assert.Single(meter.Metrics, v => v.Name == "my-gauge-float").Values,
            v => v.Value is double val && val == 4.56);

        // Do it again with seconds
        meter = await DoStuffAsync(CustomMetricMeterOptions.DurationFormat.FloatSeconds);
        // Took less than 5s
        Assert.Single(
            Assert.Single(meter.Metrics, v =>
                v.Name == "temporal_workflow_task_execution_latency" && v.Unit == "s").Values,
            v => v.Value is double val && val < 5);
        Assert.Single(
            Assert.Single(meter.Metrics, v => v.Name == "my-histogram-duration").Values,
            v => v.Value is double val && val == timeSpan.TotalSeconds);

        // Do it again with TimeSpan
        meter = await DoStuffAsync(CustomMetricMeterOptions.DurationFormat.TimeSpan);
        // Took less than 5s
        Assert.Single(
            Assert.Single(meter.Metrics, v =>
                v.Name == "temporal_workflow_task_execution_latency" && v.Unit == "duration").Values,
            v => v.Value is TimeSpan val && val < TimeSpan.FromSeconds(5));
        Assert.Single(
            Assert.Single(meter.Metrics, v => v.Name == "my-histogram-duration").Values,
            v => v.Value is TimeSpan val && val == timeSpan);
    }

    [Workflow]
    public class LastFailureWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            // First attempt fail, second attempt confirm failure is present
            if (Workflow.Info.Attempt == 1)
            {
                throw new ApplicationFailureException(
                    "Intentional failure", details: new[] { "some detail" });
            }
            var err = Assert.IsType<ApplicationFailureException>(Workflow.Info.LastFailure);
            Assert.Equal("Intentional failure", err.Message);
            Assert.Equal("some detail", err.Details.ElementAt<string>(0));
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_LastFailure_ProperlyPresent()
    {
        await ExecuteWorkerAsync<LastFailureWorkflow>(async worker =>
        {
            await Env.Client.ExecuteWorkflowAsync(
                (LastFailureWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                {
                    RetryPolicy = new() { MaximumAttempts = 2 },
                });
        });
    }

    [Workflow]
    public class LastResultWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            var maybeLastResult = Workflow.Info.LastResult?.SingleOrDefault();
            if (maybeLastResult is { } lastResult)
            {
                var lastResultStr = Workflow.PayloadConverter.ToValue<string>(lastResult);
                return $"last result: {lastResultStr}";
            }
            return "no result";
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_LastResult_ProperlyPresent()
    {
        await TestUtils.AssertNoSchedulesAsync(Client);

        await ExecuteWorkerAsync<LastResultWorkflow>(async worker =>
        {
            // Create schedule, trigger twice, confirm second got result of first
            var schedAction = ScheduleActionStartWorkflow.Create(
                (LastResultWorkflow wf) => wf.RunAsync(),
                new WorkflowOptions(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            var sched = await Client.CreateScheduleAsync(
                "sched-id",
                new(schedAction, new ScheduleSpec()) { State = new() { Paused = true } });
            async Task<string[]> AllResultsAsync()
            {
                var desc = await sched.DescribeAsync();
                return await Task.WhenAll(desc.Info.RecentActions.Select(async res =>
                {
                    var action = res.Action as ScheduleActionExecutionStartWorkflow;
                    var handle = Client.GetWorkflowHandle(
                        action!.WorkflowId) with
                    { ResultRunId = action.FirstExecutionRunId };
                    return await handle.GetResultAsync<string>();
                }));
            }

            // Check first result
            await sched.TriggerAsync();
            await AssertMore.EqualEventuallyAsync(new string[] { "no result" }, AllResultsAsync);

            // Check both results
            await sched.TriggerAsync();
            await AssertMore.EqualEventuallyAsync(
                new string[] { "no result", "last result: no result" },
                AllResultsAsync);
        });

        await TestUtils.DeleteAllSchedulesAsync(Client);
    }

    [Workflow]
    public class UpdateWorkflow
    {
        [WorkflowRun]
        public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);

        [WorkflowUpdate]
        public Task DoUpdateNoParamNoResponseAsync() => Task.CompletedTask;

        [WorkflowUpdate]
        public async Task<string> DoUpdateNoParamResponseAsync() =>
            $"no-param-response: {Workflow.Info.WorkflowId}";

        [WorkflowUpdate("some-update-name")]
        public async Task DoUpdateOneParamNoResponseAsync(string param)
        {
            switch (param)
            {
                case "update-application-failure":
                    throw new ApplicationFailureException("Intentional update application failure");
                case "update-invalid-operation-new-task":
                    // We have to have a sleep to roll the task over, or this update will never even
                    // be in history
                    await Workflow.DelayAsync(1);
                    throw new InvalidOperationException("Intentional update invalid operation");
                case "update-invalid-operation-same-task":
                    throw new InvalidOperationException("Intentional update invalid operation");
                case "update-continue-as-new":
                    await Workflow.DelayAsync(1);
                    throw Workflow.CreateContinueAsNewException((UpdateWorkflow wf) => wf.RunAsync());
            }
        }

        [WorkflowUpdate]
        public async Task<string> DoUpdateOneParamResponseAsync(string param) =>
            $"one-param-response: {param}";

        [WorkflowUpdateValidator(nameof(DoUpdateOneParamNoResponseAsync))]
        public void ValidateDoUpdateOneParamNoResponse(string param)
        {
            switch (param)
            {
                case "validate-application-failure":
                    throw new ApplicationFailureException("Intentional validator application failure");
                case "validate-invalid-operation":
                    throw new InvalidOperationException("Intentional validator invalid operation");
                case "validate-continue-as-new":
                    throw Workflow.CreateContinueAsNewException((UpdateWorkflow wf) => wf.RunAsync());
            }
        }

        [WorkflowUpdate]
        public async Task DoUpdateLongWaitAsync()
        {
            Waiting = true;
            await Workflow.DelayAsync(TimeSpan.FromHours(1));
        }

        [WorkflowQuery]
        public bool Waiting { get; private set; }

        [WorkflowUpdate]
        public async Task DoUpdateValidatorCommandsAsync() => throw new InvalidOperationException();

        [WorkflowUpdateValidator(nameof(DoUpdateValidatorCommandsAsync))]
        public void ValidateDoUpdateValidatorCommands() => _ = Workflow.DelayAsync(5000);

        [WorkflowUpdate]
        public Task DoUpdateFailOutsideAsync() =>
            throw new InvalidOperationException("Intentional update invalid operation");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Updates_AllOverloadsWork()
    {
        await ExecuteWorkerAsync<UpdateWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (UpdateWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            // Make all possible overload calls via start then get response
            await (await ((WorkflowHandle)handle).StartUpdateAsync(
                (UpdateWorkflow wf) => wf.DoUpdateNoParamNoResponseAsync(),
                new(WorkflowUpdateStage.Accepted))).GetResultAsync();
            Assert.Equal(
                $"no-param-response: {handle.Id}",
                await (await ((WorkflowHandle)handle).StartUpdateAsync(
                    (UpdateWorkflow wf) => wf.DoUpdateNoParamResponseAsync(),
                    new(WorkflowUpdateStage.Accepted))).GetResultAsync());
            await (await ((WorkflowHandle)handle).StartUpdateAsync(
                (UpdateWorkflow wf) => wf.DoUpdateOneParamNoResponseAsync("some-param"),
                new(WorkflowUpdateStage.Accepted))).GetResultAsync();
            await (await ((WorkflowHandle)handle).StartUpdateAsync(
                (UpdateWorkflow wf) => wf.DoUpdateOneParamNoResponseAsync("some-param"),
                new(WorkflowUpdateStage.Accepted))).GetResultAsync();
            Assert.Equal(
                "one-param-response: some-param",
                await (await ((WorkflowHandle)handle).StartUpdateAsync(
                    (UpdateWorkflow wf) => wf.DoUpdateOneParamResponseAsync("some-param"),
                    new(WorkflowUpdateStage.Accepted))).GetResultAsync());
            await (await handle.StartUpdateAsync(
                "some-update-name",
                new[] { "some-param" },
                new(WorkflowUpdateStage.Accepted))).GetResultAsync();
            Assert.Equal(
                "one-param-response: some-param",
                await (await handle.StartUpdateAsync<string>(
                    "DoUpdateOneParamResponse",
                    new[] { "some-param" },
                    new(WorkflowUpdateStage.Accepted))).GetResultAsync());
            await (await handle.StartUpdateAsync(
                wf => wf.DoUpdateNoParamNoResponseAsync(),
                new(WorkflowUpdateStage.Accepted))).GetResultAsync();
            Assert.Equal(
                $"no-param-response: {handle.Id}",
                await (await handle.StartUpdateAsync(
                    wf => wf.DoUpdateNoParamResponseAsync(),
                    new(WorkflowUpdateStage.Accepted))).GetResultAsync());

            // Make all possible overload calls via execute
            await ((WorkflowHandle)handle).ExecuteUpdateAsync(
                (UpdateWorkflow wf) => wf.DoUpdateNoParamNoResponseAsync());
            Assert.Equal(
                $"no-param-response: {handle.Id}",
                await ((WorkflowHandle)handle).ExecuteUpdateAsync(
                    (UpdateWorkflow wf) => wf.DoUpdateNoParamResponseAsync()));
            await ((WorkflowHandle)handle).ExecuteUpdateAsync(
                (UpdateWorkflow wf) => wf.DoUpdateOneParamNoResponseAsync("some-param"));
            await ((WorkflowHandle)handle).ExecuteUpdateAsync(
                (UpdateWorkflow wf) => wf.DoUpdateOneParamNoResponseAsync("some-param"));
            Assert.Equal(
                "one-param-response: some-param",
                await ((WorkflowHandle)handle).ExecuteUpdateAsync(
                    (UpdateWorkflow wf) => wf.DoUpdateOneParamResponseAsync("some-param")));
            await handle.ExecuteUpdateAsync(
                "some-update-name", new[] { "some-param" });
            Assert.Equal(
                "one-param-response: some-param",
                await handle.ExecuteUpdateAsync<string>(
                    "DoUpdateOneParamResponse", new[] { "some-param" }));
            await handle.ExecuteUpdateAsync(
                wf => wf.DoUpdateNoParamNoResponseAsync());
            Assert.Equal(
                $"no-param-response: {handle.Id}",
                await handle.ExecuteUpdateAsync(
                    wf => wf.DoUpdateNoParamResponseAsync()));

            // Make updates, then get handles manually, then get response
            await handle.GetUpdateHandle((await handle.StartUpdateAsync(
                wf => wf.DoUpdateNoParamNoResponseAsync(),
                new(WorkflowUpdateStage.Accepted))).Id).GetResultAsync();
            Assert.Equal(
                $"no-param-response: {handle.Id}",
                await handle.GetUpdateHandle<string>((await handle.StartUpdateAsync(
                    wf => wf.DoUpdateNoParamResponseAsync(),
                    new(WorkflowUpdateStage.Accepted))).Id).GetResultAsync());
            Assert.Equal(
                $"no-param-response: {handle.Id}",
                await handle.GetUpdateHandle((await handle.StartUpdateAsync(
                    wf => wf.DoUpdateNoParamResponseAsync(),
                    new(WorkflowUpdateStage.Accepted))).Id).GetResultAsync<string>());
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Updates_ExceptionsHandledProperly()
    {
        await ExecuteWorkerAsync<UpdateWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (UpdateWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            // Validator app exception
            var updateExc = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                () => handle.ExecuteUpdateAsync(wf =>
                    wf.DoUpdateOneParamNoResponseAsync("validate-application-failure")));
            var appExc = Assert.IsType<ApplicationFailureException>(updateExc.InnerException);
            Assert.Equal("Intentional validator application failure", appExc.Message);

            // Validator non-Temporal exception
            updateExc = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                () => handle.ExecuteUpdateAsync(wf =>
                    wf.DoUpdateOneParamNoResponseAsync("validate-invalid-operation")));
            appExc = Assert.IsType<ApplicationFailureException>(updateExc.InnerException);
            Assert.Equal("Intentional validator invalid operation", appExc.Message);
            Assert.Equal("InvalidOperationException", appExc.ErrorType);

            // Validator continue as new exception treated like non-Temporal exception
            updateExc = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                () => handle.ExecuteUpdateAsync(wf =>
                    wf.DoUpdateOneParamNoResponseAsync("validate-continue-as-new")));
            appExc = Assert.IsType<ApplicationFailureException>(updateExc.InnerException);
            Assert.Equal("ContinueAsNewException", appExc.ErrorType);

            // Check history and confirm none of those validator exceptions made it to history
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                Assert.Null(evt.WorkflowExecutionUpdateAcceptedEventAttributes);
            }

            // Update app exception
            updateExc = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                () => handle.ExecuteUpdateAsync(wf =>
                    wf.DoUpdateOneParamNoResponseAsync("update-application-failure")));
            appExc = Assert.IsType<ApplicationFailureException>(updateExc.InnerException);
            Assert.Equal("Intentional update application failure", appExc.Message);

            // Check history and confirm there is an update accepted and failed
            var foundUpdateAccepted = false;
            var foundUpdateFailed = false;
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                if (evt.WorkflowExecutionUpdateAcceptedEventAttributes is { } accepted)
                {
                    foundUpdateAccepted = true;
                }
                if (evt.WorkflowExecutionUpdateCompletedEventAttributes is { } completed)
                {
                    Assert.Equal("Intentional update application failure", completed.Outcome?.Failure?.Message);
                    foundUpdateFailed = true;
                }
            }
            Assert.True(foundUpdateAccepted);
            Assert.True(foundUpdateFailed);

            // Update invalid operation after accepted - fails workflow task
            await handle.StartUpdateAsync(
                wf => wf.DoUpdateOneParamNoResponseAsync("update-invalid-operation-new-task"),
                new(WorkflowUpdateStage.Accepted));
            await AssertTaskFailureContainsEventuallyAsync(handle, "Intentional update invalid operation");
            // Terminate the handle so it doesn't keep failing
            await handle.TerminateAsync();

            // Update invalid operation same task as accepted - fails workflow task
            handle = await Env.Client.StartWorkflowAsync(
                (UpdateWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Run in background and check task
            _ = Task.Run(() => handle.ExecuteUpdateAsync(wf =>
                wf.DoUpdateOneParamNoResponseAsync("update-invalid-operation-same-task")));
            await AssertTaskFailureContainsEventuallyAsync(
                handle, "Intentional update invalid operation");
            // Terminate the handle so it doesn't keep failing
            await handle.TerminateAsync();

            // Update continue as new
            handle = await Env.Client.StartWorkflowAsync(
                (UpdateWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await handle.StartUpdateAsync(
                wf => wf.DoUpdateOneParamNoResponseAsync("update-continue-as-new"),
                new(WorkflowUpdateStage.Accepted));
            await AssertTaskFailureContainsEventuallyAsync(handle, "Continue as new");
            await handle.TerminateAsync();

            // Fail update outside of "async" task part
            handle = await Env.Client.StartWorkflowAsync(
                (UpdateWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Run in background and check task
            _ = Task.Run(() => handle.ExecuteUpdateAsync(wf => wf.DoUpdateFailOutsideAsync()));
            await AssertTaskFailureContainsEventuallyAsync(
                handle, "Intentional update invalid operation");
            // Terminate the handle so it doesn't keep failing
            await handle.TerminateAsync();

            // TODO(cretz): Test admitted wait stage when implemented server side (waiting on
            // https://github.com/temporalio/temporal/issues/4979)
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Updates_DuplicateMemoized()
    {
        await ExecuteWorkerAsync<UpdateWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (UpdateWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            // Run an update with an ID
            var result1 = await handle.ExecuteUpdateAsync(
                wf => wf.DoUpdateOneParamResponseAsync("first-param"), new(id: "my-update-id"));
            var result2 = await handle.ExecuteUpdateAsync(
                wf => wf.DoUpdateOneParamResponseAsync("second-param"), new(id: "my-update-id"));
            // Confirm that the first result is the same as the second without running (i.e. doesn't
            // return second-param)
            Assert.Equal("one-param-response: first-param", result1);
            Assert.Equal(result1, result2);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Updates_ValidatorCreatesCommands()
    {
        await ExecuteWorkerAsync<UpdateWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (UpdateWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Do an update in the background
            _ = Task.Run(() => handle.ExecuteUpdateAsync(wf => wf.DoUpdateValidatorCommandsAsync()));
            // Confirm task fails
            await AssertTaskFailureContainsEventuallyAsync(
                handle, "Update validator for DoUpdateValidatorCommands created workflow commands");
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Updates_CancelWhileUpdating()
    {
        // TODO(cretz): This is a known server issue that poll does not stop when workflow does
        // await ExecuteWorkerAsync<UpdateWorkflow>(async worker =>
        // {
        //     // Start the workflow
        //     var handle = await Env.Client.StartWorkflowAsync(
        //         (UpdateWorkflow wf) => wf.RunAsync(),
        //         new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
        //     // Start update and wait until waiting
        //     var updateHandle = await handle.StartUpdateAsync(wf => wf.DoUpdateWaitABitAsync());
        //     await AssertMore.EqualEventuallyAsync(true, () => handle.QueryAsync(wf => wf.Waiting));
        //     // Cancel the workflow and wait for update complete
        //     await handle.CancelAsync();
        //     Assert.IsType<CanceledFailureException>((await Assert.ThrowsAsync<WorkflowFailedException>(
        //         () => handle.GetResultAsync())).InnerException);
        //     await updateHandle.GetResultAsync();
        // });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Updates_TerminateWhileUpdating()
    {
        // TODO(cretz): This is a known server issue that poll does not stop when workflow does
        // await ExecuteWorkerAsync<UpdateWorkflow>(async worker =>
        // {
        //     // Start the workflow
        //     var handle = await Env.Client.StartWorkflowAsync(
        //         (UpdateWorkflow wf) => wf.RunAsync(),
        //         new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
        //     // Start update and wait until waiting
        //     var updateHandle = await handle.StartUpdateAsync(wf => wf.DoUpdateWaitABitAsync());
        //     await AssertMore.EqualEventuallyAsync(true, () => handle.QueryAsync(wf => wf.Waiting));
        //     // Cancel the workflow and wait for update complete
        //     await handle.TerminateAsync();
        //     Assert.IsType<TerminatedFailureException>((await Assert.ThrowsAsync<WorkflowFailedException>(
        //         () => handle.GetResultAsync())).InnerException);
        //     await updateHandle.GetResultAsync();
        // });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Updates_RejectsWithNoValidatorOnBadArgument()
    {
        await ExecuteWorkerAsync<UpdateWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (UpdateWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Send untyped update with an int instead of string
            var exc = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() =>
                handle.ExecuteUpdateAsync("some-update-name", new object?[] { 123 }));
            Assert.Contains("failure decoding parameters", exc.InnerException?.Message);
            // Send untyped update with no arguments when one is expected
            exc = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() =>
                handle.ExecuteUpdateAsync("some-update-name", Array.Empty<object?>()));
            Assert.Contains("given 0 parameter(s)", exc.InnerException?.Message);

            // Check history and confirm no update was accepted
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                Assert.Null(evt.WorkflowExecutionUpdateAcceptedEventAttributes);
            }
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Updates_ClientCanceled()
    {
        await ExecuteWorkerAsync<UpdateWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                (UpdateWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            using var updateCancelSource = new CancellationTokenSource();
            // Start update that waits until started and confirm it reached workflow
            var updateTask = Task.Run(
                () => handle.ExecuteUpdateAsync(
                    wf => wf.DoUpdateLongWaitAsync(),
                    new() { Rpc = new() { CancellationToken = updateCancelSource.Token } }));
            await AssertMore.EqualEventuallyAsync(true, () => handle.QueryAsync(wf => wf.Waiting));

            // Cancel and confirm error
            updateCancelSource.Cancel();
            await Assert.ThrowsAsync<WorkflowUpdateRpcTimeoutOrCanceledException>(() => updateTask);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Updates_ClientTimedOut()
    {
        // Don't even need to run worker for this one
        var handle = await Env.Client.StartWorkflowAsync(
            (UpdateWorkflow wf) => wf.RunAsync(),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: $"tq-{Guid.NewGuid()}"));

        // Run update that times out
        await Assert.ThrowsAsync<WorkflowUpdateRpcTimeoutOrCanceledException>(() =>
            handle.ExecuteUpdateAsync(
                wf => wf.DoUpdateLongWaitAsync(),
                new() { Rpc = new() { Timeout = TimeSpan.FromMilliseconds(50) } }));
    }

    [Workflow]
    public class ImmediatelyCompleteUpdateAndWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync() => "workflow-done";

        [WorkflowQuery]
        public string GotUpdate { get; set; } = "no";

        [WorkflowUpdate]
        public async Task<string> UpdateAsync()
        {
            GotUpdate = "yes";
            return "update-done";
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Updates_BeforeWorkflowStart()
    {
        // In order to confirm that all started workflows get updates before the
        // workflow completes, this test will start a workflow and start an
        // update. Only then will it start the worker to process both in the
        // task. The workflow and update should both succeed properly. This also
        // invokes a query to confirm update mutation. We do this with the cache
        // off to confirm replay behavior.

        // Start workflow
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var handle = await Client.StartWorkflowAsync(
            (ImmediatelyCompleteUpdateAndWorkflow wf) => wf.RunAsync(),
            new(id: $"wf-{Guid.NewGuid()}", taskQueue));

        // Execute update in the background
        var updateId = $"upd-{Guid.NewGuid()}";
        var updateTask = Task.Run(() =>
            handle.ExecuteUpdateAsync(wf => wf.UpdateAsync(), new(updateId)));

        // Wait until server says it is admitted
        await AssertMore.EventuallyAsync(async () =>
        {
            try
            {
                var resp = await Client.WorkflowService.PollWorkflowExecutionUpdateAsync(new()
                {
                    Identity = Client.Connection.Options.Identity,
                    Namespace = Client.Options.Namespace,
                    UpdateRef = new()
                    {
                        UpdateId = updateId,
                        WorkflowExecution = new() { WorkflowId = handle.Id },
                    },
                });
                Assert.Equal(UpdateWorkflowExecutionLifecycleStage.Admitted, resp.Stage);
            }
            catch (Exception e)
            {
                // Throw XUnit exception so it doesn't fail this eventually call
                throw new Xunit.Sdk.XunitException("Failed polling", e);
            }
        });

        // Start no-cache worker on the task queue
        await ExecuteWorkerAsync<ImmediatelyCompleteUpdateAndWorkflow>(
            async worker =>
            {
                // Confirm things completed as expected
                Assert.Equal("workflow-done", await handle.GetResultAsync());
                Assert.Equal("update-done", await updateTask);
                Assert.Equal("yes", await handle.QueryAsync(wf => wf.GotUpdate));
            },
            new(taskQueue) { MaxCachedWorkflows = 0 });
    }

    [Workflow]
    public class CurrentBuildIdWorkflow
    {
        [Activity]
        public static string SayHi() => "hi";

        private bool doFinish;

        [WorkflowRun]
        public async Task RunAsync()
        {
            await Workflow.DelayAsync(1);
            if (Workflow.CurrentBuildId == "1.0")
            {
                await Workflow.ExecuteActivityAsync(
                    () => SayHi(),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
            }
            await Workflow.WaitConditionAsync(() => doFinish);
        }

        [WorkflowSignal]
        public async Task Finish() => doFinish = true;

        [WorkflowQuery]
        public string GetBuildId() => Workflow.CurrentBuildId;
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_CurrentBuildId_SetProperly()
    {
        var tq = $"tq-{Guid.NewGuid()}";
        var handle =
            await ExecuteWorkerAsync<CurrentBuildIdWorkflow,
                WorkflowHandle<CurrentBuildIdWorkflow>>(
            async worker =>
            {
                // Start the workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    (CurrentBuildIdWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                Assert.Equal("1.0", await handle.QueryAsync(wf => wf.GetBuildId()));
                return handle;
            },
            new(tq) { BuildId = "1.0" });

        await Env.Client.WorkflowService.ResetStickyTaskQueueAsync(new()
        {
            Namespace = Env.Client.Options.Namespace,
            Execution = new() { WorkflowId = handle.Id },
        });

        await ExecuteWorkerAsync<CurrentBuildIdWorkflow>(
            async worker =>
            {
                Assert.NotNull(handle);
                Assert.Equal("1.0", await handle.QueryAsync(wf => wf.GetBuildId()));
                await handle.SignalAsync(wf => wf.Finish());
                Assert.Equal("1.1", await handle.QueryAsync(wf => wf.GetBuildId()));
                await handle.GetResultAsync();
                Assert.Equal("1.1", await handle.QueryAsync(wf => wf.GetBuildId()));
            },
            new(tq) { BuildId = "1.1" });
    }

    public abstract class FailureTypesWorkflow
    {
        public abstract Task RunAsync(Scenario scenario);

        [WorkflowSignal]
        public Task SignalAsync(Scenario scenario) => ApplyScenario(scenario);

        [WorkflowUpdate]
        public async Task UpdateAsync(Scenario scenario)
        {
            // We have to rollover the task so the task failure isn't treated as
            // non-acceptance
            await Workflow.DelayAsync(1);
            await ApplyScenario(scenario);
        }

        protected static async Task ApplyScenario(Scenario scenario)
        {
            switch (scenario)
            {
                case Scenario.ThrowMyException:
                    throw new MyException();
                case Scenario.CauseNonDeterminism:
                    // Only sleep if not replaying
                    if (!Workflow.Unsafe.IsReplaying)
                    {
                        await Workflow.DelayAsync(1);
                    }
                    break;
                case Scenario.WaitForever:
                    await Workflow.WaitConditionAsync(() => false);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public enum Scenario
        {
            ThrowMyException,
            CauseNonDeterminism,
            WaitForever,
        }

        public class MyException : Exception
        {
            public MyException()
                : base("Intentional exception")
            {
            }
        }
    }

    [Workflow]
    public class FailureTypesUnconfiguredWorkflow : FailureTypesWorkflow
    {
        [WorkflowRun]
        public override Task RunAsync(Scenario scenario) => ApplyScenario(scenario);
    }

    [Workflow(FailureExceptionTypes = new[] { typeof(MyException), typeof(WorkflowNondeterminismException) })]
    public class FailureTypesConfiguredExplicitlyWorkflow : FailureTypesWorkflow
    {
        [WorkflowRun]
        public override Task RunAsync(Scenario scenario) => ApplyScenario(scenario);
    }

    [Workflow(FailureExceptionTypes = new[] { typeof(Exception) })]
    public class FailureTypesConfiguredInheritedWorkflow : FailureTypesWorkflow
    {
        [WorkflowRun]
        public override Task RunAsync(Scenario scenario) => ApplyScenario(scenario);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_FailureTypes_Configured()
    {
        Task AssertScenario<T>(
            bool expectTaskFail,
            string failMessageContains,
            Type? workerLevelFailureExceptionType,
            FailureTypesWorkflow.Scenario? workflowScenario = null,
            FailureTypesWorkflow.Scenario? signalScenario = null,
            FailureTypesWorkflow.Scenario? updateScenario = null)
            where T : FailureTypesWorkflow => ExecuteWorkerAsync<T>(
            async worker =>
            {
                // Start workflow
                var handle = await Client.StartWorkflowAsync<T>(
                    wf => wf.RunAsync(workflowScenario ?? FailureTypesWorkflow.Scenario.WaitForever),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                if (signalScenario is { } signalScenarioNotNull)
                {
                    await handle.SignalAsync(wf => wf.SignalAsync(signalScenarioNotNull));
                }
                if (updateScenario is { } updateScenarioNotNull)
                {
                    // Don't care about handle, we'll re-attach later
                    await handle.StartUpdateAsync(
                        wf => wf.UpdateAsync(updateScenarioNotNull),
                        new(id: "my-update-1", waitForStage: WorkflowUpdateStage.Accepted));
                }

                // Expect a task or exception fail
                if (expectTaskFail)
                {
                    await AssertTaskFailureContainsEventuallyAsync(handle, failMessageContains);
                }
                else
                {
                    // Update does not throw on non-determinism, the workflow does instead
                    var outerExc = await Assert.ThrowsAnyAsync<TemporalException>(
                        () => updateScenario == FailureTypesWorkflow.Scenario.ThrowMyException ?
                            handle.GetUpdateHandle("my-update-1").GetResultAsync() :
                            handle.GetResultAsync());
                    var appExc = Assert.IsType<ApplicationFailureException>(outerExc.InnerException);
                    Assert.Contains(failMessageContains, appExc.Message);
                }
            },
            options: new()
            {
                // Disable cache so non-determinism can happen in same worker
                MaxCachedWorkflows = 0,
                WorkflowFailureExceptionTypes = workerLevelFailureExceptionType == null ?
                    null : new[] { workerLevelFailureExceptionType },
            });

        async Task RunScenario<T>(
            FailureTypesWorkflow.Scenario scenario,
            bool expectTaskFail = false,
            Type? workerLevelFailureExceptionType = null)
            where T : FailureTypesWorkflow
        {
            var failMessageContains = scenario == FailureTypesWorkflow.Scenario.ThrowMyException ?
                "Intentional exception" : "Nondeterminism";

            // Run for workflow, signal, and update
            await AssertScenario<T>(
                expectTaskFail,
                failMessageContains,
                workerLevelFailureExceptionType,
                workflowScenario: scenario);
            await AssertScenario<T>(
                expectTaskFail,
                failMessageContains,
                workerLevelFailureExceptionType,
                signalScenario: scenario);
            await AssertScenario<T>(
                expectTaskFail,
                failMessageContains,
                workerLevelFailureExceptionType,
                updateScenario: scenario);
        }

        // Run all tasks concurrently
        var tasks = new[]
        {
            // When unconfigured completely, confirm task fails for all three interactions
            RunScenario<FailureTypesUnconfiguredWorkflow>(
                scenario: FailureTypesWorkflow.Scenario.ThrowMyException,
                expectTaskFail: true),
            RunScenario<FailureTypesUnconfiguredWorkflow>(
                scenario: FailureTypesWorkflow.Scenario.CauseNonDeterminism,
                expectTaskFail: true),

            // When configured at the worker level explicitly
            RunScenario<FailureTypesUnconfiguredWorkflow>(
                scenario: FailureTypesWorkflow.Scenario.ThrowMyException,
                workerLevelFailureExceptionType: typeof(FailureTypesWorkflow.MyException)),
            RunScenario<FailureTypesUnconfiguredWorkflow>(
                scenario: FailureTypesWorkflow.Scenario.CauseNonDeterminism,
                workerLevelFailureExceptionType: typeof(WorkflowNondeterminismException)),

            // When configured at the worker level inherited
            RunScenario<FailureTypesUnconfiguredWorkflow>(
                scenario: FailureTypesWorkflow.Scenario.ThrowMyException,
                workerLevelFailureExceptionType: typeof(Exception)),
            RunScenario<FailureTypesUnconfiguredWorkflow>(
                scenario: FailureTypesWorkflow.Scenario.CauseNonDeterminism,
                workerLevelFailureExceptionType: typeof(Exception)),

            // When configured at the workflow level explicitly
            RunScenario<FailureTypesConfiguredExplicitlyWorkflow>(
                scenario: FailureTypesWorkflow.Scenario.ThrowMyException),
            RunScenario<FailureTypesConfiguredExplicitlyWorkflow>(
                scenario: FailureTypesWorkflow.Scenario.CauseNonDeterminism),

            // When configured at the workflow level inherited
            RunScenario<FailureTypesConfiguredInheritedWorkflow>(
                scenario: FailureTypesWorkflow.Scenario.ThrowMyException),
            RunScenario<FailureTypesConfiguredInheritedWorkflow>(
                scenario: FailureTypesWorkflow.Scenario.CauseNonDeterminism),
        };
        await Task.WhenAll(tasks);
    }

    [Workflow(FailureExceptionTypes = new[] { typeof(Exception) })]
    public class FailOnBadInputWorkflow
    {
        [WorkflowRun]
        public Task RunAsync(string param) => Task.CompletedTask;
    }

    [Workflow("FailureTypeWorkflow\nWithNewline1", FailureExceptionTypes = new[] { typeof(WorkflowNondeterminismException) })]
    public class FailureTypeWorkflowWithNewline1 : FailureTypesWorkflow
    {
        [WorkflowRun]
        public override Task RunAsync(Scenario scenario) => ApplyScenario(scenario);
    }

    [Workflow("FailureTypeWorkflow\nWithNewline2", FailureExceptionTypes = new[] { typeof(WorkflowNondeterminismException) })]
    public class FailureTypeWorkflowWithNewline2 : FailureTypesWorkflow
    {
        [WorkflowRun]
        public override Task RunAsync(Scenario scenario) => ApplyScenario(scenario);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_FailureTypes_MultipleNonDetWithNewlines()
    {
        var workerOptions = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
        {
            // Disable cache to force replay and non-determinism
            MaxCachedWorkflows = 0,
        };
        workerOptions.AddWorkflow<FailureTypeWorkflowWithNewline1>();
        workerOptions.AddWorkflow<FailureTypeWorkflowWithNewline2>();
        using var worker = new TemporalWorker(Client, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                Client.ExecuteWorkflowAsync(
                    (FailureTypeWorkflowWithNewline1 wf) =>
                        wf.RunAsync(FailureTypesWorkflow.Scenario.CauseNonDeterminism),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
            Assert.Contains(
                "Nondeterminism",
                Assert.IsType<ApplicationFailureException>(wfExc.InnerException).Message);
            wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                Client.ExecuteWorkflowAsync(
                    (FailureTypeWorkflowWithNewline2 wf) =>
                        wf.RunAsync(FailureTypesWorkflow.Scenario.CauseNonDeterminism),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
            Assert.Contains(
                "Nondeterminism",
                Assert.IsType<ApplicationFailureException>(wfExc.InnerException).Message);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_BadInput_CanFailWorkflow()
    {
        await ExecuteWorkerAsync<FailOnBadInputWorkflow>(async worker =>
        {
            var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                Client.ExecuteWorkflowAsync(
                    "FailOnBadInputWorkflow",
                    new object?[] { 1234 },
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
            Assert.Contains(
                "failure decoding parameters",
                Assert.IsType<ApplicationFailureException>(wfExc.InnerException).Message);
        });
    }

    [Workflow]
    public class WaitOnSignalWorkflow
    {
        private bool complete;

        [WorkflowRun]
        public Task RunAsync() => Workflow.WaitConditionAsync(() => complete);

        [WorkflowSignal]
        public async Task CompleteAsync() => complete = true;
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WorkerClientReplacement_UsesNewClient()
    {
        // We are going to create a second ephemeral server and start a workflow on each server.
        // The worker will start with a client on the first, then we'll swap the clients, signal
        // both workflows, and confirm the second workflow completes as expected.
        await using var otherEnv = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync();

        // Start both workflows on different servers
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var handle1 = await Client.StartWorkflowAsync(
            (WaitOnSignalWorkflow wf) => wf.RunAsync(),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue));
        var handle2 = await otherEnv.Client.StartWorkflowAsync(
            (WaitOnSignalWorkflow wf) => wf.RunAsync(),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue));

        // Run the worker on the first env
        await ExecuteWorkerAsync<WaitOnSignalWorkflow>(
        async worker =>
        {
            // Confirm the first workflow has completed a task but not the second workflow
            await AssertMore.HasEventEventuallyAsync(handle1, e => e.WorkflowTaskCompletedEventAttributes != null);
            await foreach (var evt in handle2.FetchHistoryEventsAsync())
            {
                Assert.Null(evt.WorkflowTaskCompletedEventAttributes);
            }

            // Now replace the client
            worker.Client = otherEnv.Client;

            // Signal both which should allow the current poll to wake up and it'll be a task
            // failure when trying to submit that to the new client which is ignored. But also the
            // new client will poll for the new workflow, which we will wait for it to complete.
            await handle1.SignalAsync(wf => wf.CompleteAsync());
            await handle2.SignalAsync(wf => wf.CompleteAsync());

            // Now confirm the other workflow completes
            await handle2.GetResultAsync();
            await handle1.TerminateAsync();
        },
        new(taskQueue)
        {
            MaxCachedWorkflows = 0,
            MaxConcurrentWorkflowTaskPolls = 1,
        });
    }

    [Workflow]
    public class CallWorkflowInWaitConditionWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync() => await Workflow.WaitConditionAsync(
            () => !string.IsNullOrEmpty(Workflow.Info.WorkflowId));
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WaitConditionCallingWorkflow_WorksProperly()
    {
        await ExecuteWorkerAsync<CallWorkflowInWaitConditionWorkflow>(async worker =>
        {
            await Client.ExecuteWorkflowAsync(
                (CallWorkflowInWaitConditionWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
        });
    }

    [Workflow]
    public class WaitConditionExceptionWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync() => await Workflow.WaitConditionAsync(
            () => throw new ApplicationFailureException("Intentional error"));
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WaitConditionExceptionWorkflow_WorksProperly()
    {
        await ExecuteWorkerAsync<WaitConditionExceptionWorkflow>(async worker =>
        {
            var handle = await Client.StartWorkflowAsync(
                (WaitConditionExceptionWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await AssertTaskFailureContainsEventuallyAsync(handle, "Intentional error");
        });
    }

    [Workflow]
    public class NextRetryDelayWorkflow
    {
        [Activity]
        public static void NextRetryDelayActivity()
        {
            throw new ApplicationFailureException(
                "Intentional error", nextRetryDelay: TimeSpan.FromMilliseconds(5));
        }

        [WorkflowRun]
        public async Task RunAsync()
        {
            // Run the activity max attempt two
            await Workflow.ExecuteActivityAsync(
                () => NextRetryDelayActivity(),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromHours(1),
                    RetryPolicy = new() { MaximumAttempts = 2 },
                });
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_NextRetryDelay_SetOnError()
    {
        await ExecuteWorkerAsync<NextRetryDelayWorkflow>(
            async worker =>
            {
                var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        (NextRetryDelayWorkflow wf) => wf.RunAsync(),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
                var exc2 = Assert.IsType<ActivityFailureException>(exc.InnerException);
                var exc3 = Assert.IsType<ApplicationFailureException>(exc2.InnerException);
                Assert.Equal("Intentional error", exc3.Message);
                Assert.Equal(TimeSpan.FromMilliseconds(5), exc3.NextRetryDelay);
            },
            new TemporalWorkerOptions().AddActivity(NextRetryDelayWorkflow.NextRetryDelayActivity));
    }

    [Workflow]
    public class ConditionBounceWorkflow
    {
        private int workflowCounter;
        private int signalCounter;

        [WorkflowRun]
        public async Task RunAsync()
        {
            while (workflowCounter < 5)
            {
                var counterBefore = workflowCounter;
                await Workflow.WaitConditionAsync(() => workflowCounter > counterBefore);
                signalCounter++;
            }
        }

        [WorkflowSignal]
        public async Task DoSignalAsync()
        {
            while (signalCounter < 5)
            {
                workflowCounter++;
                var counterBefore = signalCounter;
                await Workflow.WaitConditionAsync(() => signalCounter > counterBefore);
            }
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_ConditionBounce_ProperlyReschedules()
    {
        await ExecuteWorkerAsync<ConditionBounceWorkflow>(
            async worker =>
            {
                var handle = await Env.Client.StartWorkflowAsync(
                    (ConditionBounceWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await AssertMore.HasEventEventuallyAsync(
                    handle, evt => evt.WorkflowTaskCompletedEventAttributes != null);
                await handle.SignalAsync(wf => wf.DoSignalAsync());
                await handle.GetResultAsync();
            });
    }

    [Fact]
    public async Task ExecuteAsync_InvalidWorker_Fails()
    {
        // Try to run a worker on an invalid namespace
        var options = (TemporalClientOptions)Client.Options.Clone();
        options.Namespace = "does-not-exist";
        var client = new TemporalClient(Client.Connection, options);
        using var worker = new TemporalWorker(
            client,
            new TemporalWorkerOptions("some-task-queue").AddWorkflow<SimpleWorkflow>());
        var err = await Assert.ThrowsAsync<InvalidOperationException>(
            () => worker.ExecuteAsync(() => Task.Delay(2000)));
        Assert.Contains("Worker validation failed", err.Message);
    }

    [Workflow]
    public class CurrentUpdateWorkflow
    {
        private readonly List<Task<string>> pendingGetUpdateIdTasks = new();

        [WorkflowRun]
        public async Task<string[]> RunAsync()
        {
            // Confirm no info
            Assert.Null(Workflow.CurrentUpdateInfo);

            // Wait for all tasks then return full set
            await Workflow.WaitConditionAsync(() => pendingGetUpdateIdTasks.Count == 5);
            var res = await Task.WhenAll(pendingGetUpdateIdTasks);

            // Confirm again null then return
            Assert.Null(Workflow.CurrentUpdateInfo);
            return res;
        }

        [WorkflowUpdate]
        public async Task<string> DoUpdateAsync()
        {
            Assert.Equal("DoUpdate", Workflow.CurrentUpdateInfo?.Name);
            // Check that the simple helper awaited has the ID
            Assert.Equal(Workflow.CurrentUpdateInfo?.Id, await GetUpdateIdAsync());

            // Also schedule the task and wait for it in the main workflow to confirm it still gets
            // the update ID
            pendingGetUpdateIdTasks.Add(Task.Factory.StartNew(() => GetUpdateIdAsync()).Unwrap());

            // Return
            return Workflow.CurrentUpdateInfo?.Id ??
                throw new InvalidOperationException("Missing update");
        }

        [WorkflowUpdateValidator(nameof(DoUpdateAsync))]
        public void ValidateDoUpdate() =>
            Assert.Equal("DoUpdate", Workflow.CurrentUpdateInfo?.Name);

        private async Task<string> GetUpdateIdAsync()
        {
            await Workflow.DelayAsync(1);
            return Workflow.CurrentUpdateInfo?.Id ??
                throw new InvalidOperationException("Missing update");
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_CurrentUpdate_HasInfo()
    {
        await ExecuteWorkerAsync<CurrentUpdateWorkflow>(
            async worker =>
            {
                // Start
                var handle = await Client.StartWorkflowAsync(
                    (CurrentUpdateWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

                // Issue 5 updates concurrently and confirm they have the right IDs
                var expected = new[] { "update1", "update2", "update3", "update4", "update5" };
                var actual = await Task.WhenAll(
                    handle.ExecuteUpdateAsync(wf => wf.DoUpdateAsync(), new("update1")),
                    handle.ExecuteUpdateAsync(wf => wf.DoUpdateAsync(), new("update2")),
                    handle.ExecuteUpdateAsync(wf => wf.DoUpdateAsync(), new("update3")),
                    handle.ExecuteUpdateAsync(wf => wf.DoUpdateAsync(), new("update4")),
                    handle.ExecuteUpdateAsync(wf => wf.DoUpdateAsync(), new("update5")));
                Assert.Equal(
                    new[] { "update1", "update2", "update3", "update4", "update5" }.ToHashSet(),
                    actual.ToHashSet());
                Assert.Equal(
                    new[] { "update1", "update2", "update3", "update4", "update5" }.ToHashSet(),
                    (await handle.GetResultAsync()).ToHashSet());
            });
    }

    [Workflow]
    public class CoroutinesAfterCompleteWorkflow
    {
        [Activity]
        public static string SomeActivity(string param) => param;

        private string? completeWorkflowWith;
        private string? completeUpdateWith;
        private bool workflowCompleted;

        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            await Workflow.WaitConditionAsync(() => completeWorkflowWith != null);
            await Workflow.ExecuteLocalActivityAsync(
                () => SomeActivity("activity-from-workflow"),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(30) });
            completeUpdateWith = "complete-update";
            workflowCompleted = true;
            return completeWorkflowWith!;
        }

        [WorkflowUpdate]
        public async Task<string> DoUpdateAsync()
        {
            completeWorkflowWith = "complete-workflow";
            await Workflow.WaitConditionAsync(() => completeUpdateWith != null);
            return completeUpdateWith!;
        }

        [WorkflowSignal]
        public async Task DoErroringSignalAsync(bool waitWorkflowComplete)
        {
            completeWorkflowWith = "complete-workflow";
            if (waitWorkflowComplete)
            {
                await Workflow.WaitConditionAsync(() => workflowCompleted);
            }
            throw new ApplicationFailureException("Intentional error from signal");
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_UpdateAfterComplete_ProcessesProperly()
    {
        // Confirm that an update that returns after the workflow returns properly has its result
        await ExecuteWorkerAsync<CoroutinesAfterCompleteWorkflow>(
            async worker =>
            {
                // Start
                var handle = await Client.StartWorkflowAsync(
                    (CoroutinesAfterCompleteWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                // Start update, wait on workflow and update
                var updateHandle = await handle.StartUpdateAsync(
                    wf => wf.DoUpdateAsync(), new(WorkflowUpdateStage.Accepted));
                Assert.Equal("complete-workflow", await handle.GetResultAsync());
                Assert.Equal("complete-update", await updateHandle.GetResultAsync());
            },
            new TemporalWorkerOptions().AddActivity(CoroutinesAfterCompleteWorkflow.SomeActivity));

        // There have been three stages in .NET SDK lifetime for post-completion commands:
        //   1. pre-dotnet-flag - All code before any command reordering occurred
        //   2. post-dotnet-flag - When .NET applied its own lang flag and did command reordering
        //   3. post-core-flag - Current (as of this writing) core behavior to set flag and reorder
        // The JSON files referenced below are histories of the same run as the workflow run above
        // at each of the three stages. We replay to confirm new code works with old history.
        var replayer = new WorkflowReplayer(
            new WorkflowReplayerOptions().AddWorkflow<CoroutinesAfterCompleteWorkflow>());
        await replayer.ReplayWorkflowAsync(WorkflowHistory.FromJson(
            "some-id", TestUtils.ReadAllFileText("Histories/update-after-complete.pre-dotnet-flag.json")));
        await replayer.ReplayWorkflowAsync(WorkflowHistory.FromJson(
            "some-id", TestUtils.ReadAllFileText("Histories/update-after-complete.post-dotnet-flag.json")));
        await replayer.ReplayWorkflowAsync(WorkflowHistory.FromJson(
            "some-id", TestUtils.ReadAllFileText("Histories/update-after-complete.post-core-flag.json")));
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_MultiComplete_ProcessesProperly()
    {
        // Confirm that multi-complete with success first properly succeeds
        await ExecuteWorkerAsync<CoroutinesAfterCompleteWorkflow>(
            async worker =>
            {
                // Start
                var handle = await Client.StartWorkflowAsync(
                    (CoroutinesAfterCompleteWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                // Issue signal, wait on workflow
                await handle.SignalAsync(wf => wf.DoErroringSignalAsync(true));
                Assert.Equal("complete-workflow", await handle.GetResultAsync());
            },
            new TemporalWorkerOptions().AddActivity(CoroutinesAfterCompleteWorkflow.SomeActivity));

        // Confirm that multi-complete with failure first properly fails
        await ExecuteWorkerAsync<CoroutinesAfterCompleteWorkflow>(
            async worker =>
            {
                // Start
                var handle = await Client.StartWorkflowAsync(
                    (CoroutinesAfterCompleteWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                // Issue signal, wait on workflow
                await handle.SignalAsync(wf => wf.DoErroringSignalAsync(false));
                await Assert.ThrowsAsync<WorkflowFailedException>(() => handle.GetResultAsync());
            },
            new TemporalWorkerOptions().AddActivity(CoroutinesAfterCompleteWorkflow.SomeActivity));

        // There have been three stages in .NET SDK lifetime for post-completion commands:
        //   1. pre-dotnet-flag - All code before any command reordering occurred
        //   2. post-dotnet-flag - When .NET applied its own lang flag and did command reordering
        //   3. post-core-flag - Current (as of this writing) core behavior to set flag and reorder
        // The JSON files referenced below are histories of the same runs as the workflow runs above
        // at each of the three stages. We replay to confirm new code works with old history.
        var replayer = new WorkflowReplayer(
            new WorkflowReplayerOptions().AddWorkflow<CoroutinesAfterCompleteWorkflow>());
        await replayer.ReplayWorkflowAsync(WorkflowHistory.FromJson(
            "some-id", TestUtils.ReadAllFileText("Histories/multi-complete-fail-after.pre-dotnet-flag.json")));
        await replayer.ReplayWorkflowAsync(WorkflowHistory.FromJson(
            "some-id", TestUtils.ReadAllFileText("Histories/multi-complete-fail-after.post-dotnet-flag.json")));
        await replayer.ReplayWorkflowAsync(WorkflowHistory.FromJson(
            "some-id", TestUtils.ReadAllFileText("Histories/multi-complete-fail-after.post-core-flag.json")));
        await replayer.ReplayWorkflowAsync(WorkflowHistory.FromJson(
            "some-id", TestUtils.ReadAllFileText("Histories/multi-complete-fail-before.pre-dotnet-flag.json")));
        await replayer.ReplayWorkflowAsync(WorkflowHistory.FromJson(
            "some-id", TestUtils.ReadAllFileText("Histories/multi-complete-fail-before.post-dotnet-flag.json")));
        await replayer.ReplayWorkflowAsync(WorkflowHistory.FromJson(
            "some-id", TestUtils.ReadAllFileText("Histories/multi-complete-fail-before.post-core-flag.json")));
    }

    public class CallbackActivities
    {
        private readonly Func<string, Task<string>> func;

        public CallbackActivities(Func<string, Task<string>> func) => this.func = func;

        [Activity]
        public Task<string> DoActivityAsync(string param) => func(param);
    }

    [Workflow]
    public class StdlibSemaphoreWorkflow : IDisposable
    {
        private readonly SemaphoreSlim semaSlim = new(1, 1);
        private readonly System.Threading.Semaphore semaHeavy = new(1, 1);

        [WorkflowRun]
        public Task RunAsync() => Workflow.WaitConditionAsync(() => CompletedUpdates.Count == 3);

        [WorkflowQuery]
        public List<string> CompletedUpdates { get; } = new();

        [WorkflowUpdate]
        public async Task UpdateWithAsyncSlim()
        {
            await semaSlim.WaitAsync();
            try
            {
                CompletedUpdates.Add(await Workflow.ExecuteActivityAsync(
                    (CallbackActivities act) => act.DoActivityAsync(Workflow.CurrentUpdateInfo!.Id),
                    new() { StartToCloseTimeout = TimeSpan.FromMinutes(10) }));
            }
            finally
            {
                semaSlim.Release();
            }
        }

        [WorkflowUpdate]
        public async Task UpdateWithSyncSlim()
        {
#pragma warning disable CA1849, VSTHRD103 // Intentionally block thread
            semaSlim.Wait();
#pragma warning restore CA1849, VSTHRD103
            try
            {
                CompletedUpdates.Add(await Workflow.ExecuteActivityAsync(
                    (CallbackActivities act) => act.DoActivityAsync(Workflow.CurrentUpdateInfo!.Id),
                    new() { StartToCloseTimeout = TimeSpan.FromMinutes(10) }));
            }
            finally
            {
                semaSlim.Release();
            }
        }

        [WorkflowUpdate]
        public async Task UpdateWithUsing()
        {
            using var ignore = new Locker(semaSlim);
            CompletedUpdates.Add(await Workflow.ExecuteActivityAsync(
                (CallbackActivities act) => act.DoActivityAsync(Workflow.CurrentUpdateInfo!.Id),
                new() { StartToCloseTimeout = TimeSpan.FromMinutes(10) }));
        }

        [WorkflowUpdate]
        public async Task UpdateWithHeavy()
        {
            semaHeavy.WaitOne();
            try
            {
                CompletedUpdates.Add(await Workflow.ExecuteActivityAsync(
                    (CallbackActivities act) => act.DoActivityAsync(Workflow.CurrentUpdateInfo!.Id),
                    new() { StartToCloseTimeout = TimeSpan.FromMinutes(10) }));
            }
            finally
            {
                semaHeavy.Release();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            semaSlim.Dispose();
            semaHeavy.Dispose();
        }

        private class Locker : IDisposable
        {
            private readonly SemaphoreSlim sema;

            public Locker(SemaphoreSlim sema)
            {
                this.sema = sema;
                sema.Wait();
            }

            public void Dispose() => sema.Release();
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_StdlibSemaphore_AsyncSlimWorks()
    {
        var complete0 = new TaskCompletionSource<string>();
        var complete1 = new TaskCompletionSource<string>();
        var complete2 = new TaskCompletionSource<string>();
        var acts = new CallbackActivities(async param =>
        {
            switch (param)
            {
                case "update-0":
                    return await complete0.Task;
                case "update-1":
                    return await complete1.Task;
                case "update-2":
                    return await complete2.Task;
                default:
                    throw new ArgumentException("Bad param");
            }
        });
        await ExecuteWorkerAsync<StdlibSemaphoreWorkflow>(
            async worker =>
            {
                // Start
                var handle = await Client.StartWorkflowAsync(
                    (StdlibSemaphoreWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                // Start 3 updates
                await handle.StartUpdateAsync(
                    wf => wf.UpdateWithAsyncSlim(),
                    new(id: "update-0", waitForStage: WorkflowUpdateStage.Accepted));
                await handle.StartUpdateAsync(
                    wf => wf.UpdateWithAsyncSlim(),
                    new(id: "update-1", waitForStage: WorkflowUpdateStage.Accepted));
                await handle.StartUpdateAsync(
                    wf => wf.UpdateWithAsyncSlim(),
                    new(id: "update-2", waitForStage: WorkflowUpdateStage.Accepted));
                // Complete the tasks out of order but confirm the results are in order
                complete2.SetResult("done-2");
                complete1.SetResult("done-1");
                // Sleep a tad
                await Task.Delay(100);
                complete0.SetResult("done-0");
                await handle.GetResultAsync();
                Assert.Equal(
                    new List<string> { "done-0", "done-1", "done-2" },
                    await handle.QueryAsync(wf => wf.CompletedUpdates));
            },
            new TemporalWorkerOptions().AddAllActivities(acts));
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_StdlibSemaphore_NonAsyncDeadlocks()
    {
        async Task AssertDeadlocks(Expression<Func<StdlibSemaphoreWorkflow, Task>> updateExpr)
        {
            // Since this is just going to deadlock forever, we need to run the worker in the
            // background
            var handleCompletion = new TaskCompletionSource<WorkflowHandle>();
            _ = Task.Run(() => ExecuteWorkerAsync<StdlibSemaphoreWorkflow>(
                async worker =>
                {
                    // Start
                    var handle = await Client.StartWorkflowAsync(
                        (StdlibSemaphoreWorkflow wf) => wf.RunAsync(),
                        new($"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                    await AssertMore.HasEventEventuallyAsync(
                        handle, evt => evt.WorkflowTaskCompletedEventAttributes != null);

                    // Run two updates in background
                    _ = Task.Run(() =>
                        handle.StartUpdateAsync(
                            updateExpr,
                            new(id: "update-0", waitForStage: WorkflowUpdateStage.Accepted)));
                    _ = Task.Run(() =>
                        handle.StartUpdateAsync(
                            updateExpr,
                            new(id: "update-1", waitForStage: WorkflowUpdateStage.Accepted)));

                    // Send handle back to check
                    handleCompletion.SetResult(handle);
                    // This will never complete
                    await handle.GetResultAsync();
                },
                new TemporalWorkerOptions().AddAllActivities(new CallbackActivities(async ignore =>
                {
                    await Task.Delay(Timeout.Infinite, ActivityExecutionContext.Current.CancellationToken);
                    return "never-reached";
                }))));

            // Wait for deadlock, ignore the hanging worker
            await AssertTaskFailureContainsEventuallyAsync(
                await handleCompletion.Task, "deadlocked");
        }

        // Run the three deadlocking scenarios concurrently since they are slow
        await Task.WhenAll(
            AssertDeadlocks(wf => wf.UpdateWithSyncSlim()),
            AssertDeadlocks(wf => wf.UpdateWithUsing()),
            AssertDeadlocks(wf => wf.UpdateWithHeavy()));
    }

    [Workflow]
    public sealed class SemaphoreWorkflow : IDisposable
    {
        private readonly Semaphore? semaphore;
        private readonly Mutex? mutex;
        private readonly Dictionary<int, CancellationTokenSource> cancellations = new();

        [WorkflowInit]
        public SemaphoreWorkflow(int? semaphoreCount)
        {
            semaphore = semaphoreCount is { } initialCount ? new(initialCount) : null;
            mutex = semaphoreCount == null ? new() : null;
        }

        public void Dispose()
        {
            foreach (var source in cancellations.Values)
            {
                source.Dispose();
            }
        }

        [WorkflowRun]
        public Task RunAsync(int? semaphoreCount) => Workflow.WaitConditionAsync(() => false);

        [WorkflowQuery]
        public List<int> Waiting { get; } = new();

        [WorkflowQuery]
        public List<int> Completed { get; } = new();

        [WorkflowUpdate]
        public async Task UpdateAsync(Update update)
        {
            // Create cancellation token if needed
            CancellationToken? cancellationToken = null;
            if (update.CanCancelManually)
            {
                cancellationToken = (cancellations[update.Number] = new()).Token;
            }

            // Do the wait
            bool acquired;
            try
            {
                if (update.TimeoutMs is { } timeoutMs)
                {
                    if (update.TimeoutTimeSpan != null)
                    {
                        throw new ApplicationFailureException("Cannot have MS and timespan");
                    }
                    acquired = await WaitAsync(timeoutMs, cancellationToken);
                }
                else if (update.TimeoutTimeSpan is { } timeoutTimeSpan)
                {
                    acquired = await WaitAsync(timeoutTimeSpan, cancellationToken);
                }
                else
                {
                    await WaitAsync(cancellationToken);
                    acquired = true;
                }
            }
            catch (TaskCanceledException e)
            {
                throw new ApplicationFailureException("Acquire canceled", e);
            }
            if (!acquired)
            {
                throw new ApplicationFailureException("Failed to acquire");
            }

            // Invoke activity, update completion, and return
            Waiting.Add(update.Number);
            try
            {
                await Workflow.ExecuteActivityAsync(
                    (Activities acts) => acts.DoActivityAsync(update.Number),
                    new() { StartToCloseTimeout = TimeSpan.FromMinutes(30) });
                Completed.Add(update.Number);
            }
            finally
            {
                Release();
                Waiting.Remove(update.Number);
            }
        }

        [WorkflowSignal]
        public async Task CancelUpdateAsync(int number) => cancellations[number].Cancel();

#pragma warning disable VSTHRD110 // Returning tasks is ok
        private Task WaitAsync(CancellationToken? cancellationToken = null) =>
            semaphore?.WaitAsync(cancellationToken) ?? mutex!.WaitOneAsync(cancellationToken);

        private Task<bool> WaitAsync(
            int millisecondsTimeout,
            CancellationToken? cancellationToken = null) =>
            semaphore?.WaitAsync(millisecondsTimeout, cancellationToken) ??
                mutex!.WaitOneAsync(millisecondsTimeout, cancellationToken);

        private Task<bool> WaitAsync(
            TimeSpan timeout,
            CancellationToken? cancellationToken = null) =>
            semaphore?.WaitAsync(timeout, cancellationToken) ??
                mutex!.WaitOneAsync(timeout, cancellationToken);
#pragma warning restore VSTHRD110

        private void Release()
        {
            semaphore?.Release();
            mutex?.ReleaseMutex();
        }

        public record Update(int Number)
        {
            public int? TimeoutMs { get; init; }

            public TimeSpan? TimeoutTimeSpan { get; init; }

            public bool CanCancelManually { get; init; }
        }

        public class Activities
        {
            public Activities(int maxUpdates) =>
                UpdateCompletions = Enumerable.Range(0, maxUpdates).
                    ToDictionary(i => i, i => new TaskCompletionSource());

            public Dictionary<int, TaskCompletionSource> UpdateCompletions { get; }

            [Activity]
            public Task DoActivityAsync(int number) => UpdateCompletions[number].Task;
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Semaphore_MultipleWaiters()
    {
        // This is a basic test of semaphore usage. We will make a semaphore with 2 allowed and 5
        // provided and confirm as we complete some that others can go.
        var acts = new SemaphoreWorkflow.Activities(5);
        await ExecuteWorkerAsync<SemaphoreWorkflow>(
            async worker =>
            {
                // Start semaphore workflow with 5 updates, max 2 at a time
                var handle = await Client.StartWorkflowAsync(
                    (SemaphoreWorkflow wf) => wf.RunAsync(2),
                    new($"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
                    handle.StartUpdateAsync(
                        wf => wf.UpdateAsync(new(i)),
                        new(WorkflowUpdateStage.Accepted))));

                // Confirm there are 2 waiting. We intentionally do this without an "eventually"
                // because the acceptance of the updates is enough to put them all at this expected
                // state.
                var waiting1 = await handle.QueryAsync(wf => wf.Waiting);
                Assert.True(waiting1.Count == 2);

                // Unblock them and confirm the next two are set
                acts.UpdateCompletions[waiting1[0]].SetResult();
                acts.UpdateCompletions[waiting1[1]].SetResult();
                var waiting2 = await AssertMore.EventuallyAsync(async () =>
                {
                    var waiting = await handle.QueryAsync(wf => wf.Waiting);
                    Assert.True(waiting.Count == 2);
                    Assert.DoesNotContain(waiting, waiting1.Contains);
                    return waiting;
                });
                Assert.All(
                    await handle.QueryAsync(wf => wf.Completed),
                    v => Assert.Contains(v, waiting1));

                // Now just complete the rest
                foreach (var comp in acts.UpdateCompletions.Values)
                {
                    if (comp.Task.Status != TaskStatus.RanToCompletion)
                    {
                        comp.SetResult();
                    }
                }

                // Confirm completions
                var completed = await AssertMore.EventuallyAsync(async () =>
                {
                    var completed = await handle.QueryAsync(wf => wf.Completed);
                    Assert.True(completed.Count == 5);
                    return completed;
                });
                Assert.All(completed.GetRange(0, 2), v => Assert.Contains(v, waiting1));
                Assert.DoesNotContain(completed.GetRange(2, 3), waiting1.Contains);
            },
            new TemporalWorkerOptions().AddAllActivities(acts));
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Mutex_MultipleWaiters()
    {
        // This is a basic test of mutex usage. We will make a mutex with 3 updates and confirm
        // that each proceeds one at a time.
        var acts = new SemaphoreWorkflow.Activities(3);
        await ExecuteWorkerAsync<SemaphoreWorkflow>(
            async worker =>
            {
                // Start semaphore workflow with 3 updates
                var handle = await Client.StartWorkflowAsync(
                    (SemaphoreWorkflow wf) => wf.RunAsync(null),
                    new($"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await Task.WhenAll(Enumerable.Range(0, 3).Select(i =>
                    handle.StartUpdateAsync(
                        wf => wf.UpdateAsync(new(i)),
                        new(WorkflowUpdateStage.Accepted))));

                // Confirm only 1 waiting
                var waiting1 = Assert.Single(await handle.QueryAsync(wf => wf.Waiting));

                // Unblock, wait for next one waiting
                acts.UpdateCompletions[waiting1].SetResult();
                var waiting2 = await AssertMore.EventuallyAsync(async () =>
                {
                    var waiting = Assert.Single(await handle.QueryAsync(wf => wf.Waiting));
                    Assert.NotEqual(waiting, waiting1);
                    return waiting;
                });

                // Unblock, wait for final one waiting
                acts.UpdateCompletions[waiting2].SetResult();
                var waiting3 = await AssertMore.EventuallyAsync(async () =>
                {
                    var waiting = Assert.Single(await handle.QueryAsync(wf => wf.Waiting));
                    Assert.NotEqual(waiting, waiting1);
                    Assert.NotEqual(waiting, waiting2);
                    return waiting;
                });

                // Unblock and confirm completions
                acts.UpdateCompletions[waiting3].SetResult();
                var completed = await AssertMore.EventuallyAsync(async () =>
                {
                    var completed = await handle.QueryAsync(wf => wf.Completed);
                    Assert.True(completed.Count == 3);
                    return completed;
                });
                Assert.Contains(0, completed);
                Assert.Contains(1, completed);
                Assert.Contains(2, completed);
            },
            new TemporalWorkerOptions().AddAllActivities(acts));
    }

    [Theory]
    // 0 means non-blocking
    [InlineData(0, true)]
    [InlineData(0, false)]
    // 100ms is a normal timer
    [InlineData(100, true)]
    [InlineData(100, false)]
    public async Task ExecuteWorkflowAsync_Semaphore_Timeout(int timeoutMs, bool useTimeoutMs)
    {
        SemaphoreWorkflow.Update NewNoTimeoutUpdate(int number) => new(number)
        {
            TimeoutMs = useTimeoutMs ? timeoutMs : null,
            TimeoutTimeSpan = useTimeoutMs ? null : TimeSpan.FromMilliseconds(timeoutMs),
        };
        // This tests that non-blocking wait works. We will make a semaphore with 1 allowed and
        // submit a non-blocking update to it. We will do the same then again and confirm the update
        // fails acquiring.
        var acts = new SemaphoreWorkflow.Activities(1);
        await ExecuteWorkerAsync<SemaphoreWorkflow>(
            async worker =>
            {
                try
                {
                    var handle = await Client.StartWorkflowAsync(
                        (SemaphoreWorkflow wf) => wf.RunAsync(1),
                        new($"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                    // This update can start
                    var updateParam1 = NewNoTimeoutUpdate(0);
                    await handle.StartUpdateAsync(
                        wf => wf.UpdateAsync(updateParam1),
                        new(WorkflowUpdateStage.Accepted));
                    Assert.Single(await handle.QueryAsync(wf => wf.Waiting), 0);
                    // This update should fail
                    var updateParam2 = NewNoTimeoutUpdate(1);
                    var exc = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                        () => handle.ExecuteUpdateAsync(wf => wf.UpdateAsync(updateParam2)));
                    Assert.Equal(
                        "Failed to acquire",
                        Assert.IsType<ApplicationFailureException>(exc.InnerException).Message);
                    // Whether the timeout was 0 or not affects whether a timer was started, so
                    // confirm that
                    Assert.Equal(
                        timeoutMs != 0,
                        (await handle.FetchHistoryAsync()).Events.Any(
                            evt => evt.TimerStartedEventAttributes != null));
                }
                finally
                {
                    acts.UpdateCompletions[0].SetResult();
                }
            },
            new TemporalWorkerOptions().AddAllActivities(acts));
    }

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteWorkflowAsync_Semaphore_Cancellation(bool useWorkflowCancellation)
    {
        if (useWorkflowCancellation)
        {
            throw new SkipException(
                "Unable to cancel workflow for this test: https://github.com/temporalio/sdk-core/issues/772");
        }
        // This tests that cancellation works. We will make a semaphore with 1 allowed and submit 2
        // updates. The second update will wait, and we will cancel the wait.
        var acts = new SemaphoreWorkflow.Activities(1);
        await ExecuteWorkerAsync<SemaphoreWorkflow>(
            async worker =>
            {
                try
                {
                    var handle = await Client.StartWorkflowAsync(
                        (SemaphoreWorkflow wf) => wf.RunAsync(1),
                        new($"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                    await handle.StartUpdateAsync(
                        wf => wf.UpdateAsync(new(0)),
                        new(WorkflowUpdateStage.Accepted));
                    var update2 = await handle.StartUpdateAsync(
                        wf => wf.UpdateAsync(new(1) { CanCancelManually = true }),
                        new(WorkflowUpdateStage.Accepted));
                    Assert.Equal(0, (await handle.QueryAsync(wf => wf.Waiting)).First());

                    // Cancel and confirm result
                    if (useWorkflowCancellation)
                    {
                        await handle.CancelAsync();
                    }
                    else
                    {
                        await handle.SignalAsync(wf => wf.CancelUpdateAsync(1));
                    }
                    var exc = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                        () => update2.GetResultAsync());
                    Assert.Equal(
                        "Acquire canceled",
                        Assert.IsType<ApplicationFailureException>(exc.InnerException).Message);
                }
                finally
                {
                    acts.UpdateCompletions[0].SetResult();
                }
            },
            new TemporalWorkerOptions().AddAllActivities(acts));
    }

    [Workflow]
    public class UnfinishedHandlersWorkflow
    {
        public enum WorkflowFinish
        {
            Succeed,
            Fail,
            Cancel,
        }

        private bool startedHandler;
        private bool handlerMayReturn;
        private bool handlerFinished;

        public UnfinishedHandlersWorkflow()
        {
            // Add manual update/signal handlers
            Workflow.Updates["MyUpdateManual"] = WorkflowUpdateDefinition.CreateWithoutAttribute(
                "MyUpdateManual", DoUpdateOrSignal, null);
            Workflow.Updates["MyUpdateManualAbandon"] = WorkflowUpdateDefinition.CreateWithoutAttribute(
                "MyUpdateManualAbandon", DoUpdateOrSignal, null, HandlerUnfinishedPolicy.Abandon);
            Workflow.Signals["MySignalManual"] = WorkflowSignalDefinition.CreateWithoutAttribute(
                "MySignalManual", DoUpdateOrSignal);
            Workflow.Signals["MySignalManualAbandon"] = WorkflowSignalDefinition.CreateWithoutAttribute(
                "MySignalManualAbandon", DoUpdateOrSignal, HandlerUnfinishedPolicy.Abandon);
        }

        [WorkflowRun]
        public async Task<bool> RunAsync(bool waitAllHandlersFinished, WorkflowFinish finish)
        {
            // Wait for started and finished if requested
            await Workflow.WaitConditionAsync(() => startedHandler);
            if (waitAllHandlersFinished)
            {
                handlerMayReturn = true;
                await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished);
            }

            // Cancel or fail
            if (finish == WorkflowFinish.Cancel)
            {
                await Workflow.ExecuteActivityAsync(
                    (Activities acts) => acts.CancelWorkflowAsync(),
                    new() { StartToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.WaitConditionAsync(() => false);
            }
            else if (finish == WorkflowFinish.Fail)
            {
                throw new ApplicationFailureException("Intentional failure");
            }

            return handlerFinished;
        }

        [WorkflowUpdate]
        public Task MyUpdateAsync() => DoUpdateOrSignal();

        [WorkflowUpdate(UnfinishedPolicy = HandlerUnfinishedPolicy.Abandon)]
        public Task MyUpdateAbandonAsync() => DoUpdateOrSignal();

        [WorkflowSignal]
        public Task MySignalAsync() => DoUpdateOrSignal();

        [WorkflowSignal(UnfinishedPolicy = HandlerUnfinishedPolicy.Abandon)]
        public Task MySignalAbandonAsync() => DoUpdateOrSignal();

        private async Task DoUpdateOrSignal()
        {
            startedHandler = true;
            // Shield from cancellation
            await Workflow.WaitConditionAsync(() => handlerMayReturn, default(CancellationToken));
            handlerFinished = true;
        }

        public class Activities
        {
            private readonly ITemporalClient client;

            public Activities(ITemporalClient client) => this.client = client;

            [Activity]
            public Task CancelWorkflowAsync() =>
                client.GetWorkflowHandle(ActivityExecutionContext.Current.Info.WorkflowId).CancelAsync();
        }
    }

    [Theory]
    [InlineData(UnfinishedHandlersWorkflow.WorkflowFinish.Succeed)]
    [InlineData(UnfinishedHandlersWorkflow.WorkflowFinish.Fail)]
    [InlineData(UnfinishedHandlersWorkflow.WorkflowFinish.Cancel)]
    public async Task ExecuteWorkflowAsync_UnfinishedHandlers_WarnProperly(
        UnfinishedHandlersWorkflow.WorkflowFinish finish)
    {
        async Task AssertWarnings(
            Func<WorkflowHandle<UnfinishedHandlersWorkflow>, Task> interaction,
            bool waitAllHandlersFinished,
            bool shouldWarn,
            bool interactionShouldFailWithNotFound = false)
        {
            // Setup log capture
            var loggerFactory = new TestUtils.LogCaptureFactory(LoggerFactory);
            // Run the workflow
            var acts = new UnfinishedHandlersWorkflow.Activities(Client);
            await ExecuteWorkerAsync<UnfinishedHandlersWorkflow>(
                async worker =>
                {
                    // Start workflow
                    var handle = await Client.StartWorkflowAsync(
                        (UnfinishedHandlersWorkflow wf) => wf.RunAsync(waitAllHandlersFinished, finish),
                        new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                    // Perform interaction
                    try
                    {
                        await interaction(handle);
                        Assert.False(interactionShouldFailWithNotFound);
                    }
                    catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)
                    {
                        Assert.True(interactionShouldFailWithNotFound);
                    }
                    // Wait for workflow completion
                    try
                    {
                        await handle.GetResultAsync();
                        Assert.Equal(UnfinishedHandlersWorkflow.WorkflowFinish.Succeed, finish);
                    }
                    catch (WorkflowFailedException e) when (
                        e.InnerException is ApplicationFailureException appEx &&
                        appEx.Message == "Intentional failure")
                    {
                        Assert.Equal(UnfinishedHandlersWorkflow.WorkflowFinish.Fail, finish);
                    }
                    catch (WorkflowFailedException e) when (
                        e.InnerException is CanceledFailureException)
                    {
                        Assert.Equal(UnfinishedHandlersWorkflow.WorkflowFinish.Cancel, finish);
                    }
                },
                new TemporalWorkerOptions() { LoggerFactory = loggerFactory }.
                    AddAllActivities(acts));
            // Check warnings
            Assert.Equal(
                shouldWarn ? 1 : 0,
                loggerFactory.Logs.
                    Select(e => e.Formatted).
                    // We have to dedupe logs because cancel causes unhandled command which causes
                    // workflow complete to replay
                    Distinct().
                    Count(s => s.Contains("handlers are still running")));
        }

        // All update scenarios
        await AssertWarnings(
            interaction: h => h.ExecuteUpdateAsync(wf => wf.MyUpdateAsync()),
            waitAllHandlersFinished: false,
            shouldWarn: true,
            interactionShouldFailWithNotFound: true);
        await AssertWarnings(
            interaction: h => h.ExecuteUpdateAsync(wf => wf.MyUpdateAsync()),
            waitAllHandlersFinished: true,
            shouldWarn: false);
        await AssertWarnings(
            interaction: h => h.ExecuteUpdateAsync(wf => wf.MyUpdateAbandonAsync()),
            waitAllHandlersFinished: false,
            shouldWarn: false,
            interactionShouldFailWithNotFound: true);
        await AssertWarnings(
            interaction: h => h.ExecuteUpdateAsync(wf => wf.MyUpdateAbandonAsync()),
            waitAllHandlersFinished: true,
            shouldWarn: false);
        await AssertWarnings(
            interaction: h => h.ExecuteUpdateAsync("MyUpdateManual", Array.Empty<object?>()),
            waitAllHandlersFinished: false,
            shouldWarn: true,
            interactionShouldFailWithNotFound: true);
        await AssertWarnings(
            interaction: h => h.ExecuteUpdateAsync("MyUpdateManual", Array.Empty<object?>()),
            waitAllHandlersFinished: true,
            shouldWarn: false);
        await AssertWarnings(
            interaction: h => h.ExecuteUpdateAsync("MyUpdateManualAbandon", Array.Empty<object?>()),
            waitAllHandlersFinished: false,
            shouldWarn: false,
            interactionShouldFailWithNotFound: true);
        await AssertWarnings(
            interaction: h => h.ExecuteUpdateAsync("MyUpdateManualAbandon", Array.Empty<object?>()),
            waitAllHandlersFinished: true,
            shouldWarn: false);

        // All signal scenarios
        await AssertWarnings(
            interaction: h => h.SignalAsync(wf => wf.MySignalAsync()),
            waitAllHandlersFinished: false,
            shouldWarn: true);
        await AssertWarnings(
            interaction: h => h.SignalAsync(wf => wf.MySignalAsync()),
            waitAllHandlersFinished: true,
            shouldWarn: false);
        await AssertWarnings(
            interaction: h => h.SignalAsync(wf => wf.MySignalAbandonAsync()),
            waitAllHandlersFinished: false,
            shouldWarn: false);
        await AssertWarnings(
            interaction: h => h.SignalAsync(wf => wf.MySignalAbandonAsync()),
            waitAllHandlersFinished: true,
            shouldWarn: false);
        await AssertWarnings(
            interaction: h => h.SignalAsync("MySignalManual", Array.Empty<object?>()),
            waitAllHandlersFinished: false,
            shouldWarn: true);
        await AssertWarnings(
            interaction: h => h.SignalAsync("MySignalManual", Array.Empty<object?>()),
            waitAllHandlersFinished: true,
            shouldWarn: false);
        await AssertWarnings(
            interaction: h => h.SignalAsync("MySignalManualAbandon", Array.Empty<object?>()),
            waitAllHandlersFinished: false,
            shouldWarn: false);
        await AssertWarnings(
            interaction: h => h.SignalAsync("MySignalManualAbandon", Array.Empty<object?>()),
            waitAllHandlersFinished: true,
            shouldWarn: false);
    }

    [Workflow]
    public class IdConflictWorkflow
    {
        private string signal = "nothing";

        // Just wait forever
        [WorkflowRun]
        public Task RunAsync() => Workflow.WaitConditionAsync(() => false);

        [WorkflowSignal]
        public async Task TheSignal(string sig)
        {
            signal = sig;
        }

        [WorkflowQuery]
        public string GetSignal() => signal;
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_IdConflictPolicy_ProperlyApplies()
    {
        await ExecuteWorkerAsync<IdConflictWorkflow>(async worker =>
        {
            // Start a workflow
            var handle = await Env.Client.StartWorkflowAsync(
                (IdConflictWorkflow wf) => wf.RunAsync(),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            handle = handle with { RunId = handle.ResultRunId };

            // Confirm another fails by default
            await Assert.ThrowsAsync<WorkflowAlreadyStartedException>(() =>
                Env.Client.StartWorkflowAsync(
                    (IdConflictWorkflow wf) => wf.RunAsync(),
                    new(id: handle.Id, taskQueue: worker.Options.TaskQueue!)));

            // Confirm fails if explicitly given that option
            await Assert.ThrowsAsync<WorkflowAlreadyStartedException>(() =>
                Env.Client.StartWorkflowAsync(
                    (IdConflictWorkflow wf) => wf.RunAsync(),
                    new(id: handle.Id, taskQueue: worker.Options.TaskQueue!)
                    {
                        IdConflictPolicy = WorkflowIdConflictPolicy.Fail,
                    }));
            // signal-with-start does not allow fail policy

            // Confirm gives back same handle if requested
            var newHandle = await Env.Client.StartWorkflowAsync(
                (IdConflictWorkflow wf) => wf.RunAsync(),
                new(id: handle.Id, taskQueue: worker.Options.TaskQueue!)
                {
                    IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
                });
            newHandle = newHandle with { RunId = newHandle.ResultRunId };
            Assert.Equal(handle.RunId, newHandle.RunId);
            Assert.Equal(WorkflowExecutionStatus.Running, (await handle.DescribeAsync()).Status);
            Assert.Equal(WorkflowExecutionStatus.Running, (await newHandle.DescribeAsync()).Status);
            // Also with signal-with-start
            newHandle = await Env.Client.StartWorkflowAsync(
                (IdConflictWorkflow wf) => wf.RunAsync(),
                new(id: handle.Id, taskQueue: worker.Options.TaskQueue!)
                {
                    StartSignal = "TheSignal",
                    StartSignalArgs = new[] { "hi!" },
                    IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
                });
            newHandle = newHandle with { RunId = newHandle.ResultRunId };
            Assert.Equal(handle.RunId, newHandle.RunId);
            Assert.Equal(WorkflowExecutionStatus.Running, (await handle.DescribeAsync()).Status);
            Assert.Equal(WorkflowExecutionStatus.Running, (await newHandle.DescribeAsync()).Status);

            // Confirm terminates and starts new if requested
            newHandle = await Env.Client.StartWorkflowAsync(
                (IdConflictWorkflow wf) => wf.RunAsync(),
                new(id: handle.Id, taskQueue: worker.Options.TaskQueue!)
                {
                    IdConflictPolicy = WorkflowIdConflictPolicy.TerminateExisting,
                });
            newHandle = newHandle with { RunId = newHandle.ResultRunId };
            Assert.NotEqual(handle.RunId, newHandle.RunId);
            Assert.Equal(WorkflowExecutionStatus.Terminated, (await handle.DescribeAsync()).Status);
            Assert.Equal(WorkflowExecutionStatus.Running, (await newHandle.DescribeAsync()).Status);
            // Also with signal-with-start
            newHandle = await Env.Client.StartWorkflowAsync(
                (IdConflictWorkflow wf) => wf.RunAsync(),
                new(id: handle.Id, taskQueue: worker.Options.TaskQueue!)
                {
                    StartSignal = "TheSignal",
                    StartSignalArgs = new[] { "hi!" },
                    IdConflictPolicy = WorkflowIdConflictPolicy.TerminateExisting,
                });
            newHandle = newHandle with { RunId = newHandle.ResultRunId };
            Assert.NotEqual(handle.RunId, newHandle.RunId);
            Assert.Equal(WorkflowExecutionStatus.Terminated, (await handle.DescribeAsync()).Status);
            Assert.Equal(WorkflowExecutionStatus.Running, (await newHandle.DescribeAsync()).Status);
            // Ensure it actually got the signal this time
            var queryRes = await newHandle.QueryAsync(wf => wf.GetSignal());
            Assert.Equal("hi!", queryRes);
        });
    }

    internal static Task AssertTaskFailureContainsEventuallyAsync(
        WorkflowHandle handle, string messageContains)
    {
        return AssertMore.TaskFailureEventuallyAsync(
            handle,
            attrs => Assert.Contains(messageContains, attrs.Failure?.Message));
    }

    private Task<ValueTuple> ExecuteWorkerAsync<TWorkflow>(
        Func<TemporalWorker, Task> action,
        TemporalWorkerOptions? options = null,
        IWorkerClient? client = null) =>
        ExecuteWorkerAsync<TWorkflow, ValueTuple>(
            async (w) =>
            {
                await action(w);
                return ValueTuple.Create();
            },
            options,
            client);

    private async Task<TResult> ExecuteWorkerAsync<TWorkflow, TResult>(
        Func<TemporalWorker, Task<TResult>> action,
        TemporalWorkerOptions? options = null,
        IWorkerClient? client = null)
    {
        options ??= new();
        options = (TemporalWorkerOptions)options.Clone();
        options.TaskQueue ??= $"tq-{Guid.NewGuid()}";
        options.AddWorkflow<TWorkflow>();
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(client ?? Client, options);
        return await worker.ExecuteAsync(() => action(worker));
    }
}