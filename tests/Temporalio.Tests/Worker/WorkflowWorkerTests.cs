#pragma warning disable CA1724 // Don't care about name conflicts
#pragma warning disable SA1201, SA1204 // We want to have classes near their tests

namespace Temporalio.Tests.Worker;

using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Exceptions;
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
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
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

            // Good
            TaskFactoryStartNew,
            TaskStart,
            TaskContinueWith,
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
            Assert.Null(result.ContinuedRunID);
            Assert.Null(result.CronSchedule);
            Assert.Null(result.ExecutionTimeout);
            Assert.Equal(worker.Client.Options.Namespace, result.Namespace);
            Assert.Null(result.Parent);
            Assert.Null(result.RetryPolicy);
            Assert.Equal(handle.ResultRunID, result.RunID);
            Assert.Null(result.RunTimeout);
            Assert.InRange(
                result.StartTime,
                DateTime.UtcNow - TimeSpan.FromMinutes(5),
                DateTime.UtcNow + TimeSpan.FromMinutes(5));
            Assert.Equal(worker.Options.TaskQueue, result.TaskQueue);
            // TODO(cretz): Can assume default 10 in all test servers?
            Assert.Equal(TimeSpan.FromSeconds(10), result.TaskTimeout);
            Assert.Equal(handle.ID, result.WorkflowID);
            Assert.Equal("InfoWorkflow", result.WorkflowType);
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
    public async Task ExecuteWorkflowAsync_TimerCancelBefore_ProperlyCancelled()
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
    public async Task ExecuteWorkflowAsync_TimerCancelAfter_ProperlyCancelled()
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
    public async Task ExecuteWorkflowAsync_Cancel_ProperlyCancelled()
    {
        Task AssertProperlyCancelled(
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
                        await AssertStartedEventuallyAsync(handle);
                    }
                    await handle.CancelAsync();
                    var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                        () => handle.GetResultAsync());
                    Assert.IsType<CancelledFailureException>(exc.InnerException);
                    additionalAssertions?.Invoke(handle);
                },
                new TemporalWorkerOptions().
                    AddActivity(CancelWorkflow.SwallowCancelActivityAsync).
                    AddWorkflow<CancelWorkflow.SwallowCancelChildWorkflow>());

        // TODO(cretz): wait condition, external signal, etc
        await AssertProperlyCancelled(CancelWorkflow.Scenario.Timer);
        await AssertProperlyCancelled(
            CancelWorkflow.Scenario.Activity, _ => CancelWorkflow.ActivityStarted!.Task);
        await AssertProperlyCancelled(
            CancelWorkflow.Scenario.LocalActivity, _ => CancelWorkflow.ActivityStarted!.Task);
        await AssertProperlyCancelled(
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
                        await AssertStartedEventuallyAsync(handle);
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
            // Non-existent query
            var exc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                () => handle.QueryAsync<string>("some-query", new[] { "some-arg" }));
            Assert.Contains(
                "known queries: [custom-name QueryFail QueryMakingCommands QuerySimple]",
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
        var workflowID = string.Empty;
        var taskQueue = string.Empty;
        var loggerFactory = new TestUtils.LogCaptureFactory(LoggerFactory);
        await ExecuteWorkerAsync<MiscHelpersWorkflow>(
            async worker =>
            {
                // Start the workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    (MiscHelpersWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                workflowID = handle.ID;
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
                    workflowID).QueryAsync(wf => wf.GetEventsForIsReplaying());
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
            await AssertStartedEventuallyAsync(handle);
            // Cancel workflow and confirm it does
            await handle.CancelAsync();
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => handle.GetResultAsync());
            Assert.IsType<CancelledFailureException>(exc.InnerException);
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
        public async Task RunAsync() => Thread.Sleep(4000);
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
                kvp => kvp.Key, kvp => (object)kvp.Value.ToValue<string>()));
            // Wait, then update and check
            await Workflow.WaitConditionAsync(() => proceed);
            Workflow.UpsertMemo(MemoUpdates);
            Assert.Equal(MemoUpdated, Workflow.Memo.ToDictionary(
                kvp => kvp.Key, kvp => (object)kvp.Value.ToValue<string>()));
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
        public async Task<IList<string>> RunAsync(IList<string> pastRunIDs)
        {
            // Check memo and retry policy
            Assert.Equal(pastRunIDs.Count, Workflow.Memo["PastRunIDCount"].ToValue<int>());
            Assert.Equal(pastRunIDs.Count + 1000, Workflow.Info.RetryPolicy?.MaximumAttempts);

            if (pastRunIDs.Count == 5)
            {
                return pastRunIDs;
            }
            if (Workflow.Info.ContinuedRunID is string contRunID)
            {
                pastRunIDs.Add(contRunID);
            }
            throw Workflow.CreateContinueAsNewException(
                (ContinueAsNewWorkflow wf) => wf.RunAsync(pastRunIDs),
                new()
                {
                    Memo = new Dictionary<string, object>
                    {
                        ["PastRunIDCount"] = pastRunIDs.Count,
                    },
                    RetryPolicy = new() { MaximumAttempts = pastRunIDs.Count + 1000 },
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
                    Memo = new Dictionary<string, object> { ["PastRunIDCount"] = 0 },
                    RetryPolicy = new() { MaximumAttempts = 1000 },
                });
            var result = await handle.GetResultAsync();
            Assert.Equal(5, result.Count);
            Assert.Equal(handle.FirstExecutionRunID, result[0]);
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
                    ActivityDefinition.Create(ResultMultiArgSync).Name,
                    new object?[] { "9", "10" },
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteLocalActivityAsync(
                    ActivityDefinition.Create(NoResultMultiArgSync).Name,
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
                    ActivityDefinition.Create(ResultMultiArgSync).Name,
                    new object?[] { "9", "10" },
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteActivityAsync(
                    ActivityDefinition.Create(NoResultMultiArgSync).Name,
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
        public static Task RunUntilCancelledAsync() =>
            Task.Delay(Timeout.Infinite, ActivityExecutionContext.Current.CancellationToken);

        [WorkflowRun]
        public async Task RunAsync(bool local)
        {
            // Timeout after 10ms w/ no retry
            if (local)
            {
                await Workflow.ExecuteLocalActivityAsync(
                    () => RunUntilCancelledAsync(),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromMilliseconds(10),
                        RetryPolicy = new() { MaximumAttempts = 1 },
                    });
            }
            else
            {
                await Workflow.ExecuteActivityAsync(
                    () => RunUntilCancelledAsync(),
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
            new TemporalWorkerOptions().AddActivity(TimeoutActivityWorkflow.RunUntilCancelledAsync));
    }

    [Workflow]
    public class CancelActivityWorkflow
    {
        [Activity]
        public static Task RunUntilCancelledAsync() =>
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
                    () => RunUntilCancelledAsync(),
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
                    () => RunUntilCancelledAsync(),
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
                Assert.IsType<CancelledFailureException>(actExc.InnerException);

                // Cancel before start
                wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        (CancelActivityWorkflow wf) => wf.RunAsync(new(local, true)),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                        {
                            // Lowering for quicker LA result
                            TaskTimeout = TimeSpan.FromSeconds(2),
                        }));
                var cancelExc = Assert.IsType<CancelledFailureException>(wfExc.InnerException);
                Assert.Contains("cancelled before scheduled", cancelExc.Message);
            },
            new TemporalWorkerOptions().AddActivity(CancelActivityWorkflow.RunUntilCancelledAsync));
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
                WorkflowDefinition.Create(typeof(ResultMultiArg)).Name,
                new object?[] { "5", "6" },
                new() { RunTimeout = TimeSpan.FromHours(1) }));
            await Workflow.ExecuteChildWorkflowAsync(
                WorkflowDefinition.Create(typeof(NoResultMultiArg)).Name,
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
                Assert.IsType<CancelledFailureException>(childExc.InnerException);

                // Cancel before start
                wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        (CancelChildWorkflow wf) => wf.RunAsync(true),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
                var cancelExc = Assert.IsType<CancelledFailureException>(wfExc.InnerException);
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
        public string? ChildID() => child?.ID;
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
                var childID = await AssertMore.EventuallyAsync(async () =>
                {
                    var childID = await handle.QueryAsync(wf => wf.ChildID());
                    Assert.NotNull(childID);
                    return childID!;
                });

                // Signal and wait for signal received
                await handle.SignalAsync(wf => wf.SignalChildAsync("some value"));
                await AssertMore.EqualEventuallyAsync(
                    "some value",
                    () => Env.Client.GetWorkflowHandle<SignalChildWorkflow.ChildWorkflow>(childID).
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
                (ChildWorkflow wf) => wf.RunAsync(), new() { ID = handle.ID });
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
        public Task SignalExternalAsync(string otherID) =>
            Workflow.GetExternalWorkflowHandle<OtherWorkflow>(otherID).SignalAsync(
                wf => wf.SignalAsync("external signal"));

        [WorkflowSignal]
        public Task CancelExternalAsync(string otherID) =>
            Workflow.GetExternalWorkflowHandle(otherID).CancelAsync();
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
                await AssertStartedEventuallyAsync(otherHandle);

                // Start primary workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    (ExternalWorkflow wf) => wf.RunAsync(),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await AssertStartedEventuallyAsync(handle);

                // Send a signal and confirm received
                await handle.SignalAsync(wf => wf.SignalExternalAsync(otherHandle.ID));
                await AssertMore.EqualEventuallyAsync(
                    "external signal",
                    () => otherHandle.QueryAsync(wf => wf.LastSignal()));

                // Cancel and confirm cancelled
                await handle.SignalAsync(wf => wf.CancelExternalAsync(otherHandle.ID));
                await AssertMore.EventuallyAsync(async () =>
                {
                    var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                        otherHandle.GetResultAsync());
                    Assert.IsType<CancelledFailureException>(exc.InnerException);
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

    [Fact(Skip = "TODO(cretz): Current local server doesn't support async metadata, fix with https://github.com/temporalio/sdk-dotnet/issues/50")]
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
        var prePatchID = $"workflow-{Guid.NewGuid()}";
        await ExecuteWorkerAsync<PatchWorkflowBase.PrePatchWorkflow>(
            async worker =>
            {
                Assert.Equal("pre-patch", await ExecuteWorkflowAsync(prePatchID));
            },
            workerOptions);

        // Patch workflow and confirm pre-patch and patched work
        var patchedID = $"workflow-{Guid.NewGuid()}";
        await ExecuteWorkerAsync<PatchWorkflowBase.PatchWorkflow>(
            async worker =>
            {
                Assert.Equal("post-patch", await ExecuteWorkflowAsync(patchedID));
                Assert.Equal("pre-patch", await QueryWorkflowAsync(prePatchID));
            },
            workerOptions);

        // Deprecate patch and confirm patched and deprecated work, but not pre-patch
        var deprecatePatchID = $"workflow-{Guid.NewGuid()}";
        await ExecuteWorkerAsync<PatchWorkflowBase.DeprecatePatchWorkflow>(
            async worker =>
            {
                Assert.Equal("post-patch", await ExecuteWorkflowAsync(deprecatePatchID));
                Assert.Equal("post-patch", await QueryWorkflowAsync(patchedID));
                var exc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                    () => QueryWorkflowAsync(prePatchID));
                Assert.Contains("Nondeterminism", exc.Message);
            },
            workerOptions);

        // Remove patch and confirm post patch and deprecated work, but not pre-patch or patched
        var postPatchPatchID = $"workflow-{Guid.NewGuid()}";
        await ExecuteWorkerAsync<PatchWorkflowBase.PostPatchWorkflow>(
            async worker =>
            {
                Assert.Equal("post-patch", await ExecuteWorkflowAsync(postPatchPatchID));
                Assert.Equal("post-patch", await QueryWorkflowAsync(deprecatePatchID));
                var exc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                    () => QueryWorkflowAsync(prePatchID));
                Assert.Contains("Nondeterminism", exc.Message);
                // TODO(cretz): This was causing a core panic which may now be fixed, but other
                // issue is still causing entire test to be skipped
                // exc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                //     () => QueryWorkflowAsync(patchedID));
                // Assert.Contains("Nondeterminism", exc.Message);
            },
            workerOptions);
    }

    private async Task ExecuteWorkerAsync<TWf>(
        Func<TemporalWorker, Task> action, TemporalWorkerOptions? options = null)
    {
        options ??= new();
        options = (TemporalWorkerOptions)options.Clone();
        options.TaskQueue ??= $"tq-{Guid.NewGuid()}";
        options.AddWorkflow<TWf>();
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(Client, options);
        await worker.ExecuteAsync(() => action(worker));
    }

    private static Task AssertTaskFailureContainsEventuallyAsync(
        WorkflowHandle handle, string messageContains)
    {
        return AssertTaskFailureEventuallyAsync(
            handle, attrs => Assert.Contains(messageContains, attrs.Failure?.Message));
    }

    private static Task AssertTaskFailureEventuallyAsync(
        WorkflowHandle handle, Action<WorkflowTaskFailedEventAttributes> assert)
    {
        return AssertMore.EventuallyAsync(async () =>
        {
            WorkflowTaskFailedEventAttributes? attrs = null;
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                if (evt.WorkflowTaskFailedEventAttributes != null)
                {
                    attrs = evt.WorkflowTaskFailedEventAttributes;
                }
            }
            Assert.NotNull(attrs);
            assert(attrs!);
        });
    }

    private static Task AssertStartedEventuallyAsync(WorkflowHandle handle)
    {
        return AssertHasEventEventuallyAsync(
            handle, e => e.WorkflowExecutionStartedEventAttributes != null);
    }

    private static async Task AssertChildStartedEventuallyAsync(WorkflowHandle handle)
    {
        // Wait for started
        string? childID = null;
        await AssertHasEventEventuallyAsync(
            handle,
            e =>
            {
                childID = e.ChildWorkflowExecutionStartedEventAttributes?.WorkflowExecution?.WorkflowId;
                return childID != null;
            });
        // Check that a workflow task has completed proving child has really started
        await AssertHasEventEventuallyAsync(
            handle.Client.GetWorkflowHandle(childID!),
            e => e.WorkflowTaskCompletedEventAttributes != null);
    }

    private static Task AssertHasEventEventuallyAsync(
        WorkflowHandle handle, Func<HistoryEvent, bool> predicate)
    {
        return AssertMore.EventuallyAsync(async () =>
        {
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                if (predicate(evt))
                {
                    return;
                }
            }
            Assert.Fail("Event not found");
        });
    }

    // TODO(cretz): Potential features/tests:
    // * IDisposable workflows?
    //   * Otherwise, what if I have a cancellation token source at a high level?
    // * Custom errors
    // * Custom codec
    // * Interceptor
    // * Dynamic activities
    // * Dynamic workflows
    // * Dynamic signals/queries
    // * Tracing
    //   * CorrelationManager?
    //   * EventSource?
    // * "WorkflowRunner" abstraction for trying out sandbox techniques
}