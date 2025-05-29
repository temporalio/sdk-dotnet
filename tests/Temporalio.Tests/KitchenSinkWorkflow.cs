using Temporalio.Api.Enums.V1;

namespace Temporalio.Tests;

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow("kitchen_sink")]
public class KitchenSinkWorkflow
{
    [WorkflowRun]
    public async Task<object?> RunAsync(KSWorkflowParams args)
    {
        Workflow.Logger.LogInformation("Starting kitchen sink workflow with params: {Params}", args);

        // Handle all initial actions
        if (args.Actions != null)
        {
            foreach (var action in args.Actions)
            {
                var (shouldReturn, value) = await HandleActionAsync(args, action);
                if (shouldReturn)
                {
                    return value;
                }
            }
        }

        // Handle signal actions
        if (args.ActionSignal != null)
        {
            var actions = new List<KSAction>();
            Workflow.Signals[args.ActionSignal] = WorkflowSignalDefinition.CreateWithoutAttribute(
                args.ActionSignal,
                async (KSAction action) => actions.Add(action));
            while (true)
            {
                await Workflow.WaitConditionAsync(() => actions.Count > 0);
                var (shouldReturn, value) = await HandleActionAsync(args, actions[0]);
                actions.RemoveAt(0);
                if (shouldReturn)
                {
                    return value;
                }
            }
        }
        return null;
    }

    public Task SomeActionSignalAsync(KSAction action) => throw new NotImplementedException();

    public string SomeQuery(string arg) => throw new NotImplementedException();

    public Task SomeSignalAsync(string arg) => Task.CompletedTask;

