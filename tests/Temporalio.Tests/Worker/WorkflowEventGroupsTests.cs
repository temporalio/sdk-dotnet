#pragma warning disable CA1724
#pragma warning disable CA1849, VSTHRD103

namespace Temporalio.Tests.Worker;

using System.Text;
using Google.Protobuf;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Activities;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;
using Temporalio.Api.Sdk.V1;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class WorkflowEventGroupsTests : WorkflowEnvironmentTestBase
{
    public WorkflowEventGroupsTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    private static readonly TimeSpan ActTimeout = TimeSpan.FromSeconds(10);

    public class EventGroupActivities
    {
        private int failFirstAttempts;

        [Activity]
        public string Noop() => "ok";

        [Activity]
        public string Control(string value) => value;

        [Activity]
        public async Task SleepAsync() =>
            await Task.Delay(TimeSpan.FromSeconds(5), ActivityExecutionContext.Current.CancellationToken);

        [Activity]
        public void FailFirst()
        {
            if (Interlocked.Increment(ref failFirstAttempts) == 1)
            {
                throw new ApplicationFailureException("retry me");
            }
        }
    }

    private static Task<string> ActivityAsync(string activityId, IReadOnlyCollection<EventGroup>? groups = null) =>
        Workflow.ExecuteActivityAsync(
            (EventGroupActivities act) => act.Noop(),
            new()
            {
                StartToCloseTimeout = ActTimeout,
                ActivityId = activityId,
                EventGroups = groups,
            });

    private static async Task SwallowCancelAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception e) when (TemporalException.IsCanceledException(e))
        {
        }
    }

    [Workflow]
    public class NoopChildWorkflow
    {
        [WorkflowRun]
        public Task RunAsync() => Task.CompletedTask;
    }

    [Workflow]
    public class SleepChildWorkflow
    {
        [WorkflowRun]
        public Task RunAsync() => Workflow.DelayAsync(TimeSpan.FromSeconds(5));
    }

    [Workflow]
    public class WaitForSignalChildWorkflow
    {
        private int signals;

        [WorkflowRun]
        public Task RunAsync() => Workflow.WaitConditionAsync(() => signals >= 1);

        [WorkflowSignal]
        public Task NoopAsync()
        {
            signals++;
            return Task.CompletedTask;
        }
    }

    [NexusService]
    public interface IEventGroupsNexusService
    {
        [NexusOperation]
        string Echo();

        [NexusOperation]
        string Sleep();
    }

    [NexusServiceHandler(typeof(IEventGroupsNexusService))]
    public class EventGroupsNexusService
    {
        [NexusOperationHandler]
        public IOperationHandler<NoValue, string> Echo() =>
            OperationHandler.Sync<NoValue, string>((ctx, _) => "ok");

        [NexusOperationHandler]
        public IOperationHandler<NoValue, string> Sleep() =>
            new SleepNexusHandler();

        private class SleepNexusHandler : IOperationHandler<NoValue, string>
        {
            public Task<OperationStartResult<string>> StartAsync(
                OperationStartContext context, NoValue input) =>
                Task.FromResult(OperationStartResult.AsyncResult<string>("sleep-token"));

            public Task CancelAsync(OperationCancelContext context) => Task.CompletedTask;
        }
    }

    [Workflow]
    public class UserProvidedIdsWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var c = Workflow.CreateEventGroup("c-id", new() { Label = "ccc" });
            var d1 = Workflow.CreateEventGroup("d-id", new() { Label = "ddd1" });
            var d2 = Workflow.CreateEventGroup("d-id", new() { Label = "ddd2" });
            var notC = Workflow.CreateEventGroup("not-c-id", new() { Label = "ccc" });
            await ActivityAsync("activity-c", new[] { c });
            await ActivityAsync("activity-d1", new[] { d1 });
            await ActivityAsync("activity-d2", new[] { d2 });
            await ActivityAsync("activity-not-c", new[] { notC });
        }
    }

    [Fact]
    public async Task UserProvidedIds_AreUsedVerbatim()
    {
        await ExecuteAsync<UserProvidedIdsWorkflow>(async worker =>
        {
            var handle = await StartAsync((UserProvidedIdsWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            Assert.Equal(4, EventsOfType(events, EventType.ActivityTaskScheduled).Count);

            AssertMarkerIds(ActivityEvent(events, "activity-c"), LabelId("c-id"));
            Assert.Equal(
                MarkerIds(ActivityEvent(events, "activity-d1")),
                MarkerIds(ActivityEvent(events, "activity-d2")));
            Assert.NotEqual(
                MarkerIds(ActivityEvent(events, "activity-c")),
                MarkerIds(ActivityEvent(events, "activity-not-c")));
        });
    }

    [Workflow]
    public class LabelPayloadWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var a = Workflow.CreateEventGroup("aaa");
            var b = Workflow.CreateEventGroup("bbb", new() { Label = "Label B" });
            await Workflow.ExecuteActivityAsync(
                (EventGroupActivities act) => act.Control("control"),
                new() { StartToCloseTimeout = ActTimeout, ActivityId = "control" });
            await ActivityAsync("activity-a", new[] { a });
            await ActivityAsync("activity-b", new[] { b });
        }
    }

    [Fact]
    public async Task LabelPayload_IsJsonPlainAndOmittedWhenUnset()
    {
        await ExecuteAsync<LabelPayloadWorkflow>(async worker =>
        {
            var handle = await StartAsync((LabelPayloadWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var activityA = ActivityEvent(events, "activity-a");
            var activityB = ActivityEvent(events, "activity-b");
            AssertMarkers(activityA, LabelId("aaa"));
            AssertMarkers(activityB, Label("bbb", "Label B"));
            Assert.Equal(("json/plain", "\"Label B\""), LabelPayloadOf(activityB, "bbb"));
            Assert.False(LabelPayloadSet(activityA, "aaa"));
        });
    }

    [Fact]
    public async Task LabelPayload_UsesDefaultConverterNotWorkerConverter()
    {
        var clientOptions = (TemporalClientOptions)Client.Options.Clone();
        clientOptions.DataConverter = new DataConverter(
            new CustomStringPayloadConverter(), new DefaultFailureConverter());
        var customClient = new TemporalClient(Client.Connection, clientOptions);
        await ExecuteAsync<LabelPayloadWorkflow>(
            async worker =>
            {
                var handle = await customClient.StartWorkflowAsync(
                    (LabelPayloadWorkflow wf) => wf.RunAsync(),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await handle.GetResultAsync();
                var events = await FetchEventsAsync(handle);
                var control = ActivityEvent(events, "control");
                var controlPayload = control.ActivityTaskScheduledEventAttributes.Input.Payloads_.Single();
                Assert.Equal("custom", controlPayload.Metadata["encoding"].ToStringUtf8());
                Assert.Equal(
                    Encoding.UTF8.GetBytes("custom-converter-control"),
                    controlPayload.Data.ToByteArray());
                Assert.Equal(
                    ("json/plain", "\"Label B\""),
                    LabelPayloadOf(ActivityEvent(events, "activity-b"), "bbb"));
                Assert.False(LabelPayloadSet(ActivityEvent(events, "activity-a"), "aaa"));
            },
            client: customClient);
    }

    [Fact]
    public async Task LabelPayload_IsCodecEncodedButIdsAreNot()
    {
        var codec = new WrappingPayloadCodec();
        var clientOptions = (TemporalClientOptions)Client.Options.Clone();
        clientOptions.DataConverter = DataConverter.Default with { PayloadCodec = codec };
        var codecClient = new TemporalClient(Client.Connection, clientOptions);
        await ExecuteAsync<LabelPayloadWorkflow>(
            async worker =>
            {
                var handle = await codecClient.StartWorkflowAsync(
                    (LabelPayloadWorkflow wf) => wf.RunAsync(),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await handle.GetResultAsync();
                var events = await FetchEventsAsync(handle);
                var activityA = ActivityEvent(events, "activity-a");
                var activityB = ActivityEvent(events, "activity-b");
                AssertMarkerIds(activityA, LabelId("aaa"));
                AssertMarkerIds(activityB, LabelId("bbb"));
                Assert.Equal("binary/wrapped", LabelPayloadOf(activityB, "bbb").Encoding);
                var decoded = (await codec.DecodeAsync(new[] { RawLabelPayload(activityB, "bbb") })).Single();
                Assert.Equal("Label B", DataConverter.Default.PayloadConverter.ToValue<string>(decoded));
                Assert.False(LabelPayloadSet(activityA, "aaa"));
            },
            client: codecClient);
    }

    [Workflow]
    public class NestedScopesWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var a = Workflow.CreateEventGroup("aaa");
            var b = Workflow.CreateEventGroup("bbb");
            using (Workflow.WithEventGroups(a))
            {
                await ActivityAsync("a-before");
                using (Workflow.WithEventGroups(b))
                {
                    await ActivityAsync("a-b");
                }
                await ActivityAsync("a-after");
            }
            await ActivityAsync("outside");
        }
    }

    [Fact]
    public async Task NestedScopes_ComposeAndUnwoundAfterDispose()
    {
        await ExecuteAsync<NestedScopesWorkflow>(async worker =>
        {
            var handle = await StartAsync((NestedScopesWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var a = LabelId("aaa");
            var b = LabelId("bbb");
            AssertMarkers(ActivityEvent(events, "a-before"), a);
            AssertMarkers(ActivityEvent(events, "a-b"), a, b);
            AssertMarkers(ActivityEvent(events, "a-after"), a);
            AssertMarkers(ActivityEvent(events, "outside"));
        });
    }

    [Workflow]
    public class ScopeBaselineWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var a = Workflow.CreateEventGroup("aaa");
            using (Workflow.WithEventGroups(a))
            {
                await ActivityAsync("activity");
                await Workflow.DelayAsync(TimeSpan.FromMilliseconds(1));
                await Workflow.StartChildWorkflowAsync(
                    (NoopChildWorkflow wf) => wf.RunAsync(),
                    new() { Id = $"{Workflow.Info.WorkflowId}_child" });
            }
        }
    }

    [Fact]
    public async Task CommandsInAScope_CarryItsMarker()
    {
        await ExecuteAsync<ScopeBaselineWorkflow>(
            async worker =>
            {
                var handle = await StartAsync((ScopeBaselineWorkflow wf) => wf.RunAsync(), worker);
                await handle.GetResultAsync();
                var events = await FetchEventsAsync(handle);
                var a = LabelId("aaa");
                AssertMarkers(ActivityEvent(events, "activity"), a);
                AssertMarkers(SingleEvent(events, EventType.TimerStarted), a);
                AssertMarkers(SingleEvent(events, EventType.StartChildWorkflowExecutionInitiated), a);
            },
            extraWorkflows: new[] { typeof(NoopChildWorkflow) });
    }

    [Workflow]
    public class ReenteredScopeWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var a = Workflow.CreateEventGroup("aaa");
            using (Workflow.WithEventGroups(a))
            {
                await ActivityAsync("a-before");
                using (Workflow.WithEventGroups(a))
                {
                    await ActivityAsync("a-inner");
                }
                await ActivityAsync("a-after");
            }
            await ActivityAsync("outside");
        }
    }

    [Fact]
    public async Task ReenteringAGroup_NestsCorrectly()
    {
        await ExecuteAsync<ReenteredScopeWorkflow>(async worker =>
        {
            var handle = await StartAsync((ReenteredScopeWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var a = LabelId("aaa");
            AssertMarkers(ActivityEvent(events, "a-before"), a);
            AssertMarkers(ActivityEvent(events, "a-inner"), a);
            AssertMarkers(ActivityEvent(events, "a-after"), a);
            AssertMarkers(ActivityEvent(events, "outside"));
        });
    }

    [Workflow]
    public class ConcurrentScopesWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var a = Workflow.CreateEventGroup("aaa");
            var b = Workflow.CreateEventGroup("bbb");
            var c = Workflow.CreateEventGroup("ccc");
            var d = Workflow.CreateEventGroup("ddd");
            var e = Workflow.CreateEventGroup("eee");

            async Task LeftAsync()
            {
                using (Workflow.WithEventGroups(b))
                {
                    using (Workflow.WithEventGroups(a))
                    {
                        using (Workflow.WithEventGroups(c))
                        {
                            await ActivityAsync("b-a-c");
                        }
                        await ActivityAsync("b-a");
                    }
                    await ActivityAsync("b-after-a");
                }
            }

            async Task RightAsync()
            {
                using (Workflow.WithEventGroups(d))
                {
                    using (Workflow.WithEventGroups(a))
                    {
                        using (Workflow.WithEventGroups(e))
                        {
                            await ActivityAsync("d-a-e");
                        }
                        await ActivityAsync("d-a");
                    }
                    await ActivityAsync("d-after-a");
                }
            }

            await Task.WhenAll(LeftAsync(), RightAsync());
            await ActivityAsync("outside");
        }
    }

    [Fact]
    public async Task ConcurrentScopes_ComposeIndependently()
    {
        await ExecuteAsync<ConcurrentScopesWorkflow>(async worker =>
        {
            var handle = await StartAsync((ConcurrentScopesWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            AssertMarkers(ActivityEvent(events, "b-a-c"), LabelId("bbb"), LabelId("aaa"), LabelId("ccc"));
            AssertMarkers(ActivityEvent(events, "b-a"), LabelId("bbb"), LabelId("aaa"));
            AssertMarkers(ActivityEvent(events, "b-after-a"), LabelId("bbb"));
            AssertMarkers(ActivityEvent(events, "d-a-e"), LabelId("ddd"), LabelId("aaa"), LabelId("eee"));
            AssertMarkers(ActivityEvent(events, "d-a"), LabelId("ddd"), LabelId("aaa"));
            AssertMarkers(ActivityEvent(events, "d-after-a"), LabelId("ddd"));
            AssertMarkers(ActivityEvent(events, "outside"));
        });
    }

    [Workflow]
    public class TaskKeepsScopeWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var a = Workflow.CreateEventGroup("aaa");
            var go = false;
            Task task;
            using (Workflow.WithEventGroups(a))
            {
                task = Workflow.RunTaskAsync(async () =>
                {
                    await ActivityAsync("inside-before");
                    await Workflow.WaitConditionAsync(() => go);
                    await ActivityAsync("inside-after");
                });
            }
            go = true;
            await task;
            await ActivityAsync("outside");
        }
    }

    [Fact]
    public async Task TaskStartedInsideScope_KeepsItAfterExit()
    {
        await ExecuteAsync<TaskKeepsScopeWorkflow>(async worker =>
        {
            var handle = await StartAsync((TaskKeepsScopeWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var a = LabelId("aaa");
            AssertMarkers(ActivityEvent(events, "inside-before"), a);
            AssertMarkers(ActivityEvent(events, "inside-after"), a);
            AssertMarkers(ActivityEvent(events, "outside"));
        });
    }

    [Workflow]
    public class TaskCreatedOutsideScopeWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var a = Workflow.CreateEventGroup("aaa");
            var go = false;
            var task = Workflow.RunTaskAsync(async () =>
            {
                await Workflow.WaitConditionAsync(() => go);
                await ActivityAsync("outside-task");
            });
            using (Workflow.WithEventGroups(a))
            {
                go = true;
                await task;
            }
        }
    }

    [Fact]
    public async Task TaskCreatedOutsideScope_DoesNotInheritItWhenResumedInside()
    {
        await ExecuteAsync<TaskCreatedOutsideScopeWorkflow>(async worker =>
        {
            var handle = await StartAsync((TaskCreatedOutsideScopeWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            AssertMarkers(ActivityEvent(await FetchEventsAsync(handle), "outside-task"));
        });
    }

    [Workflow]
    public class ScopeThrowWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var a = Workflow.CreateEventGroup("aaa");
            var b = Workflow.CreateEventGroup("bbb");
            using (Workflow.WithEventGroups(a))
            {
                try
                {
                    using (Workflow.WithEventGroups(b))
                    {
                        await ActivityAsync("a-b");
                        throw new ApplicationFailureException("boom");
                    }
                }
                catch (ApplicationFailureException)
                {
                }
                await ActivityAsync("a-after");
            }
        }
    }

    [Fact]
    public async Task Scope_UnwindsWhenBodyThrows()
    {
        await ExecuteAsync<ScopeThrowWorkflow>(async worker =>
        {
            var handle = await StartAsync((ScopeThrowWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            AssertMarkers(ActivityEvent(events, "a-b"), LabelId("aaa"), LabelId("bbb"));
            AssertMarkers(ActivityEvent(events, "a-after"), LabelId("aaa"));
        });
    }

    [Workflow]
    public class StaticSignalHandlerWorkflow
    {
        private bool done;

        [WorkflowRun]
        public async Task RunAsync()
        {
            await ActivityAsync("from-main-before-signal");
            await Workflow.WaitConditionAsync(() => done);
            await ActivityAsync("from-main-after-signal");
        }

        [WorkflowSignal]
        public async Task MySignalAsync()
        {
            await ActivityAsync("from-static-signal");
            var a = Workflow.CreateEventGroup("aaa");
            using (Workflow.WithEventGroups(a))
            {
                await ActivityAsync("from-static-signal-scoped");
            }
            done = true;
        }
    }

    [Fact]
    public async Task StaticSignalHandler_CarriesImplicitGroup()
    {
        await ExecuteAsync<StaticSignalHandlerWorkflow>(async worker =>
        {
            var handle = await StartAsync((StaticSignalHandlerWorkflow wf) => wf.RunAsync(), worker);
            await handle.SignalAsync(wf => wf.MySignalAsync());
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var signal = EventMarker(SignaledEventIds(events).Single());
            var a = LabelId("aaa");
            AssertMarkers(ActivityEvent(events, "from-static-signal"), signal);
            AssertMarkers(ActivityEvent(events, "from-static-signal-scoped"), signal, a);
            AssertMarkers(ActivityEvent(events, "from-main-before-signal"));
            AssertMarkers(ActivityEvent(events, "from-main-after-signal"));
        });
    }

    [Workflow]
    public class RuntimeSignalHandlerWorkflow
    {
        private bool done;

        [WorkflowRun]
        public async Task RunAsync()
        {
            var outside = Workflow.CreateEventGroup("outside");
            var inside = Workflow.CreateEventGroup("inside");
            using (Workflow.WithEventGroups(outside))
            {
                Workflow.Signals["mySignal"] = WorkflowSignalDefinition.CreateWithoutAttribute(
                    "mySignal",
                    async () =>
                    {
                        await ActivityAsync("from-runtime-signal");
                        using (Workflow.WithEventGroups(inside))
                        {
                            await ActivityAsync("from-runtime-signal-scoped");
                        }
                        done = true;
                    });
                await ActivityAsync("in-outside");
            }
            await ActivityAsync("from-main-before-signal");
            await Workflow.WaitConditionAsync(() => done);
            await ActivityAsync("from-main-after-signal");
        }
    }

    [Fact]
    public async Task RuntimeSignalHandler_DoesNotInheritRegistrationScope()
    {
        await ExecuteAsync<RuntimeSignalHandlerWorkflow>(async worker =>
        {
            var handle = await StartAsync((RuntimeSignalHandlerWorkflow wf) => wf.RunAsync(), worker);
            await handle.SignalAsync("mySignal", Array.Empty<object?>());
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var signal = EventMarker(SignaledEventIds(events).Single());
            AssertMarkers(ActivityEvent(events, "from-runtime-signal"), signal);
            AssertMarkers(ActivityEvent(events, "in-outside"), LabelId("outside"));
            AssertMarkers(ActivityEvent(events, "from-runtime-signal-scoped"), signal, LabelId("inside"));
            AssertMarkers(ActivityEvent(events, "from-main-before-signal"));
            AssertMarkers(ActivityEvent(events, "from-main-after-signal"));
        });
    }

    [Workflow]
    public class BufferedSignalWorkflow
    {
        private bool unblocked;
        private bool handled;

        [WorkflowRun]
        public async Task RunAsync()
        {
            Workflow.Signals["unblock"] = WorkflowSignalDefinition.CreateWithoutAttribute(
                "unblock",
                () =>
                {
                    unblocked = true;
                    return Task.CompletedTask;
                });
            await Workflow.WaitConditionAsync(() => unblocked);
            Workflow.Signals["mySignal"] = WorkflowSignalDefinition.CreateWithoutAttribute(
                "mySignal",
                async () =>
                {
                    await ActivityAsync("from-runtime-signal");
                    handled = true;
                });
            await Workflow.WaitConditionAsync(() => handled);
        }
    }

    [Fact]
    public async Task BufferedSignal_KeepsOriginalImplicitMarker()
    {
        await ExecuteAsync<BufferedSignalWorkflow>(async worker =>
        {
            var handle = await StartAsync((BufferedSignalWorkflow wf) => wf.RunAsync(), worker);
            await handle.SignalAsync("mySignal", Array.Empty<object?>());
            await handle.SignalAsync("unblock", Array.Empty<object?>());
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            AssertMarkers(
                ActivityEvent(events, "from-runtime-signal"),
                EventMarker(SignaledEventIds(events)[0]));
        });
    }

    [Workflow]
    public class CatchAllSignalWorkflow
    {
        private bool done;

        [WorkflowRun]
        public Task RunAsync() => Workflow.WaitConditionAsync(() => done);

        [WorkflowSignal(Dynamic = true)]
        public async Task OnAnySignalAsync(string name, IRawValue[] args)
        {
            await ActivityAsync("from-catch-all-signal");
            done = true;
        }
    }

    [Fact]
    public async Task CatchAllSignalHandler_CarriesSignaledEventMarker()
    {
        await ExecuteAsync<CatchAllSignalWorkflow>(async worker =>
        {
            var handle = await StartAsync((CatchAllSignalWorkflow wf) => wf.RunAsync(), worker);
            await handle.SignalAsync("non-existent-signal", Array.Empty<object?>());
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            AssertMarkers(
                ActivityEvent(events, "from-catch-all-signal"),
                EventMarker(SignaledEventIds(events).Single()));
        });
    }

    [Workflow]
    public class StaticUpdateHandlerWorkflow
    {
        private bool done;

        [WorkflowRun]
        public async Task RunAsync()
        {
            await ActivityAsync("from-main-before-update");
            await Workflow.WaitConditionAsync(() => done);
            await ActivityAsync("from-main-after-update");
        }

        [WorkflowUpdate]
        public async Task MyUpdateAsync()
        {
            await ActivityAsync("from-static-update");
            var inside = Workflow.CreateEventGroup("inside");
            using (Workflow.WithEventGroups(inside))
            {
                await ActivityAsync("from-static-update-scoped");
            }
            done = true;
        }
    }

    [Fact]
    public async Task StaticUpdateHandler_CarriesUpdateId()
    {
        await ExecuteAsync<StaticUpdateHandlerWorkflow>(async worker =>
        {
            var handle = await StartAsync((StaticUpdateHandlerWorkflow wf) => wf.RunAsync(), worker);
            await handle.ExecuteUpdateAsync(wf => wf.MyUpdateAsync(), new("static-update-1"));
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var update = UpdateMarker("static-update-1");
            AssertMarkers(ActivityEvent(events, "from-static-update"), update);
            AssertMarkers(ActivityEvent(events, "from-static-update-scoped"), update, LabelId("inside"));
            AssertMarkers(ActivityEvent(events, "from-main-before-update"));
            AssertMarkers(ActivityEvent(events, "from-main-after-update"));
        });
    }

    [Workflow]
    public class RuntimeUpdateHandlerWorkflow
    {
        private bool done;

        [WorkflowRun]
        public async Task RunAsync()
        {
            var outside = Workflow.CreateEventGroup("outside");
            var inside = Workflow.CreateEventGroup("inside");
            using (Workflow.WithEventGroups(outside))
            {
                Workflow.Updates["myUpdate"] = WorkflowUpdateDefinition.CreateWithoutAttribute(
                    "myUpdate",
                    async () =>
                    {
                        await ActivityAsync("from-runtime-update");
                        using (Workflow.WithEventGroups(inside))
                        {
                            await ActivityAsync("from-runtime-update-scoped");
                        }
                        done = true;
                    });
                await ActivityAsync("in-outside");
            }
            await ActivityAsync("from-main-before-update");
            await Workflow.WaitConditionAsync(() => done);
            await ActivityAsync("from-main-after-update");
        }
    }

    [Fact]
    public async Task RuntimeUpdateHandler_DoesNotInheritRegistrationScope()
    {
        await ExecuteAsync<RuntimeUpdateHandlerWorkflow>(async worker =>
        {
            var handle = await StartAsync((RuntimeUpdateHandlerWorkflow wf) => wf.RunAsync(), worker);
            await AssertMore.HasEventEventuallyAsync(
                handle,
                e => e.ActivityTaskScheduledEventAttributes?.ActivityId == "in-outside");
            await handle.ExecuteUpdateAsync("myUpdate", Array.Empty<object?>(), new("runtime-update-1"));
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var update = UpdateMarker("runtime-update-1");
            AssertMarkers(ActivityEvent(events, "from-runtime-update"), update);
            AssertMarkers(ActivityEvent(events, "in-outside"), LabelId("outside"));
            AssertMarkers(ActivityEvent(events, "from-runtime-update-scoped"), update, LabelId("inside"));
            AssertMarkers(ActivityEvent(events, "from-main-before-update"));
            AssertMarkers(ActivityEvent(events, "from-main-after-update"));
        });
    }

    [Workflow]
    public class CatchAllUpdateWorkflow
    {
        private bool done;

        [WorkflowRun]
        public Task RunAsync() => Workflow.WaitConditionAsync(() => done);

        [WorkflowUpdate(Dynamic = true)]
        public async Task OnAnyUpdateAsync(string name, IRawValue[] args)
        {
            await ActivityAsync("from-catch-all-update");
            done = true;
        }
    }

    [Fact]
    public async Task CatchAllUpdateHandler_CarriesUpdateId()
    {
        await ExecuteAsync<CatchAllUpdateWorkflow>(async worker =>
        {
            var handle = await StartAsync((CatchAllUpdateWorkflow wf) => wf.RunAsync(), worker);
            await handle.ExecuteUpdateAsync("non-existent-update", Array.Empty<object?>(), new("catch-all-update-1"));
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            AssertMarkers(
                ActivityEvent(events, "from-catch-all-update"),
                UpdateMarker("catch-all-update-1"));
        });
    }

    [Workflow]
    public class AggregationWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var a1 = Workflow.CreateEventGroup("aaa");
            var a2 = Workflow.CreateEventGroup("aaa");
            var b1 = Workflow.CreateEventGroup("b-id", new() { Label = "bbb1" });
            var b2 = Workflow.CreateEventGroup("b-id", new() { Label = "bbb2" });
            await ActivityAsync("direct-duplicates", new[] { a2, b1, a1, b1, a2, a1 });
            using (Workflow.WithEventGroups(a1))
            using (Workflow.WithEventGroups(a2))
            using (Workflow.WithEventGroups(b1))
            {
                await ActivityAsync("nested-scopes");
            }
            using (Workflow.WithEventGroups(a1))
            using (Workflow.WithEventGroups(b1))
            {
                await ActivityAsync("scope-and-direct-b", new[] { b1 });
                await ActivityAsync("scope-and-direct-a-b", new[] { b1, a1 });
            }
            await ActivityAsync("same-instance-twice", new[] { a1, a1 });
            await ActivityAsync("same-id-direct", new[] { b1, b2 });
            using (Workflow.WithEventGroups(b1))
            {
                await ActivityAsync("same-id-scope-and-direct", new[] { b2 });
            }
        }
    }

    [Fact]
    public async Task Aggregation_CollapsesDuplicateIds()
    {
        await ExecuteAsync<AggregationWorkflow>(async worker =>
        {
            var handle = await StartAsync((AggregationWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var expected = new[] { LabelId("aaa"), LabelId("b-id") };
            AssertMarkerIds(ActivityEvent(events, "direct-duplicates"), expected);
            AssertMarkerIds(ActivityEvent(events, "nested-scopes"), expected);
            AssertMarkerIds(ActivityEvent(events, "scope-and-direct-b"), expected);
            AssertMarkerIds(ActivityEvent(events, "scope-and-direct-a-b"), expected);
            AssertMarkerIds(ActivityEvent(events, "same-instance-twice"), LabelId("aaa"));
            AssertMarkerIds(ActivityEvent(events, "same-id-direct"), LabelId("b-id"));
            AssertMarkerIds(ActivityEvent(events, "same-id-scope-and-direct"), LabelId("b-id"));
        });
    }

    [Workflow]
    public class TimerCommandsWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var direct = Workflow.CreateEventGroup("direct");
            var scope = Workflow.CreateEventGroup("scope");
            using (Workflow.WithEventGroups(scope))
            {
                await Workflow.DelayWithOptionsAsync(new(TimeSpan.FromMilliseconds(1)) { EventGroups = new[] { direct } });
                await Workflow.WaitConditionWithOptionsAsync(new(() => false, timeout: TimeSpan.FromMilliseconds(1))
                {
                    EventGroups = new[] { direct },
                });
                using var cts = new CancellationTokenSource();
                var longTimer = Workflow.DelayWithOptionsAsync(new(TimeSpan.FromSeconds(60), cancellationToken: cts.Token)
                {
                    EventGroups = new[] { direct },
                });
                await Workflow.DelayAsync(TimeSpan.FromMilliseconds(1));
                cts.Cancel();
                await SwallowCancelAsync(longTimer);
            }
        }
    }

    [Fact]
    public async Task TimerCommands_CarryMarkers()
    {
        await ExecuteAsync<TimerCommandsWorkflow>(async worker =>
        {
            var handle = await StartAsync((TimerCommandsWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var both = new[] { LabelId("direct"), LabelId("scope") };
            var ambient = new[] { LabelId("scope") };
            var timers = EventsOfType(events, EventType.TimerStarted);
            Assert.Equal(4, timers.Count);
            var cancels = EventsOfType(events, EventType.TimerCanceled);
            Assert.Single(cancels);
            AssertMarkers(timers[0], both);
            AssertMarkers(timers[1], both);
            var rest = timers.Skip(2).ToList();
            Assert.Single(rest, t => MarkerIds(t).SequenceEqual(ambient.OrderBy(x => x)));
            Assert.Single(rest, t => MarkerIds(t).SequenceEqual(both.OrderBy(x => x)));
            AssertMarkers(cancels[0], both);
        });
    }

    [Workflow]
    public class MutexAndSemaphoreTimeoutWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var mutex = new Temporalio.Workflows.Mutex();
            await mutex.WaitOneAsync();
            var semaphore = new Temporalio.Workflows.Semaphore(1);
            await semaphore.WaitAsync();
            var scope = Workflow.CreateEventGroup("scope");
            Task<bool> mutexWait;
            Task<bool> semaphoreWait;
            using (Workflow.WithEventGroups(scope))
            {
                // Mutex/Semaphore timeout waits expose no EventGroups argument, so the ambient
                // scope is all they can carry.
                mutexWait = mutex.WaitOneAsync(TimeSpan.FromSeconds(60));
                semaphoreWait = semaphore.WaitAsync(TimeSpan.FromSeconds(60));
            }
            await Workflow.DelayAsync(TimeSpan.FromMilliseconds(1));
            mutex.ReleaseMutex();
            semaphore.Release();
            await mutexWait;
            await semaphoreWait;
        }
    }

    [Fact]
    public async Task MutexAndSemaphoreTimeouts_CarryAmbientMarkers()
    {
        await ExecuteAsync<MutexAndSemaphoreTimeoutWorkflow>(async worker =>
        {
            var handle = await StartAsync((MutexAndSemaphoreTimeoutWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var ambient = new[] { LabelId("scope") };
            var timers = EventsOfType(events, EventType.TimerStarted);
            Assert.Equal(3, timers.Count);
            Assert.Equal(2, timers.Count(t => MarkerIds(t).SequenceEqual(ambient.OrderBy(x => x))));
            Assert.Single(timers, t => MarkerIds(t).Count == 0);
            var cancels = EventsOfType(events, EventType.TimerCanceled);
            Assert.Equal(2, cancels.Count);
            foreach (var cancel in cancels)
            {
                AssertMarkers(cancel, ambient);
            }
        });
    }

    [Workflow]
    public class ActivityCommandsWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var direct = Workflow.CreateEventGroup("direct");
            var scope = Workflow.CreateEventGroup("scope");
            using (Workflow.WithEventGroups(scope))
            {
                await ActivityAsync("activity", new[] { direct });
                using var cts = new CancellationTokenSource();
                var sleeping = Workflow.ExecuteActivityAsync(
                    (EventGroupActivities act) => act.SleepAsync(),
                    new()
                    {
                        StartToCloseTimeout = ActTimeout,
                        ScheduleToStartTimeout = TimeSpan.FromSeconds(10),
                        CancellationType = ActivityCancellationType.TryCancel,
                        CancellationToken = cts.Token,
                        ActivityId = "activity-cancelled-sleep-5s",
                        EventGroups = new[] { direct },
                    });
                await Workflow.DelayAsync(TimeSpan.FromMilliseconds(1));
                cts.Cancel();
                await SwallowCancelAsync(sleeping);
            }
        }
    }

    [Fact]
    public async Task ActivityCommands_CarryMarkers()
    {
        await ExecuteAsync<ActivityCommandsWorkflow>(async worker =>
        {
            var handle = await StartAsync((ActivityCommandsWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var both = new[] { LabelId("direct"), LabelId("scope") };
            AssertMarkers(ActivityEvent(events, "activity"), both);
            AssertMarkers(ActivityEvent(events, "activity-cancelled-sleep-5s"), both);
            AssertMarkers(SingleEvent(events, EventType.ActivityTaskCancelRequested), both);
        });
    }

    [Workflow]
    public class LocalActivityCommandsWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var direct = Workflow.CreateEventGroup("direct");
            var scope = Workflow.CreateEventGroup("scope");
            using (Workflow.WithEventGroups(scope))
            {
                await Workflow.ExecuteLocalActivityAsync(
                    (EventGroupActivities act) => act.Noop(),
                    new()
                    {
                        StartToCloseTimeout = ActTimeout,
                        ActivityId = "local-activity",
                        EventGroups = new[] { direct },
                    });

                var cancelTrigger = Workflow.CreateEventGroup("cancel-trigger");
                var cancelledLa = Workflow.CreateEventGroup("cancelled-la");
                using var cts = new CancellationTokenSource();
                var sleeping = Workflow.ExecuteLocalActivityAsync(
                    (EventGroupActivities act) => act.SleepAsync(),
                    new()
                    {
                        StartToCloseTimeout = ActTimeout,
                        CancellationType = ActivityCancellationType.TryCancel,
                        CancellationToken = cts.Token,
                        ActivityId = "cancelled-local-activity-sleep-5s",
                        EventGroups = new[] { direct, cancelledLa },
                    });
                await Workflow.ExecuteLocalActivityAsync(
                    (EventGroupActivities act) => act.Noop(),
                    new()
                    {
                        StartToCloseTimeout = ActTimeout,
                        ActivityId = "cancel-trigger",
                        EventGroups = new[] { direct, cancelTrigger },
                    });
                cts.Cancel();
                await SwallowCancelAsync(sleeping);

                await Workflow.ExecuteLocalActivityAsync(
                    (EventGroupActivities act) => act.FailFirst(),
                    new()
                    {
                        StartToCloseTimeout = ActTimeout,
                        LocalRetryThreshold = TimeSpan.FromMilliseconds(1),
                        RetryPolicy = new()
                        {
                            InitialInterval = TimeSpan.FromSeconds(1),
                            BackoffCoefficient = 1,
                            MaximumAttempts = 2,
                        },
                        ActivityId = "backoff-local-activity-fail-first-attempt",
                        EventGroups = new[] { direct },
                    });
            }
        }
    }

    [Fact]
    public async Task LocalActivityCommands_CarryMarkers()
    {
        await ExecuteAsync<LocalActivityCommandsWorkflow>(
            async worker =>
            {
                var handle = await Client.StartWorkflowAsync(
                    (LocalActivityCommandsWorkflow wf) => wf.RunAsync(),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                    {
                        TaskTimeout = TimeSpan.FromSeconds(5),
                    });
                await handle.GetResultAsync();
                var events = await FetchEventsAsync(handle);
                var both = new[] { LabelId("direct"), LabelId("scope") };
                var localActs = events.Where(
                    e => e.MarkerRecordedEventAttributes?.MarkerName == "core_local_activity").ToList();
                Assert.Equal(5, localActs.Count);
                AssertMarkers(localActs[0], both);
                AssertMarkers(localActs[1], both.Concat(new[] { LabelId("cancel-trigger") }).ToArray());
                AssertMarkers(localActs[2], both.Concat(new[] { LabelId("cancelled-la") }).ToArray());
                var backoffTimer = EventsOfType(events, EventType.TimerStarted);
                Assert.Single(backoffTimer);
                AssertMarkers(backoffTimer[0], both);
                AssertMarkers(localActs[3], both);
                AssertMarkers(localActs[4], both);
            });
    }

    [Workflow]
    public class ChildWorkflowCommandsWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var direct = Workflow.CreateEventGroup("direct");
            var scope = Workflow.CreateEventGroup("scope");
            using (Workflow.WithEventGroups(scope))
            {
                await Workflow.StartChildWorkflowAsync(
                    (NoopChildWorkflow wf) => wf.RunAsync(),
                    new() { Id = $"{Workflow.Info.WorkflowId}_child", EventGroups = new[] { direct } });
                using var cts = new CancellationTokenSource();
                var child = Workflow.ExecuteChildWorkflowAsync(
                    (SleepChildWorkflow wf) => wf.RunAsync(),
                    new()
                    {
                        Id = $"{Workflow.Info.WorkflowId}_child_cancel",
                        CancellationType = ChildWorkflowCancellationType.WaitCancellationRequested,
                        CancellationToken = cts.Token,
                        EventGroups = new[] { direct },
                    });
                await Workflow.DelayAsync(TimeSpan.FromMilliseconds(1));
                cts.Cancel();
                await SwallowCancelAsync(child);
            }
        }
    }

    [Fact]
    public async Task ChildWorkflowCommands_CarryMarkers()
    {
        await ExecuteAsync<ChildWorkflowCommandsWorkflow>(
            async worker =>
            {
                var handle = await StartAsync((ChildWorkflowCommandsWorkflow wf) => wf.RunAsync(), worker);
                await handle.GetResultAsync();
                var events = await FetchEventsAsync(handle);
                var both = new[] { LabelId("direct"), LabelId("scope") };
                var started = EventsOfType(events, EventType.StartChildWorkflowExecutionInitiated);
                Assert.Equal(2, started.Count);
                AssertMarkers(started[0], both);
                AssertMarkers(started[1], both);
                AssertMarkers(SingleEvent(events, EventType.RequestCancelExternalWorkflowExecutionInitiated), both);
            },
            extraWorkflows: new[] { typeof(NoopChildWorkflow), typeof(SleepChildWorkflow) });
    }

    [Workflow]
    public class ExternalWorkflowCommandsWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var direct = Workflow.CreateEventGroup("direct");
            var scope = Workflow.CreateEventGroup("scope");
            var other = Workflow.GetExternalWorkflowHandle("missing-workflow");
            using (Workflow.WithEventGroups(scope))
            {
                try
                {
                    await other.SignalAsync("signal", Array.Empty<object?>(), new() { EventGroups = new[] { direct } });
                }
                catch (TemporalException)
                {
                }
                try
                {
                    await other.CancelAsync(new() { EventGroups = new[] { direct } });
                }
                catch (TemporalException)
                {
                }
            }
        }
    }

    [Fact]
    public async Task ExternalWorkflowCommands_CarryMarkers()
    {
        await ExecuteAsync<ExternalWorkflowCommandsWorkflow>(async worker =>
        {
            var handle = await StartAsync((ExternalWorkflowCommandsWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var both = new[] { LabelId("direct"), LabelId("scope") };
            AssertMarkers(SingleEvent(events, EventType.SignalExternalWorkflowExecutionInitiated), both);
            AssertMarkers(SingleEvent(events, EventType.RequestCancelExternalWorkflowExecutionInitiated), both);
        });
    }

    [Workflow]
    public class ChildWorkflowSignalCommandsWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            var direct = Workflow.CreateEventGroup("direct");
            var scope = Workflow.CreateEventGroup("scope");
            var child = await Workflow.StartChildWorkflowAsync(
                (WaitForSignalChildWorkflow wf) => wf.RunAsync(),
                new() { Id = $"{Workflow.Info.WorkflowId}_child" });
            using (Workflow.WithEventGroups(scope))
            {
                await child.SignalAsync(wf => wf.NoopAsync(), new() { EventGroups = new[] { direct } });
            }
            await child.GetResultAsync();
        }
    }

    [Fact]
    public async Task ChildWorkflowSignalCommands_CarryMarkers()
    {
        await ExecuteAsync<ChildWorkflowSignalCommandsWorkflow>(
            async worker =>
            {
                var handle = await StartAsync((ChildWorkflowSignalCommandsWorkflow wf) => wf.RunAsync(), worker);
                await handle.GetResultAsync();
                var events = await FetchEventsAsync(handle);
                AssertMarkers(SingleEvent(events, EventType.StartChildWorkflowExecutionInitiated));
                AssertMarkers(
                    SingleEvent(events, EventType.SignalExternalWorkflowExecutionInitiated),
                    LabelId("direct"),
                    LabelId("scope"));
            },
            extraWorkflows: new[] { typeof(WaitForSignalChildWorkflow) });
    }

    [Workflow]
    public class MetadataCommandsWorkflow
    {
        [WorkflowRun]
        public Task RunAsync()
        {
            var direct = Workflow.CreateEventGroup("direct");
            var scope = Workflow.CreateEventGroup("scope");
            using (Workflow.WithEventGroups(scope))
            {
                Workflow.UpsertMemoWithOptions(new(MemoUpdate.ValueSet("some-key", "some-value"))
                {
                    EventGroups = new[] { direct },
                });
                Workflow.UpsertTypedSearchAttributesWithOptions(new(AttrBool.ValueSet(false))
                {
                    EventGroups = new[] { direct },
                });
                Workflow.Patched("my-patch-1", new() { EventGroups = new[] { direct } });
                Workflow.DeprecatePatch("my-patch-2", new() { EventGroups = new[] { direct } });
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    [CloudTestExclusion(
        CloudTestExclusionReason.NeedsCloudAdaptation,
        "Requires custom search attributes that the Cloud harness does not provision.")]
    public async Task MetadataCommands_CarryMarkers()
    {
        await EnsureSearchAttributesPresentAsync();
        await ExecuteAsync<MetadataCommandsWorkflow>(async worker =>
        {
            var handle = await StartAsync((MetadataCommandsWorkflow wf) => wf.RunAsync(), worker);
            await handle.GetResultAsync();
            var events = await FetchEventsAsync(handle);
            var both = new[] { LabelId("direct"), LabelId("scope") };
            AssertMarkers(SingleEvent(events, EventType.WorkflowPropertiesModified), both);
            var upserts = EventsOfType(events, EventType.UpsertWorkflowSearchAttributes);
            Assert.Equal(3, upserts.Count);
            foreach (var upsert in upserts)
            {
                AssertMarkers(upsert, both);
            }
            var patches = events.Where(
                e => e.MarkerRecordedEventAttributes?.MarkerName == "core_patch").ToList();
            Assert.Equal(2, patches.Count);
            foreach (var patch in patches)
            {
                AssertMarkers(patch, both);
            }
        });
    }

    [Workflow]
    public class ContinueAsNewCommandsWorkflow
    {
        [WorkflowRun]
        public Task RunAsync(bool secondRun)
        {
            if (secondRun)
            {
                return Task.CompletedTask;
            }
            var direct = Workflow.CreateEventGroup("direct");
            var scope = Workflow.CreateEventGroup("scope");
            using (Workflow.WithEventGroups(scope))
            {
                throw Workflow.CreateContinueAsNewException(
                    (ContinueAsNewCommandsWorkflow wf) => wf.RunAsync(true),
                    new() { EventGroups = new[] { direct } });
            }
        }
    }

    [Fact]
    public async Task ContinueAsNew_CarriesMarkers()
    {
        await ExecuteAsync<ContinueAsNewCommandsWorkflow>(async worker =>
        {
            var handle = await StartAsync((ContinueAsNewCommandsWorkflow wf) => wf.RunAsync(false), worker);
            await handle.GetResultAsync();
            var firstRun = handle with { RunId = handle.FirstExecutionRunId };
            var events = await FetchEventsAsync(firstRun);
            AssertMarkers(
                SingleEvent(events, EventType.WorkflowExecutionContinuedAsNew),
                LabelId("direct"),
                LabelId("scope"));
        });
    }

    [Workflow]
    public class EmptyIdAndLabelWorkflow
    {
        [WorkflowRun]
        public Task<IReadOnlyCollection<string>> RunAsync()
        {
            var errors = new List<string>();
            try
            {
                Workflow.CreateEventGroup(string.Empty);
            }
            catch (ArgumentException e)
            {
                errors.Add(e.Message);
            }
            try
            {
                Workflow.CreateEventGroup("id", new() { Label = string.Empty });
            }
            catch (ArgumentException e)
            {
                errors.Add(e.Message);
            }
            return Task.FromResult<IReadOnlyCollection<string>>(errors);
        }
    }

    [Fact]
    public async Task CreateEventGroup_RejectsEmptyIdAndLabel()
    {
        await ExecuteAsync<EmptyIdAndLabelWorkflow>(async worker =>
        {
            var handle = await StartAsync((EmptyIdAndLabelWorkflow wf) => wf.RunAsync(), worker);
            var errors = await handle.GetResultAsync();
            Assert.Equal(2, errors.Count);
            Assert.Contains("Event group id cannot be empty", errors.ElementAt(0));
            Assert.Contains("Event group label cannot be empty", errors.ElementAt(1));
        });
    }

    [Fact]
    public void CreateEventGroup_RequiresWorkflowContext()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Workflow.CreateEventGroup("outside"));
        Assert.Contains("Not in workflow", ex.Message);
    }

    [Workflow]
    public class NexusCommandsWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync(string endpoint)
        {
            var direct = Workflow.CreateEventGroup("direct");
            var scope = Workflow.CreateEventGroup("scope");
            var client = Workflow.CreateNexusWorkflowClient<IEventGroupsNexusService>(endpoint);
            using (Workflow.WithEventGroups(scope))
            {
                await client.ExecuteNexusOperationAsync(svc => svc.Echo(), new() { EventGroups = new[] { direct } });
                using var cts = new CancellationTokenSource();
                var running = client.ExecuteNexusOperationAsync(
                    svc => svc.Sleep(),
                    new()
                    {
                        CancellationType = NexusOperationCancellationType.TryCancel,
                        CancellationToken = cts.Token,
                        EventGroups = new[] { direct },
                    });
                await Workflow.DelayAsync(TimeSpan.FromMilliseconds(1));
                cts.Cancel();
                await SwallowCancelAsync(running);
            }
        }
    }

    [Fact]
    [CloudTestExclusion(
        CloudTestExclusionReason.NeedsCloudAdaptation,
        "Requires Cloud Nexus endpoint setup and cleanup.")]
    public async Task NexusOperationCommands_CarryMarkers()
    {
        var options = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddNexusService(new EventGroupsNexusService());
        var endpointName = $"eg-nexus-{Guid.NewGuid()}";
        var endpoint = await Env.TestEnv.CreateNexusEndpointAsync(endpointName, options.TaskQueue!);
        await ExecuteAsync<NexusCommandsWorkflow>(
            async worker =>
            {
                var handle = await Client.StartWorkflowAsync(
                    (NexusCommandsWorkflow wf) => wf.RunAsync(endpointName),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await handle.GetResultAsync();
                var events = await FetchEventsAsync(handle);
                var both = new[] { LabelId("direct"), LabelId("scope") };
                var scheduled = EventsOfType(events, EventType.NexusOperationScheduled);
                Assert.Equal(2, scheduled.Count);
                AssertMarkers(scheduled[0], both);
                AssertMarkers(scheduled[1], both);
                AssertMarkers(SingleEvent(events, EventType.NexusOperationCancelRequested), both);
            },
            options);
    }

    private Task ExecuteAsync<TWorkflow>(
        Func<TemporalWorker, Task> action,
        TemporalWorkerOptions? options = null,
        IWorkerClient? client = null,
        IReadOnlyCollection<Type>? extraWorkflows = null)
    {
        options ??= new();
        options.TaskQueue ??= $"tq-{Guid.NewGuid()}";
        options.AddWorkflow<TWorkflow>();
        if (extraWorkflows != null)
        {
            foreach (var extra in extraWorkflows)
            {
                options.AddWorkflow(extra);
            }
        }
        options.AddAllActivities(new EventGroupActivities());
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        return ExecuteWithOptionsAsync(options, action, client);
    }

    private async Task ExecuteWithOptionsAsync(
        TemporalWorkerOptions options, Func<TemporalWorker, Task> action, IWorkerClient? client)
    {
        using var worker = new TemporalWorker(client ?? Client, options);
        await worker.ExecuteAsync(() => action(worker));
    }

    private Task<WorkflowHandle<TWorkflow>> StartAsync<TWorkflow>(
        System.Linq.Expressions.Expression<Func<TWorkflow, Task>> run,
        TemporalWorker worker) =>
        Client.StartWorkflowAsync(run, new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

    private Task<WorkflowHandle<TWorkflow, TResult>> StartAsync<TWorkflow, TResult>(
        System.Linq.Expressions.Expression<Func<TWorkflow, Task<TResult>>> run,
        TemporalWorker worker) =>
        Client.StartWorkflowAsync(run, new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

    private static async Task<IReadOnlyList<HistoryEvent>> FetchEventsAsync(WorkflowHandle handle) =>
        (await handle.FetchHistoryAsync()).Events.ToList();

    private static List<HistoryEvent> EventsOfType(IReadOnlyList<HistoryEvent> events, EventType type) =>
        events.Where(e => e.EventType == type).ToList();

    private static HistoryEvent SingleEvent(IReadOnlyList<HistoryEvent> events, EventType type) =>
        Assert.Single(EventsOfType(events, type));

    private static HistoryEvent ActivityEvent(IReadOnlyList<HistoryEvent> events, string activityId) =>
        Assert.Single(
            events,
            e => e.ActivityTaskScheduledEventAttributes?.ActivityId == activityId);

    private static List<long> SignaledEventIds(IReadOnlyList<HistoryEvent> events) =>
        events.Where(e => e.EventType == EventType.WorkflowExecutionSignaled).Select(e => e.EventId).ToList();

    private static string LabelId(string id) => $"label:{id}";

    private static string Label(string id, string label) => $"label:{id}:{label}";

    private static string EventMarker(long eventId) => $"event:{eventId}";

    private static string UpdateMarker(string updateId) => $"update:{updateId}";

    private static List<string> MarkerIds(HistoryEvent evt) =>
        evt.EventGroupMarkers.Select(RenderMarkerId).OrderBy(x => x).ToList();

    private static void AssertMarkers(HistoryEvent evt, params string[] expected)
    {
        var actual = evt.EventGroupMarkers.Select(RenderMarker).ToList();
        Assert.Equal(expected.Length, actual.Count);
        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
    }

    private static void AssertMarkerIds(HistoryEvent evt, params string[] expected)
    {
        var actual = evt.EventGroupMarkers.Select(RenderMarkerId).ToList();
        Assert.Equal(expected.Length, actual.Count);
        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
    }

    private static string RenderMarker(EventGroupMarker marker)
    {
        if (marker.InboundEvent != null)
        {
            return $"event:{marker.InboundEvent.InboundEventId}";
        }
        if (marker.InboundUpdate != null)
        {
            return $"update:{marker.InboundUpdate.InboundUpdateId}";
        }
        if (marker.Label == null)
        {
            return "unknown";
        }
        if (marker.Label.Label_ != null &&
            marker.Label.Label_.Metadata.TryGetValue("encoding", out var encoding) &&
            encoding.ToStringUtf8() == "json/plain")
        {
            var label = DataConverter.Default.PayloadConverter.ToValue<string>(marker.Label.Label_);
            return $"label:{marker.Label.Id}:{label}";
        }
        return $"label:{marker.Label.Id}";
    }

    private static string RenderMarkerId(EventGroupMarker marker)
    {
        if (marker.InboundEvent != null)
        {
            return $"event:{marker.InboundEvent.InboundEventId}";
        }
        if (marker.InboundUpdate != null)
        {
            return $"update:{marker.InboundUpdate.InboundUpdateId}";
        }
        return $"label:{marker.Label.Id}";
    }

    private static (string Encoding, string Data) LabelPayloadOf(HistoryEvent evt, string markerId)
    {
        var marker = evt.EventGroupMarkers.Single(m => m.Label?.Id == markerId);
        Assert.NotNull(marker.Label.Label_);
        return (
            marker.Label.Label_.Metadata["encoding"].ToStringUtf8(),
            marker.Label.Label_.Data.ToStringUtf8());
    }

    private static bool LabelPayloadSet(HistoryEvent evt, string markerId)
    {
        var marker = evt.EventGroupMarkers.Single(m => m.Label?.Id == markerId);
        return marker.Label.Label_ != null;
    }

    private static Payload RawLabelPayload(HistoryEvent evt, string markerId) =>
        evt.EventGroupMarkers.Single(m => m.Label?.Id == markerId).Label.Label_;

    private class CustomStringConverter : IEncodingConverter
    {
        public string Encoding => "custom";

        public bool TryToPayload(object? value, out Payload? payload)
        {
            if (value is not string text)
            {
                payload = null;
                return false;
            }
            payload = new Payload
            {
                Data = ByteString.CopyFromUtf8($"custom-converter-{text}"),
            };
            payload.Metadata["encoding"] = ByteString.CopyFromUtf8(Encoding);
            return true;
        }

        public object? ToValue(Payload payload, Type type)
        {
            var text = payload.Data.ToStringUtf8();
            const string prefix = "custom-converter-";
            return text.StartsWith(prefix, StringComparison.Ordinal) ? text[prefix.Length..] : text;
        }
    }

    private class CustomStringPayloadConverter : DefaultPayloadConverter
    {
        public CustomStringPayloadConverter()
            : base(
                new CustomStringConverter(),
                new BinaryNullConverter(),
                new BinaryPlainConverter(),
                new JsonProtoConverter(),
                new BinaryProtoConverter(),
                new JsonPlainConverter(new()))
        {
        }
    }

    private class WrappingPayloadCodec : IPayloadCodec
    {
        public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p => new Payload
            {
                Metadata = { ["encoding"] = ByteString.CopyFromUtf8("binary/wrapped") },
                Data = p.ToByteString(),
            }).ToList());

        public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
            {
                var inner = new Payload();
                inner.MergeFrom(p.Data);
                return inner;
            }).ToList());
    }
}
