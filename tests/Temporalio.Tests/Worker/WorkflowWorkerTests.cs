#pragma warning disable CA1724 // Don't care about name conflicts
#pragma warning disable CS1998 // Sometimes I wait "async" functions with no await in them
#pragma warning disable SA1201, SA1204 // We want to have classes near their tests

namespace Temporalio.Tests.Worker;

using Temporalio.Api.History.V1;
using Temporalio.Client;
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
        public static readonly SimpleWorkflow Ref = Refs.Create<SimpleWorkflow>();

        [WorkflowRun]
        public Task<string> RunAsync(string name) => Task.FromResult($"Hello, {name}!");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Simple_Succeeds()
    {
        await ExecuteWorkerAsync<SimpleWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync(
                SimpleWorkflow.Ref.RunAsync,
                "Temporal",
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_ManualDefinition_Succeeds()
    {
        using var worker = new TemporalWorker(Client, new()
        {
            TaskQueue = $"tq-{Guid.NewGuid()}",
            AdditionalWorkflowDefinitions =
            {
                WorkflowDefinition.CreateWithoutAttribute("other-name", typeof(SimpleWorkflow)),
            },
        });
        await worker.ExecuteAsync(async () =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync<string>(
                "other-name",
                new[] { "Temporal" },
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Workflow]
    public class WorkflowInitWorkflow
    {
        public static readonly WorkflowInitWorkflow Ref = Refs.Create<WorkflowInitWorkflow>();
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
                WorkflowInitWorkflow.Ref.RunAsync,
                "Temporal",
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Workflow]
    public class WorkflowInitNoParamsWorkflow
    {
        public static readonly WorkflowInitNoParamsWorkflow Ref = Refs.Create<WorkflowInitNoParamsWorkflow>();

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
                WorkflowInitNoParamsWorkflow.Ref.RunAsync,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Workflow]
    public class InvalidTaskCallsWorkflow
    {
        public static readonly InvalidTaskCallsWorkflow Ref = Refs.Create<InvalidTaskCallsWorkflow>();

        [WorkflowRun]
        public async Task RunAsync(string scenario)
        {
            switch (scenario)
            {
                case "Task.Delay":
                    await Task.Delay(5000);
                    break;
                default:
                    throw new NotImplementedException(scenario);
            }
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_InvalidTaskCalls_FailsTask()
    {
        async Task AssertScenarioFailsTask(string scenario, string exceptionContains)
        {
            await ExecuteWorkerAsync<InvalidTaskCallsWorkflow>(async worker =>
            {
                // Start the workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    InvalidTaskCallsWorkflow.Ref.RunAsync,
                    scenario,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

                // Make sure task failure occurs in history eventually
                await AssertTaskFailureContainsEventuallyAsync(handle, exceptionContains);
            });
        }

        await AssertScenarioFailsTask("Task.Delay", "Task.Delay cannot be used in workflows");
        // TODO(cretz): Scenarios to prevent:
        // * Task run/create/etc on different scheduler
        //   * Note Task.Run uses default scheduler by default so we should error here
        //   * Note, ConfigureAwait(false) uses default scheduler too!
        // * Task wait with timeout
        // * CancellationTokenSource.CancelAfter (and all TimerQueueTimer uses)
        // * ThreadPool.QueueUserWorkIterm
        // * Timers - https://learn.microsoft.com/en-us/dotnet/standard/threading/timers
        // * IO
    }

    [Workflow]
    public class InfoWorkflow
    {
        public static readonly InfoWorkflow Ref = Refs.Create<InfoWorkflow>();

        [WorkflowRun]
        public Task<WorkflowInfo> RunAsync() => Task.FromResult(Workflow.Info);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Info_Succeeds()
    {
        await ExecuteWorkerAsync<InfoWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                InfoWorkflow.Ref.RunAsync,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            var result = await handle.GetResultAsync();
            Assert.Equal(1, result.Attempt);
            Assert.Null(result.ContinuedRunID);
            Assert.Null(result.CronSchedule);
            Assert.Null(result.ExecutionTimeout);
            Assert.Equal(worker.Client.Options.Namespace, result.Namespace);
            Assert.Null(result.Parent);
            Assert.Null(result.RawMemo);
            Assert.Null(result.RawSearchAttributes);
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
        public static readonly MultiParamWorkflow Ref = Refs.Create<MultiParamWorkflow>();

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
        public static readonly DefaultParamWorkflow Ref = Refs.Create<DefaultParamWorkflow>();

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
        public static readonly TimerWorkflow Ref = Refs.Create<TimerWorkflow>();

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
            var handle = await Env.Client.StartWorkflowAsync(
                TimerWorkflow.Ref.RunAsync,
                new TimerWorkflow.Input(DelayMS: 100),
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
            var handle = await Env.Client.StartWorkflowAsync(
                TimerWorkflow.Ref.RunAsync,
                new TimerWorkflow.Input(DelayMS: 10000, CancelBefore: true),
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
            var handle = await Env.Client.StartWorkflowAsync(
                TimerWorkflow.Ref.RunAsync,
                new TimerWorkflow.Input(DelayMS: 10000, CancelAfterMS: 100),
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
            var handle = await Env.Client.StartWorkflowAsync(
                TimerWorkflow.Ref.RunAsync,
                new TimerWorkflow.Input(DelayMS: Timeout.Infinite, CancelAfterMS: 100),
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
            var handle = await Env.Client.StartWorkflowAsync(
                TimerWorkflow.Ref.RunAsync,
                new TimerWorkflow.Input(DelayMS: -50),
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
            var handle = await Env.Client.StartWorkflowAsync(
                TimerWorkflow.Ref.RunAsync,
                new TimerWorkflow.Input(DelayMS: 0),
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
        public static readonly CancelWorkflow Ref = Refs.Create<CancelWorkflow>();

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
                case Scenario.WaitAndIgnore:
                    await Task.WhenAny(Workflow.DelayAsync(Timeout.Infinite, Workflow.CancellationToken));
                    break;
                default:
                    throw new NotImplementedException();
            }
            return "done";
        }

        public enum Scenario
        {
            Timer,
            TimerIgnoreCancel,
            WaitAndIgnore,
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Cancel_ProperlyCancelled()
    {
        Task AssertProperlyCancelled(
            CancelWorkflow.Scenario scenario,
            Func<HistoryEvent, bool>? waitForEvent = null,
            Action<WorkflowHandle>? additionalAssertions = null) =>
            ExecuteWorkerAsync<CancelWorkflow>(async worker =>
            {
                var handle = await Env.Client.StartWorkflowAsync(
                    CancelWorkflow.Ref.RunAsync,
                    scenario,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                if (waitForEvent != null)
                {
                    await AssertHasEventEventuallyAsync(handle, waitForEvent);
                }
                else
                {
                    await AssertStartedEventuallyAsync(handle);
                }
                await handle.CancelAsync();
                var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() => handle.GetResultAsync());
                Assert.IsType<CancelledFailureException>(exc.InnerException);
                additionalAssertions?.Invoke(handle);
            });

        // TODO(cretz): activities, child workflows, external signal, etc
        await AssertProperlyCancelled(CancelWorkflow.Scenario.Timer);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Cancel_ProperlyIgnored()
    {
        Task AssertProperlyIgnored(
            CancelWorkflow.Scenario scenario,
            Func<HistoryEvent, bool>? waitForEvent = null,
            Action<WorkflowHandle>? additionalAssertions = null) =>
            ExecuteWorkerAsync<CancelWorkflow>(async worker =>
            {
                var handle = await Env.Client.StartWorkflowAsync(
                    CancelWorkflow.Ref.RunAsync,
                    scenario,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                if (waitForEvent != null)
                {
                    await AssertHasEventEventuallyAsync(handle, waitForEvent);
                }
                else
                {
                    await AssertStartedEventuallyAsync(handle);
                }
                await handle.CancelAsync();
                Assert.Equal("done", await handle.GetResultAsync());
                additionalAssertions?.Invoke(handle);
            });

        // TODO(cretz): Test wait conditions, activities, child workflows, external signal, etc
        await AssertProperlyIgnored(CancelWorkflow.Scenario.TimerIgnoreCancel);
        await AssertProperlyIgnored(CancelWorkflow.Scenario.WaitAndIgnore);
    }

    [Workflow]
    public class AssertWorkflow
    {
        public static readonly AssertWorkflow Ref = Refs.Create<AssertWorkflow>();

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
                    AssertWorkflow.Ref.RunAsync,
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
        public static readonly SignalWorkflow Ref = Refs.Create<SignalWorkflow>();
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
                SignalWorkflow.Ref.RunAsync,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                {
                    StartSignal = "Signal1",
                    StartSignalArgs = new[] { "signalval1" },
                });
            // Add one w/ custom name
            await handle.SignalAsync(SignalWorkflow.Ref.SignalCustomAsync, "signalval2");
            // Add a couple more to be buffered
            await handle.SignalAsync("latesig1", new[] { "signalval3" });
            await handle.SignalAsync("latesig2", new[] { "signalval4" });
            // Add the signal handlers
            await handle.SignalAsync(
                SignalWorkflow.Ref.AddSignalHandlersAsync,
                new[] { "latesig1", "latesig2", "latesig3" });
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
                await handle.QueryAsync(SignalWorkflow.Ref.Events));

            // Now send a signal that fails the entire workflow
            await handle.SignalAsync(SignalWorkflow.Ref.FailWorkflowAsync);
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => handle.GetResultAsync());
            var exc2 = Assert.IsType<ApplicationFailureException>(exc.InnerException);
            Assert.Equal("Oh no", exc2.Message);
        });
    }

    [Workflow]
    public class QueryWorkflow
    {
        public static readonly QueryWorkflow Ref = Refs.Create<QueryWorkflow>();

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
                QueryWorkflow.Ref.RunAsync,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Basic queries
            Assert.Equal(
                "QuerySimple: foo",
                await handle.QueryAsync(QueryWorkflow.Ref.QuerySimple, "foo"));
            Assert.Equal(
                "QueryCustom: bar",
                await handle.QueryAsync(QueryWorkflow.Ref.QueryCustom, "bar"));
            // Non-existent query
            var exc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                () => handle.QueryAsync<string>("some-query", new[] { "some-arg" }));
            Assert.Contains(
                "known queries: [custom-name QueryFail QueryMakingCommands QuerySimple]",
                exc.Message);
            // Add that non-existent query then try again
            await handle.SignalAsync(QueryWorkflow.Ref.AddQueryHandlerAsync, "some-query");
            Assert.Equal(
                "AddQueryHandler: some-query - some-arg",
                await handle.QueryAsync<string>("some-query", new[] { "some-arg" }));
            // Query fail
            var exc2 = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                () => handle.QueryAsync(QueryWorkflow.Ref.QueryFail));
            Assert.Equal("Query fail", exc2.Message);
            // Make commands in a query
            var exc3 = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                () => handle.QueryAsync(QueryWorkflow.Ref.QueryMakingCommands));
            Assert.Contains("created workflow commands", exc3.Message);
        });
    }

    [Workflow]
    public class MiscHelpersWorkflow
    {
        public static readonly MiscHelpersWorkflow Ref = Refs.Create<MiscHelpersWorkflow>();

        private static readonly List<bool> EventsForIsReplaying = new();

        private string? completeWorkflow;

        [WorkflowRun]
        public async Task<string> RunAsync()
        {
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
        public bool[] GetEventsForIsReplaying() => EventsForIsReplaying.ToArray();
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_MiscHelpers_Succeed()
    {
        // Run one worker doing test
        var workflowID = string.Empty;
        var taskQueue = string.Empty;
        await ExecuteWorkerAsync<MiscHelpersWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                MiscHelpersWorkflow.Ref.RunAsync,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            workflowID = handle.ID;
            taskQueue = worker.Options.TaskQueue!;
            Assert.InRange(
                await handle.QueryAsync(MiscHelpersWorkflow.Ref.CurrentTime),
                DateTime.UtcNow - TimeSpan.FromMinutes(5),
                DateTime.UtcNow + TimeSpan.FromMinutes(5));
            Assert.InRange(
                await handle.QueryAsync(MiscHelpersWorkflow.Ref.Random, 3), 0, 2);
            // Mark workflow complete and wait on result
            await handle.SignalAsync(MiscHelpersWorkflow.Ref.CompleteWorkflow, "done!");
            Assert.Equal("done!", await handle.GetResultAsync());
        });

        // Run the worker again with a query so we can force replay
        await ExecuteWorkerAsync<MiscHelpersWorkflow>(
            async worker =>
            {
                // Now query after close to check that is replaying worked
                var isReplayingValues = await Env.Client.GetWorkflowHandle(workflowID).
                    QueryAsync(MiscHelpersWorkflow.Ref.GetEventsForIsReplaying);
                Assert.Equal(new[] { false, true }, isReplayingValues);
            },
            new(taskQueue: taskQueue));
    }

    [Workflow]
    public class WaitConditionWorkflow
    {
        public static readonly WaitConditionWorkflow Ref = Refs.Create<WaitConditionWorkflow>();

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
                WaitConditionWorkflow.Ref.RunAsync,
                Timeout.Infinite,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Tell to proceed and confirm it does
            await handle.SignalAsync(WaitConditionWorkflow.Ref.ProceedAsync);
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
                WaitConditionWorkflow.Ref.RunAsync,
                Timeout.Infinite,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Cancel and confirm it does
            await handle.SignalAsync(WaitConditionWorkflow.Ref.CancelWaitAsync);
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
                WaitConditionWorkflow.Ref.RunAsync,
                Timeout.Infinite,
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
                WaitConditionWorkflow.Ref.RunAsync,
                10,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Wait for timeout
            Assert.False(await handle.GetResultAsync());
        });
    }

    [Workflow]
    public class DeadlockWorkflow
    {
        public static readonly DeadlockWorkflow Ref = Refs.Create<DeadlockWorkflow>();

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
                    DeadlockWorkflow.Ref.RunAsync,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await AssertTaskFailureContainsEventuallyAsync(handle, "deadlocked");
            },
            // Disable task tracing so we can add delay in there
            new() { DisableWorkflowTaskTracing = true });
    }

    private async Task ExecuteWorkerAsync<TWf>(
        Func<TemporalWorker, Task> action, TemporalWorkerOptions? options = null)
    {
        options ??= new();
        options.TaskQueue ??= $"tq-{Guid.NewGuid()}";
        options.AddWorkflow(typeof(TWf));
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

    // TODO(cretz): Tests needed:
    // * Continue as new
    // * Search attributes and memos
    // * Logging
    // * Stack trace query
    //   * Consider leveraging https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stacktracehiddenattribute in newer versions
    // * Patching
    // * Separate interface from impl
    // * Workflows as records with WorkflowInit
    // * Workflows as structs
    // * IDisposable workflows?
    //   * Otherwise, what if I have a cancellation token source at a high level?
    // * Custom errors
    // * Workflow not registered
    // * Move args like WorkflowStartOptions to WorkflowOptions
    // * Local/non-local activities
    //   * Call single/multiple
    //   * Cancel swallowed/bubbled
    //   * Cancel before started/task
    //   * Timeouts
    //   * Default params
    // * Child workflows
    //   * Blocking/non-blocking
    //   * Call single/multiple
    //   * Cancel swallowed/bubbled
    //   * Cancel before started/task
    //   * Signal
    //   * Failed start (including specific already-started exception)
    // * External workflows
    //   * Cancel
    //   * Signal
    // * Async utilities (e.g. Dataflow)
    // * Custom codec
    // * Interceptor
    // * Dynamic activities
    // * Dynamic workflows
    // * Dynamic signals/queries
    // * Tracing
    //   * CorrelationManager?
    //   * EventSource?
}