    private async Task<(bool ShouldReturn, object? Value)> HandleActionAsync(
        KSWorkflowParams args, KSAction action)
    {
        if (action.Result != null)
        {
            if (action.Result.RunId)
            {
                return (true, Workflow.Info.RunId);
            }
            return (true, action.Result.Value);
        }
        else if (action.Error != null)
        {
            if (action.Error.Attempt)
            {
                throw new ApplicationFailureException($"attempt {Workflow.Info.Attempt}");
            }
            IReadOnlyCollection<object?>? details = null;
            if (action.Error.Details != null)
            {
                details = new[] { action.Error.Details };
            }
            throw new ApplicationFailureException(
                action.Error.Message ?? string.Empty,
                details: details,
                category: action.Error.IsBenign ? ApplicationErrorCategory.Benign : ApplicationErrorCategory.Unspecified);
        }
        else if (action.ContinueAsNew != null)
        {
            // We only support this with one action
            if (args.Actions?.Count != 1)
            {
                throw new ApplicationFailureException("Only one action supported with continue as new");
            }
            if (action.ContinueAsNew.WhileAboveZero != null && action.ContinueAsNew.WhileAboveZero > 0)
            {
                args = new KSWorkflowParams(Actions: new[]
                {
                    action with
                    {
                        ContinueAsNew = action.ContinueAsNew with
                        {
                            WhileAboveZero = action.ContinueAsNew.WhileAboveZero - 1,
                        },
                    },
                });
                throw Workflow.CreateContinueAsNewException(
                    (IKitchenSinkWorkflow wf) => wf.RunAsync(args));
            }
            return (true, action.ContinueAsNew.Result);
        }
        else if (action.Sleep != null)
        {
            await Workflow.DelayAsync((int)action.Sleep.Millis);
            return (false, null);
        }
        else if (action.QueryHandler != null)
        {
            Workflow.Queries[action.QueryHandler.Name] = WorkflowQueryDefinition.CreateWithoutAttribute(
                action.QueryHandler.Name,
                (string arg) =>
                {
                    if (action.QueryHandler.Error != null)
                    {
                        throw new ApplicationFailureException(action.QueryHandler.Error);
                    }
                    return arg;
                });
            return (false, null);
        }
        else if (action.Signal != null)
        {
            // We have to wait for the signal
            var seenSignal = false;
            Workflow.Signals[action.Signal.Name] = WorkflowSignalDefinition.CreateWithoutAttribute(
                action.Signal.Name,
                (string arg) =>
                {
                    seenSignal = true;
                    return Task.CompletedTask;
                });
            await Workflow.WaitConditionAsync(() => seenSignal);
            return (false, null);
        }
        else if (action.ExecuteActivity != null)
        {
            var opts = new ActivityOptions()
            {
                TaskQueue = action.ExecuteActivity.TaskQueue,
                RetryPolicy = new()
                {
                    InitialInterval = TimeSpan.FromMilliseconds(1),
                    BackoffCoefficient = 1.01F,
                    MaximumInterval = TimeSpan.FromMilliseconds(2),
                    MaximumAttempts = 1,
                    NonRetryableErrorTypes = action.ExecuteActivity.NonRetryableErrorTypes ?? Array.Empty<string>(),
                },
            };
            if (action.ExecuteActivity.ScheduleToCloseTimeoutMS != null)
            {
                opts.ScheduleToCloseTimeout = TimeSpan.FromMilliseconds(
                    (double)action.ExecuteActivity.ScheduleToCloseTimeoutMS);
            }
            if (action.ExecuteActivity.StartToCloseTimeoutMS != null)
            {
                opts.StartToCloseTimeout = TimeSpan.FromMilliseconds(
                    (double)action.ExecuteActivity.StartToCloseTimeoutMS);
            }
            if (action.ExecuteActivity.ScheduleToStartTimeoutMS != null)
            {
                opts.ScheduleToStartTimeout = TimeSpan.FromMilliseconds(
                    (double)action.ExecuteActivity.ScheduleToStartTimeoutMS);
            }
            if (action.ExecuteActivity.WaitForCancellation)
            {
                opts.CancellationType = ActivityCancellationType.WaitCancellationCompleted;
            }
            if (action.ExecuteActivity.HeartbeatTimeoutMS != null)
            {
                opts.HeartbeatTimeout = TimeSpan.FromMilliseconds(
                    (double)action.ExecuteActivity.HeartbeatTimeoutMS);
            }
            if (action.ExecuteActivity.StartToCloseTimeoutMS == null &&
                action.ExecuteActivity.ScheduleToCloseTimeoutMS == null)
            {
                opts.ScheduleToCloseTimeout = TimeSpan.FromMinutes(3);
            }
            if (action.ExecuteActivity.RetryMaxAttempts != null)
            {
                opts.RetryPolicy.MaximumAttempts = action.ExecuteActivity.RetryMaxAttempts.Value;
            }
            // Build cancellation token source for delayed cancelling
            using var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(
                Workflow.CancellationToken);
            var tasks = new List<Task>();
            if (action.ExecuteActivity.CancelAfterMS != null)
            {
                tasks.Add(Task.Factory.StartNew(async () =>
                {
                    await Workflow.DelayAsync((int)action.ExecuteActivity.CancelAfterMS);
                    cancelSource.Cancel();
                }).Unwrap());
            }
            // Start all activities
            var activityTasks = Enumerable.Range(0, action.ExecuteActivity.Count ?? 1).Select(index =>
            {
                var activityArgs = action.ExecuteActivity.Args;
                if (action.ExecuteActivity.IndexAsArg)
                {
                    activityArgs = new object?[] { index };
                }
                return Workflow.ExecuteActivityAsync<object?>(
                    action.ExecuteActivity.Name, activityArgs ?? Array.Empty<object?>(), opts);
            }).ToList();
            tasks.AddRange(activityTasks);
            // Wait for any including cancel
            await Workflow.WhenAnyAsync(tasks);
            // Wait for every activity, return last result
            object? lastResult = null;
            while (activityTasks.Count > 0)
            {
                var task = await Workflow.WhenAnyAsync(activityTasks);
                activityTasks.Remove(task);
                lastResult = await task;
            }
            return (true, lastResult);
        }
        else
        {
            throw new ApplicationFailureException("Unrecognized action");
        }
    }
}