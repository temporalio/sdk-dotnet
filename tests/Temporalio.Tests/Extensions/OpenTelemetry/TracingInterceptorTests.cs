#pragma warning disable CA1711 // Not letting us have "New" as suffix on type name
#pragma warning disable SA1312 // Use underscores as discarded using vars

namespace Temporalio.Tests.Extensions.OpenTelemetry;

using System.Diagnostics;
using System.Text;
using global::OpenTelemetry.Trace;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Extensions.OpenTelemetry;
using Temporalio.Tests.Worker;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class TracingInterceptorTests : WorkflowEnvironmentTestBase
{
    private readonly ILogger<TracingInterceptorTests> logger;

    public TracingInterceptorTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env) => logger = LoggerFactory.CreateLogger<TracingInterceptorTests>();

    [Fact]
    public async Task TracingInterceptor_CommonCalls_HaveProperSpans()
    {
        // Run with set of actions
        var (handle, activities) = await ExecuteTracingWorkflowAsync(
            new(new TracingWorkflowAction[]
            {
                // Custom span
                new(CreateCustomActivity: "MyCustomActivity"),
                // Wait for signal
                new(WaitUntilSignalCount: 1),
                // Exec activity that fails task before complete
                new(Activity: new(
                    Param: new(FailUntilAttempt: 2))),
                // Exec child workflow that fails task before complete
                new(ChildWorkflow: new(
                    // Exec activity and finish after 2 signals
                    Param: new(new TracingWorkflowAction[]
                    {
                        new(Activity: new(Param: new(), Local: true)),
                        // Wait for the 2 signals
                        new(WaitUntilSignalCount: 2),
                    }),
                    Signal: true,
                    ExternalSignal: true)),
                // Continue as new and run one local activity
                new(ContinueAsNew: new(
                    new(new TracingWorkflowAction[]
                    {
                        new(Activity: new(Param: new(), Local: true)),
                    }))),
            }),
            async handle =>
            {
                // Send query, then update, then signal to move it along
                Assert.Equal("some query", await handle.QueryAsync(wf => wf.QueryWithActivity()));
                Assert.Equal("some update", await handle.ExecuteUpdateAsync(wf => wf.UpdateWithActivityAsync()));
                await handle.SignalAsync(wf => wf.SignalWithActivityAsync());
            });

        // Check activities
        var workflowTags = new[] { ActivityAssertion.TagEqual("temporalWorkflowID", handle.Id) };
        var workflowRunTags = workflowTags.Append(
            ActivityAssertion.TagEqual("temporalRunID", handle.ResultRunId!)).ToArray();
        var workflowChildRunTags = new[]
        {
            ActivityAssertion.TagEqual("temporalWorkflowID", handle.Id + "_child"),
            ActivityAssertion.TagNotEqual("temporalRunID", handle.ResultRunId!),
        };
        var activityRunTags = workflowRunTags.Append(
            ActivityAssertion.TagEqual("temporalActivityID", "1")).ToArray();
        var activityChildRunTags = workflowChildRunTags.Append(
            ActivityAssertion.TagEqual("temporalActivityID", "1")).ToArray();
        var workflowContinueRunTags = workflowTags.Append(
            ActivityAssertion.TagNotEqual("temporalRunID", handle.ResultRunId!)).ToArray();
        var activityContinueRunTags = workflowContinueRunTags.Append(
            ActivityAssertion.TagEqual("temporalActivityID", "1")).ToArray();
        AssertActivities(
            activities,
            // Client start
            new(
                "StartWorkflow:TracingWorkflow",
                Parent: null,
                Tags: workflowTags),
            // Run workflow
            new(
                "RunWorkflow:TracingWorkflow",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Custom activity
            new(
                "MyCustomActivity",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Start activity
            new(
                "StartActivity:TracingActivity",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Run activity first try
            new(
                "RunActivity:TracingActivity",
                Parent: "StartActivity:TracingActivity",
                Tags: activityRunTags,
                Events: new[] { ActivityAssertion.ExceptionEvent("Intentional activity failure") }),
            // Run activity second try
            new(
                "RunActivity:TracingActivity",
                Parent: "StartActivity:TracingActivity",
                Tags: activityRunTags),
            // Start child workflow
            new(
                "StartChildWorkflow:TracingWorkflow",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Run child workflow
            new(
                "RunWorkflow:TracingWorkflow",
                Parent: "StartChildWorkflow:TracingWorkflow",
                Tags: workflowChildRunTags),
            // Start activity in child
            new(
                "StartActivity:TracingActivity",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowChildRunTags),
            // Run activity in child
            new(
                "RunActivity:TracingActivity",
                Parent: "StartActivity:TracingActivity",
                Tags: activityChildRunTags),
            // Signal child
            new(
                "SignalChildWorkflow:Signal1",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Handle child signal
            new(
                "HandleSignal:Signal1",
                Parent: "StartChildWorkflow:TracingWorkflow",
                Tags: workflowChildRunTags,
                Links: new[] { ActivityAssertion.LinkTo(activities, "SignalChildWorkflow:Signal1") }),
            // Signal external
            new(
                "SignalExternalWorkflow:Signal2",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Handle external signal
            new(
                "HandleSignal:Signal2",
                Parent: "StartChildWorkflow:TracingWorkflow",
                Tags: workflowChildRunTags,
                Links: new[] { ActivityAssertion.LinkTo(activities, "SignalExternalWorkflow:Signal2") }),
            // Complete child
            new(
                "CompleteWorkflow:TracingWorkflow",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowChildRunTags),
            // Send signal
            new(
                "SignalWorkflow:SignalWithActivity",
                Parent: null,
                Tags: workflowTags),
            // Handle signal
            new(
                "HandleSignal:SignalWithActivity",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Links: new[] { ActivityAssertion.LinkTo(activities, "SignalWorkflow:SignalWithActivity") }),
            // Custom signal activity
            new(
                "MySignalActivity",
                Parent: "HandleSignal:SignalWithActivity",
                Tags: workflowRunTags.Append(ActivityAssertion.TagEqual("foo", "bar")).ToArray()),
            // Send query
            new(
                "QueryWorkflow:QueryWithActivity",
                Parent: null,
                Tags: workflowTags),
            // Handle query
            new(
                "HandleQuery:QueryWithActivity",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Links: new[] { ActivityAssertion.LinkTo(activities, "QueryWorkflow:QueryWithActivity") }),
            // Custom query activity
            new(
                "MyQueryActivity",
                Parent: "HandleQuery:QueryWithActivity",
                Tags: workflowRunTags.Append(ActivityAssertion.TagEqual("baz", "qux")).ToArray()),
            // Send update
            new(
                "UpdateWorkflow:UpdateWithActivity",
                Parent: null,
                Tags: workflowTags),
            // Validate update
            new(
                "ValidateUpdate:UpdateWithActivity",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Links: new[] { ActivityAssertion.LinkTo(activities, "UpdateWorkflow:UpdateWithActivity") }),
            // Handle update
            new(
                "HandleUpdate:UpdateWithActivity",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Links: new[] { ActivityAssertion.LinkTo(activities, "UpdateWorkflow:UpdateWithActivity") }),
            // Custom update activity
            new(
                "MyUpdateActivity",
                Parent: "HandleUpdate:UpdateWithActivity",
                Tags: workflowRunTags.Append(ActivityAssertion.TagEqual("qux", "quux")).ToArray()),
            // Continue as new workflow
            new(
                "RunWorkflow:TracingWorkflow",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowContinueRunTags),
            // Start continue workflow activity
            new(
                "StartActivity:TracingActivity",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowContinueRunTags),
            // Run continue workflow activity
            new(
                "RunActivity:TracingActivity",
                Parent: "StartActivity:TracingActivity",
                Tags: activityContinueRunTags),
            // Complete continue workflow
            new(
                "CompleteWorkflow:TracingWorkflow",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowContinueRunTags));
    }

    [Fact]
    public async Task TracingInterceptor_TaskFailures_HaveProperSpans()
    {
        // Simple workflow task failure
        var (handle, activities) = await ExecuteTracingWorkflowAsync(
            new(new TracingWorkflowAction[] { new(FailOnNonReplay: "fail1") }));
        var workflowTags = new[] { ActivityAssertion.TagEqual("temporalWorkflowID", handle.Id) };
        var workflowRunTags = workflowTags.Append(
            ActivityAssertion.TagEqual("temporalRunID", handle.ResultRunId!)).ToArray();
        AssertActivities(
            activities,
            // Client start
            new(
                "StartWorkflow:TracingWorkflow",
                Parent: null,
                Tags: workflowTags),
            // Run workflow
            new(
                "RunWorkflow:TracingWorkflow",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Task failure
            new(
                "WorkflowTaskFailure:TracingWorkflow",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Events: new[] { ActivityAssertion.ExceptionEvent("fail1") }),
            // Complete (succeeds after replay of above)
            new(
                "CompleteWorkflow:TracingWorkflow",
                // Parent is start-workflow because task failure
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags));

        // Fail task, start activity, fail task, Start child, fail task
        (handle, activities) = await ExecuteTracingWorkflowAsync(new(new TracingWorkflowAction[]
        {
            new(FailOnNonReplay: "fail2"),
            new(Activity: new(
                Param: new(),
                FailOnNonReplayBeforeComplete: "fail3")),
            new(ChildWorkflow: new(
                Param: new(Array.Empty<TracingWorkflowAction>()),
                FailOnNonReplayBeforeComplete: "fail4")),
        }));
        workflowTags = new[] { ActivityAssertion.TagEqual("temporalWorkflowID", handle.Id) };
        workflowRunTags = workflowTags.Append(
            ActivityAssertion.TagEqual("temporalRunID", handle.ResultRunId!)).ToArray();
        var activityRunTags = workflowRunTags.Append(
            ActivityAssertion.TagEqual("temporalActivityID", "1")).ToArray();
        var workflowChildRunTags = new[]
        {
            ActivityAssertion.TagEqual("temporalWorkflowID", handle.Id + "_child"),
            ActivityAssertion.TagNotEqual("temporalRunID", handle.ResultRunId!),
        };
        AssertActivities(
            activities,
            // Client start
            new(
                "StartWorkflow:TracingWorkflow",
                Parent: null,
                Tags: workflowTags),
            // Run workflow
            new(
                "RunWorkflow:TracingWorkflow",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Initial task failure
            new(
                "WorkflowTaskFailure:TracingWorkflow",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Events: new[] { ActivityAssertion.ExceptionEvent("fail2") }),
            // Start activity
            new(
                "StartActivity:TracingActivity",
                // Parent is start-workflow because task failure
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Run activity
            new(
                "RunActivity:TracingActivity",
                Parent: "StartActivity:TracingActivity",
                Tags: activityRunTags),
            // Task failure
            new(
                "WorkflowTaskFailure:TracingWorkflow",
                // Parent is start-workflow because task failure
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Events: new[] { ActivityAssertion.ExceptionEvent("fail3") }),
            // Start child
            new(
                "StartChildWorkflow:TracingWorkflow",
                // Parent is start-workflow because task failure
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Run child
            new(
                "RunWorkflow:TracingWorkflow",
                Parent: "StartChildWorkflow:TracingWorkflow",
                Tags: workflowChildRunTags),
            // Complete child
            new(
                "CompleteWorkflow:TracingWorkflow",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowChildRunTags),
            // Task failure
            new(
                "WorkflowTaskFailure:TracingWorkflow",
                // Parent is start-workflow because task failure
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Events: new[] { ActivityAssertion.ExceptionEvent("fail4") }),
            // Complete
            new(
                "CompleteWorkflow:TracingWorkflow",
                // Parent is start-workflow because task failure
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags));

        // TODO(cretz): Fix when https://github.com/temporalio/sdk-core/issues/615 fixed
        // Fail task inside update
        (handle, activities) = await ExecuteTracingWorkflowAsync(
            new(new TracingWorkflowAction[] { new(WaitUntilSignalCount: 1) }),
            async handle =>
            {
                // Send update that fails task in background and confirm task fail
                _ = Task.Run(() =>
                    handle.ExecuteUpdateAsync(wf => wf.UpdateTaskFailureAsync("some message")));
                await WorkflowWorkerTests.AssertTaskFailureContainsEventuallyAsync(
                    handle, "some message");
            },
            expectFail: true,
            terminate: true);
        workflowTags = new[] { ActivityAssertion.TagEqual("temporalWorkflowID", handle.Id) };
        workflowRunTags = workflowTags.Append(
            ActivityAssertion.TagEqual("temporalRunID", handle.ResultRunId!)).ToArray();
        // Remove duplicate activities since task failure causes retry
        var dedupedActivities = activities.GroupBy(a => a.OperationName).Select(g => g.First()).ToList();
        logger.LogDebug(
            "Activities2:\n{Activities}",
            string.Join("\n", DumpActivities(dedupedActivities)));
        AssertActivities(
            dedupedActivities,
            // Client start
            new(
                "StartWorkflow:TracingWorkflow",
                Parent: null,
                Tags: workflowTags),
            // Run workflow
            new(
                "RunWorkflow:TracingWorkflow",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Handle update
            new(
                "HandleUpdate:UpdateTaskFailure",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                IgnoreLinks: true),
            // Update
            new(
                "WorkflowTaskFailure:UpdateTaskFailure",
                Parent: "HandleUpdate:UpdateTaskFailure",
                Tags: workflowRunTags,
                Events: new[] { ActivityAssertion.ExceptionEvent("some message") }),
            // On 1.24+, updates properly fail on client when workflow terminated
            new(
                "UpdateWorkflow:UpdateTaskFailure",
                Parent: null,
                Tags: workflowTags,
                Events: new[] { ActivityAssertion.ExceptionEvent("workflow execution already completed") }));
    }

    [Fact]
    public async Task TracingInterceptor_ProperFailures_HaveProperSpans()
    {
        // Workflow failure
        var (handle, activities) = await ExecuteTracingWorkflowAsync(
            new(new TracingWorkflowAction[] { new(AppFail: "fail1") }),
            expectFail: true);
        var workflowTags = new[] { ActivityAssertion.TagEqual("temporalWorkflowID", handle.Id) };
        var workflowRunTags = workflowTags.Append(
            ActivityAssertion.TagEqual("temporalRunID", handle.ResultRunId!)).ToArray();
        AssertActivities(
            activities,
            // Client start
            new(
                "StartWorkflow:TracingWorkflow",
                Parent: null,
                Tags: workflowTags),
            // Run workflow
            new(
                "RunWorkflow:TracingWorkflow",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Complete
            new(
                "CompleteWorkflow:TracingWorkflow",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Events: new[] { ActivityAssertion.ExceptionEvent("fail1") }));

        // Signal failure
        (handle, activities) = await ExecuteTracingWorkflowAsync(
            new(new TracingWorkflowAction[] { new(WaitUntilSignalCount: 1) }),
            afterStart: handle => handle.SignalAsync(wf => wf.SignalFailureAsync("fail2")),
            expectFail: true);
        workflowTags = new[] { ActivityAssertion.TagEqual("temporalWorkflowID", handle.Id) };
        workflowRunTags = workflowTags.Append(
            ActivityAssertion.TagEqual("temporalRunID", handle.ResultRunId!)).ToArray();
        AssertActivities(
            activities,
            // Client start
            new(
                "StartWorkflow:TracingWorkflow",
                Parent: null,
                Tags: workflowTags),
            // Run workflow
            new(
                "RunWorkflow:TracingWorkflow",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Send signal
            new(
                "SignalWorkflow:SignalFailure",
                Parent: null,
                Tags: workflowTags),
            // Handle signal
            new(
                "HandleSignal:SignalFailure",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Links: new[] { ActivityAssertion.LinkTo(activities, "SignalWorkflow:SignalFailure") }),
            // Complete
            new(
                "CompleteWorkflow:TracingWorkflow",
                // Child of the signal handler
                Parent: "HandleSignal:SignalFailure",
                Tags: workflowRunTags,
                // Failure is at workflow level for signals
                Events: new[] { ActivityAssertion.ExceptionEvent("fail2") }));

        // Query failure
        (handle, activities) = await ExecuteTracingWorkflowAsync(
            new(Array.Empty<TracingWorkflowAction>()),
            afterStart: handle => Assert.ThrowsAsync<WorkflowQueryFailedException>(() =>
                handle.QueryAsync(wf => wf.QueryFailure("fail3"))));
        workflowTags = new[] { ActivityAssertion.TagEqual("temporalWorkflowID", handle.Id) };
        workflowRunTags = workflowTags.Append(
            ActivityAssertion.TagEqual("temporalRunID", handle.ResultRunId!)).ToArray();
        AssertActivities(
            activities,
            // Client start
            new(
                "StartWorkflow:TracingWorkflow",
                Parent: null,
                Tags: workflowTags),
            // Run workflow
            new(
                "RunWorkflow:TracingWorkflow",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Send query
            new(
                "QueryWorkflow:QueryFailure",
                Parent: null,
                Tags: workflowTags,
                Events: new[] { ActivityAssertion.ExceptionEvent("fail3") }),
            // Handle query
            new(
                "HandleQuery:QueryFailure",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Links: new[] { ActivityAssertion.LinkTo(activities, "QueryWorkflow:QueryFailure") },
                // Failure is at query level
                Events: new[] { ActivityAssertion.ExceptionEvent("fail3") }),
            // Complete
            new(
                "CompleteWorkflow:TracingWorkflow",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowRunTags));

        // Update failure
        (handle, activities) = await ExecuteTracingWorkflowAsync(
            new(new TracingWorkflowAction[] { new(WaitUntilSignalCount: 1) }),
            async handle =>
            {
                // Send update that fails then send finish signal
                await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() =>
                    handle.ExecuteUpdateAsync(wf => wf.UpdateFailureAsync("fail4")));
                await handle.SignalAsync(wf => wf.Signal1Async());
            });
        workflowTags = new[] { ActivityAssertion.TagEqual("temporalWorkflowID", handle.Id) };
        workflowRunTags = workflowTags.Append(
            ActivityAssertion.TagEqual("temporalRunID", handle.ResultRunId!)).ToArray();
        AssertActivities(
            activities,
            // Client start
            new(
                "StartWorkflow:TracingWorkflow",
                Parent: null,
                Tags: workflowTags),
            // Run workflow
            new(
                "RunWorkflow:TracingWorkflow",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Send update
            new(
                "UpdateWorkflow:UpdateFailure",
                Parent: null,
                Tags: workflowTags),
            // Validate update
            new(
                "ValidateUpdate:UpdateFailure",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Links: new[] { ActivityAssertion.LinkTo(activities, "UpdateWorkflow:UpdateFailure") }),
            // Handle update
            new(
                "HandleUpdate:UpdateFailure",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Links: new[] { ActivityAssertion.LinkTo(activities, "UpdateWorkflow:UpdateFailure") }),
            // Update failure as separate span
            new(
                "CompleteUpdate:UpdateFailure",
                Parent: "HandleUpdate:UpdateFailure",
                Tags: workflowRunTags,
                // Failure is its own event for update handlers
                Events: new[] { ActivityAssertion.ExceptionEvent("fail4") }),
            // Send signal
            new(
                "SignalWorkflow:Signal1",
                Parent: null,
                Tags: workflowTags),
            // Handle signal
            new(
                "HandleSignal:Signal1",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Links: new[] { ActivityAssertion.LinkTo(activities, "SignalWorkflow:Signal1") }),
            // Complete
            new(
                "CompleteWorkflow:TracingWorkflow",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowRunTags));

        // Update validator failure
        (handle, activities) = await ExecuteTracingWorkflowAsync(
            new(new TracingWorkflowAction[] { new(WaitUntilSignalCount: 1) }),
            async handle =>
            {
                // Send update that fails validator then send finish signal
                await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() =>
                    handle.ExecuteUpdateAsync(wf => wf.UpdateValidatorFailureAsync("fail5")));
                await handle.SignalAsync(wf => wf.Signal1Async());
            });
        workflowTags = new[] { ActivityAssertion.TagEqual("temporalWorkflowID", handle.Id) };
        workflowRunTags = workflowTags.Append(
            ActivityAssertion.TagEqual("temporalRunID", handle.ResultRunId!)).ToArray();
        AssertActivities(
            activities,
            // Client start
            new(
                "StartWorkflow:TracingWorkflow",
                Parent: null,
                Tags: workflowTags),
            // Run workflow
            new(
                "RunWorkflow:TracingWorkflow",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags),
            // Send update
            new(
                "UpdateWorkflow:UpdateValidatorFailure",
                Parent: null,
                Tags: workflowTags),
            // Validate update
            new(
                "ValidateUpdate:UpdateValidatorFailure",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                // Failure is on the validator span
                Events: new[] { ActivityAssertion.ExceptionEvent("fail5") },
                Links: new[] { ActivityAssertion.LinkTo(activities, "UpdateWorkflow:UpdateValidatorFailure") }),
            // Send signal
            new(
                "SignalWorkflow:Signal1",
                Parent: null,
                Tags: workflowTags),
            // Handle signal
            new(
                "HandleSignal:Signal1",
                Parent: "StartWorkflow:TracingWorkflow",
                Tags: workflowRunTags,
                Links: new[] { ActivityAssertion.LinkTo(activities, "SignalWorkflow:Signal1") }),
            // Complete
            new(
                "CompleteWorkflow:TracingWorkflow",
                Parent: "RunWorkflow:TracingWorkflow",
                Tags: workflowRunTags));
    }

    [Fact]
    public async Task TracingInterceptor_SignalWithStart_HaveProperSpans()
    {
        var activities = await WithTracingWorkerAsync(async (client, worker) =>
        {
            var twoSignalWait = new TracingWorkflowParam(
                new TracingWorkflowAction[] { new(WaitUntilSignalCount: 2) });

            // Signal with start new
            var options = new WorkflowOptions(id: $"wf-{Guid.NewGuid()}", worker.Options.TaskQueue!);
            options.SignalWithStart((TracingWorkflow wf) => wf.Signal1Async());
            var handle = await client.StartWorkflowAsync(
                (TracingWorkflow wf) => wf.RunAsync(twoSignalWait),
                options);

            // Wait first task done
            await AssertMore.HasEventEventuallyAsync(
                handle, e => e.WorkflowTaskCompletedEventAttributes != null);

            // Go again
            options = new WorkflowOptions(id: handle.Id, worker.Options.TaskQueue!);
            options.SignalWithStart((TracingWorkflow wf) => wf.Signal2Async());
            await client.ExecuteWorkflowAsync(
                (TracingWorkflow wf) => wf.RunAsync(twoSignalWait),
                options);
        });
        AssertActivities(
            activities,
            ActivityAssertion.NameAndParent(
                "SignalWithStartWorkflow:TracingWorkflow", null),
            ActivityAssertion.NameAndParent(
                "HandleSignal:Signal1", "SignalWithStartWorkflow:TracingWorkflow"),
            ActivityAssertion.NameAndParent(
                "HandleSignal:Signal2", "SignalWithStartWorkflow:TracingWorkflow"),
            ActivityAssertion.NameAndParent(
                "RunWorkflow:TracingWorkflow", "SignalWithStartWorkflow:TracingWorkflow"),
            ActivityAssertion.NameAndParent(
                "CompleteWorkflow:TracingWorkflow", "RunWorkflow:TracingWorkflow"),
            // There's actually a second one of these
            ActivityAssertion.NameAndParent(
                "SignalWithStartWorkflow:TracingWorkflow", null));
    }

    [Fact]
    public async Task TracingInterceptor_UpdateWithStart_HaveProperSpans()
    {
        var activities = await WithTracingWorkerAsync(async (client, worker) =>
        {
            var twoUpdateWait = new TracingWorkflowParam(
                new TracingWorkflowAction[] { new(WaitUntilUpdateCount: 2) });

            // Update with start new
            var id = $"wf-{Guid.NewGuid()}";
            await client.ExecuteUpdateWithStartWorkflowAsync(
                (TracingWorkflow wf) => wf.Update1Async(),
                new(WithStartWorkflowOperation.Create(
                    (TracingWorkflow wf) => wf.RunAsync(twoUpdateWait),
                    new(id, worker.Options.TaskQueue!) { IdConflictPolicy = WorkflowIdConflictPolicy.Fail })));

            // And again
            await client.ExecuteUpdateWithStartWorkflowAsync(
                (TracingWorkflow wf) => wf.Update2Async(),
                new(WithStartWorkflowOperation.Create(
                    (TracingWorkflow wf) => wf.RunAsync(twoUpdateWait),
                    new(id, worker.Options.TaskQueue!) { IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting })));
        });
        AssertActivities(
            activities,
            ActivityAssertion.NameAndParent(
                "UpdateWithStartWorkflow:TracingWorkflow", null),
            ActivityAssertion.NameAndParent(
                "HandleUpdate:Update1", "UpdateWithStartWorkflow:TracingWorkflow"),
            ActivityAssertion.NameAndParent(
                "HandleUpdate:Update2", "UpdateWithStartWorkflow:TracingWorkflow"),
            ActivityAssertion.NameAndParent(
                "RunWorkflow:TracingWorkflow", "UpdateWithStartWorkflow:TracingWorkflow"),
            ActivityAssertion.NameAndParent(
                "CompleteWorkflow:TracingWorkflow", "RunWorkflow:TracingWorkflow"),
            // There's actually a second one of these
            ActivityAssertion.NameAndParent(
                "UpdateWithStartWorkflow:TracingWorkflow", null));
    }

    private static void AssertActivities(
        IReadOnlyCollection<Activity> activities, params ActivityAssertion[] assertions)
    {
        var checks = assertions.Select<ActivityAssertion, Action<Activity>>(
            assert => act => assert.AssertActivity(activities, act));
        AssertMore.Every(activities, checks.ToArray());
    }

    private static IEnumerable<string> DumpActivities(
        IReadOnlyCollection<Activity> activities,
        ActivitySpanId ParentId = default,
        int IndentDepth = 0) =>
        activities.Where(a => a.ParentSpanId == ParentId).SelectMany(activity =>
            Enumerable.Concat(
                new[] { DumpActivity(activity, IndentDepth) },
                DumpActivities(activities, activity.SpanId, IndentDepth + 1)));

    private static string DumpActivity(Activity activity, int IndentDepth = 0)
    {
        var str = new StringBuilder(string.Concat(Enumerable.Repeat("  ", IndentDepth)));
        str.Append(activity.OperationName);
        str.Append($" id: {activity.SpanId} (parent: {activity.ParentSpanId})");
        if (activity.Tags.Any())
        {
            str.Append(" (tags: ").
                AppendJoin(", ", activity.Tags.Select(t => $"{t.Key}={t.Value}")).
                Append(')');
        }
        if (activity.Links.Any())
        {
            str.Append(" (links:").
                AppendJoin(", ", activity.Links.Select(l => l.Context.SpanId)).
                Append(')');
        }
        foreach (var evt in activity.Events)
        {
            str.Append(" (event: ").
                Append(evt.Name).
                Append(", tags: ").
                AppendJoin(", ", evt.Tags.Select(t => $"{t.Key}={t.Value}")).
                Append(')');
        }
        return str.ToString();
    }

    private async Task<(WorkflowHandle<TracingWorkflow> Handle, IReadOnlyCollection<Activity> Activities)> ExecuteTracingWorkflowAsync(
        TracingWorkflowParam param,
        Func<WorkflowHandle<TracingWorkflow>, Task>? afterStart = null,
        bool expectFail = false,
        bool terminate = false)
    {
        WorkflowHandle<TracingWorkflow>? handle = null;
        var activities = await WithTracingWorkerAsync(async (client, worker) =>
        {
            // Start
            var options = new WorkflowOptions(id: $"wf-{Guid.NewGuid()}", worker.Options.TaskQueue!);
            handle = await client.StartWorkflowAsync((TracingWorkflow wf) => wf.RunAsync(param), options);

            // Run after-start, then wait for complete
            if (afterStart != null)
            {
                await afterStart.Invoke(handle);
            }
            if (terminate)
            {
                await handle.TerminateAsync();
            }
            if (expectFail)
            {
                await Assert.ThrowsAsync<WorkflowFailedException>(() => handle.GetResultAsync());
            }
            else
            {
                await handle.GetResultAsync();
            }
        });
        return (handle!, activities);
    }

    private async Task<IReadOnlyCollection<Activity>> WithTracingWorkerAsync(
        Func<ITemporalClient, TemporalWorker, Task> run)
    {
        var activities = new List<Activity>();

        // Setup provider
        using var tracerProvider = global::OpenTelemetry.Sdk.CreateTracerProviderBuilder().
            AddSource(
                TracingInterceptor.ClientSource.Name,
                TracingInterceptor.WorkflowsSource.Name,
                TracingInterceptor.ActivitiesSource.Name,
                TracingWorkflow.CustomSource.Name).
            AddInMemoryExporter(activities).
            Build();

        // Create a client with the interceptor
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Interceptors = new[] { new TracingInterceptor() };
        var client = new TemporalClient(Client.Connection, newOptions);

        // Run with worker
        var workerOptions = new TemporalWorkerOptions(taskQueue: $"tq-{Guid.NewGuid()}").
                AddAllActivities<TracingActivities>(null).
                AddWorkflow<TracingWorkflow>();
        using var worker = new TemporalWorker(client, workerOptions);
        await worker.ExecuteAsync(() => run(client, worker));
        logger.LogDebug(
            "Activities:\n{Activities}",
            string.Join("\n", DumpActivities(activities)));
        return activities;
    }

    public record ActivityAssertion(
        string Name,
        string? Parent,
        Action<KeyValuePair<string, string?>>[]? Tags = null,
        Action<ActivityEvent>[]? Events = null,
        Action<ActivityLink>[]? Links = null,
        bool IgnoreLinks = false,
        bool IgnoreTags = false,
        bool IgnoreEvents = false)
    {
        public static ActivityAssertion NameAndParent(string name, string? parent) => new(
            Name: name, Parent: parent, IgnoreLinks: true, IgnoreTags: true, IgnoreEvents: true);

        public void AssertActivity(IReadOnlyCollection<Activity> activities, Activity activity)
        {
            Assert.Equal(Name, activity.OperationName);
            Activity? parent = activities.SingleOrDefault(a => a.SpanId == activity.ParentSpanId);
            Assert.Equal(Parent, parent?.OperationName);
            if (!IgnoreTags)
            {
                AssertMore.Every(activity.Tags, Tags ?? Array.Empty<Action<KeyValuePair<string, string?>>>());
            }
            if (!IgnoreEvents)
            {
                AssertMore.Every(activity.Events, Events ?? Array.Empty<Action<ActivityEvent>>());
            }
            if (!IgnoreLinks)
            {
                AssertMore.Every(activity.Links, Links ?? Array.Empty<Action<ActivityLink>>());
            }
        }

        public static Action<KeyValuePair<string, string?>> TagEqual(string key, string? value) =>
            tag => Assert.Equal(new KeyValuePair<string, string?>(key, value), tag);

        public static Action<KeyValuePair<string, string?>> TagNotEqual(string key, string? value) =>
            tag => Assert.True(tag.Key == key && tag.Value != value);

        public static Action<ActivityEvent> ExceptionEvent(string msg) => evt =>
        {
            Assert.Equal("exception", evt.Name);
            Assert.Equal(msg, evt.Tags.Single(t => t.Key == "exception.message").Value);
        };

        public static Action<ActivityLink> LinkTo(
            IReadOnlyCollection<Activity> activities, string name) => link =>
                Assert.Equal(
                    name,
                    activities.SingleOrDefault(a => a.SpanId == link.Context.SpanId)?.OperationName);
    }

    [Workflow]
    public class TracingWorkflow
    {
        public static readonly ActivitySource CustomSource = new("MyCustomSource");

        private int signalCount;
        private int updateCount;

        [WorkflowRun]
        public async Task RunAsync(TracingWorkflowParam param)
        {
            foreach (var action in param.Actions)
            {
                if (action.AppFail != null)
                {
                    throw new ApplicationFailureException(action.AppFail);
                }
                if (action.FailOnNonReplay != null)
                {
                    await RaiseOnNonReplayAsync(action.FailOnNonReplay);
                }
                if (action.ChildWorkflow != null)
                {
                    var handle = await Workflow.StartChildWorkflowAsync(
                        (TracingWorkflow wf) => wf.RunAsync(action.ChildWorkflow.Param),
                        new() { Id = Workflow.Info.WorkflowId + action.ChildWorkflow.IdSuffix });
                    if (action.ChildWorkflow.FailOnNonReplayBeforeComplete != null)
                    {
                        await RaiseOnNonReplayAsync(action.ChildWorkflow.FailOnNonReplayBeforeComplete);
                    }
                    if (action.ChildWorkflow.Signal)
                    {
                        await handle.SignalAsync(wf => wf.Signal1Async());
                    }
                    if (action.ChildWorkflow.ExternalSignal)
                    {
                        var externalHandle = Workflow.GetExternalWorkflowHandle<TracingWorkflow>(handle.Id);
                        await externalHandle.SignalAsync(wf => wf.Signal2Async());
                    }
                    await handle.GetResultAsync();
                }
                if (action.Activity != null)
                {
                    var retry = new RetryPolicy() { InitialInterval = TimeSpan.FromMilliseconds(1) };
                    // Start but don't execute quite yet
                    Task activityTask;
                    if (action.Activity.Local)
                    {
                        activityTask = Workflow.ExecuteLocalActivityAsync(
                            () => TracingActivities.TracingActivity(action.Activity.Param),
                            new()
                            {
                                StartToCloseTimeout = TimeSpan.FromSeconds(10),
                                RetryPolicy = retry,
                            });
                    }
                    else
                    {
                        activityTask = Workflow.ExecuteActivityAsync(
                            () => TracingActivities.TracingActivity(action.Activity.Param),
                            new()
                            {
                                StartToCloseTimeout = TimeSpan.FromSeconds(10),
                                RetryPolicy = retry,
                            });
                    }
                    if (action.Activity.FailOnNonReplayBeforeComplete != null)
                    {
                        await RaiseOnNonReplayAsync(action.Activity.FailOnNonReplayBeforeComplete);
                    }
                    await activityTask;
                }
                if (action.ContinueAsNew != null)
                {
                    throw Workflow.CreateContinueAsNewException(
                        (TracingWorkflow wf) => wf.RunAsync(action.ContinueAsNew.Param));
                }
                if (action.WaitUntilSignalCount > 0)
                {
                    await Workflow.WaitConditionAsync(() => signalCount >= action.WaitUntilSignalCount);
                }
                if (action.CreateCustomActivity != null)
                {
                    CustomSource.TrackWorkflowDiagnosticActivity(action.CreateCustomActivity).Dispose();
                }
                if (action.WaitUntilUpdateCount > 0)
                {
                    await Workflow.WaitConditionAsync(() => updateCount >= action.WaitUntilUpdateCount);
                }
            }
        }

        [WorkflowSignal]
        public async Task Signal1Async() => signalCount++;

        [WorkflowSignal]
        public async Task Signal2Async() => signalCount++;

        [WorkflowSignal]
        public async Task SignalWithActivityAsync()
        {
            using var _ = CustomSource.TrackWorkflowDiagnosticActivity(
                "MySignalActivity",
                tags: new[] { KeyValuePair.Create<string, object?>("foo", "bar") });
            signalCount++;
        }

        [WorkflowSignal]
        public async Task SignalFailureAsync(string msg) =>
            throw new ApplicationFailureException(msg);

        [WorkflowQuery]
        public string QueryWithActivity()
        {
            using var _ = CustomSource.TrackWorkflowDiagnosticActivity(
                "MyQueryActivity",
                tags: new[] { KeyValuePair.Create<string, object?>("baz", "qux") });
            return "some query";
        }

        [WorkflowQuery]
        public string QueryFailure(string msg) =>
            throw new ApplicationFailureException(msg);

        [WorkflowUpdate]
        public async Task<string> UpdateWithActivityAsync()
        {
            using var _ = CustomSource.TrackWorkflowDiagnosticActivity(
                "MyUpdateActivity",
                tags: new[] { KeyValuePair.Create<string, object?>("qux", "quux") });
            return "some update";
        }

        [WorkflowUpdate]
        public async Task<string> UpdateFailureAsync(string msg) =>
            throw new ApplicationFailureException(msg);

        [WorkflowUpdate]
        public async Task UpdateTaskFailureAsync(string msg) =>
            throw new InvalidOperationException(msg);

        [WorkflowUpdate]
        public async Task<string> UpdateValidatorFailureAsync(string msg) =>
            throw new NotImplementedException();

        [WorkflowUpdateValidator(nameof(UpdateValidatorFailureAsync))]
        public void ValidateUpdateValidatorFailure(string msg) =>
            throw new ApplicationFailureException(msg);

        [WorkflowUpdate]
        public async Task Update1Async() => updateCount++;

        [WorkflowUpdate]
        public async Task Update2Async() => updateCount++;

        private static async Task RaiseOnNonReplayAsync(string msg)
        {
            var replaying = Workflow.Unsafe.IsReplaying;
            // We sleep to force a task rollover
            await Workflow.DelayAsync(1);
            if (!replaying)
            {
                throw new InvalidOperationException(msg);
            }
        }
    }

    public sealed class TracingActivities
    {
        [Activity]
        public static void TracingActivity(TracingActivityParam param)
        {
            if (param.Heartbeat && !ActivityExecutionContext.Current.Info.IsLocal)
            {
                ActivityExecutionContext.Current.Heartbeat();
            }
            if (ActivityExecutionContext.Current.Info.Attempt < param.FailUntilAttempt)
            {
                throw new InvalidOperationException("Intentional activity failure");
            }
        }
    }

    public record TracingWorkflowParam(
        TracingWorkflowAction[] Actions);

    public record TracingWorkflowAction(
        string? AppFail = null,
        string? FailOnNonReplay = null,
        TracingWorkflowActionChildWorkflow? ChildWorkflow = null,
        TracingWorkflowActionActivity? Activity = null,
        TracingWorkflowActionContinueAsNew? ContinueAsNew = null,
        int WaitUntilSignalCount = 0,
        string? CreateCustomActivity = null,
        int WaitUntilUpdateCount = 0);

    public record TracingWorkflowActionChildWorkflow(
        TracingWorkflowParam Param,
        string IdSuffix = "_child",
        bool Signal = false,
        bool ExternalSignal = false,
        string? FailOnNonReplayBeforeComplete = null);

    public record TracingWorkflowActionActivity(
        TracingActivityParam Param,
        bool Local = false,
        string? FailOnNonReplayBeforeComplete = null);

    public record TracingWorkflowActionContinueAsNew(
        TracingWorkflowParam Param);

    public record TracingActivityParam(
        bool Heartbeat = true,
        int FailUntilAttempt = 0);
}