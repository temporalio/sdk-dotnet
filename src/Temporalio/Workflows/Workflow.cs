#pragma warning disable CA1724 // We know this clashes with Temporalio.Api.Workflow namespace

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Converters;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Static class with all calls that can be made from a workflow. Properties and methods on this
    /// class cannot be used outside of a workflow (with the obvious exception of
    /// <see cref="InWorkflow" />).
    /// </summary>
    public static class Workflow
    {
        /// <summary>
        /// Gets the cancellation token for the workflow.
        /// </summary>
        /// <remarks>
        /// This token is cancelled when the workflow is cancelled. When cancellation token is not
        /// provided to any method in this class, this cancellation token is the default.
        /// </remarks>
        public static CancellationToken CancellationToken => Context.CancellationToken;

        /// <summary>
        /// Gets information about the workflow.
        /// </summary>
        public static WorkflowInfo Info => Context.Info;

        /// <summary>
        /// Gets a value indicating whether this code is currently running in a workflow.
        /// </summary>
        public static bool InWorkflow => TaskScheduler.Current is IWorkflowContext;

        /// <summary>
        /// Gets the logger for the workflow. This is scoped with logger information and does not
        /// log during replay.
        /// </summary>
        public static ILogger Logger => Context.Logger;

        /// <summary>
        /// Gets the workflow memo.
        /// </summary>
        /// <remarks>
        /// This is read-only from the workflow author perspective. To update use
        /// <see cref="UpsertMemo" />. This always returns the same instance. Any workflow memo
        /// updates are immediately reflected on the returned instance, so it is not immutable.
        /// </remarks>
        public static IReadOnlyDictionary<string, IRawValue> Memo => Context.Memo;

        /// <summary>
        /// Gets queries for this workflow.
        /// </summary>
        /// <remarks>
        /// This dictionary can be mutated during workflow run. However, users are strongly
        /// encouraged to use fixed methods with the <c>[WorkflowQuery]</c> attribute.
        /// </remarks>
        public static IDictionary<string, WorkflowQueryDefinition> Queries => Context.Queries;

        /// <summary>
        /// Gets a random instance that is deterministic for workflow use.
        /// </summary>
        /// <remarks>
        /// This instance should be accessed each time needed, not stored. This instance may be
        /// recreated with a different seed in special cases (e.g. workflow reset). Do not use any
        /// other randomization inside workflow code.
        /// </remarks>
        public static Random Random => Context.Random;

        // TODO(cretz): Document that this is immutable from user POV but internally is mutated on
        // upsert

        /// <summary>
        /// Gets the workflow search attributes.
        /// </summary>
        /// <remarks>
        /// This is read-only from the workflow author perspective. To update use
        /// <see cref="UpsertTypedSearchAttributes" />. This always returns the same instance. Any
        /// workflow search attribute updates are immediately reflected on the returned instance, so
        /// it is not immutable.
        /// </remarks>
        public static SearchAttributeCollection TypedSearchAttributes =>
            Context.TypedSearchAttributes;

        /// <summary>
        /// Gets signals for this workflow.
        /// </summary>
        /// <remarks>
        /// This dictionary can be mutated during workflow run. However, users are strongly
        /// encouraged to use fixed methods with the <c>[WorkflowSignal]</c> attribute.
        /// </remarks>
        public static IDictionary<string, WorkflowSignalDefinition> Signals => Context.Signals;

        /// <summary>
        /// Gets the current timestamp for this workflow.
        /// </summary>
        /// <remarks>
        /// This value is deterministic and safe for replays. Do not use normal
        /// <see cref="DateTime.UtcNow" /> or anything else dealing with system time in workflows.
        /// </remarks>
        public static DateTime UtcNow => Context.UtcNow;

        private static IWorkflowContext Context =>
            TaskScheduler.Current as IWorkflowContext ?? throw new InvalidOperationException("Not in workflow");

        /// <summary>
        /// Create an exception that, when thrown out of the workflow, will continue-as-new with
        /// the given workflow.
        /// </summary>
        /// <param name="workflow">Workflow.</param>
        /// <param name="options">Continue as new options.</param>
        /// <returns>Exception for continuing as new.</returns>
        public static ContinueAsNewException CreateContinueAsNewException(
            Func<Task> workflow, ContinueAsNewOptions? options = null) =>
            CreateContinueAsNewException(
                WorkflowDefinition.FromRunMethod(workflow.Method).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Create an exception that, when thrown out of the workflow, will continue-as-new with
        /// the given workflow.
        /// </summary>
        /// <typeparam name="T">Workflow argument type.</typeparam>
        /// <param name="workflow">Workflow.</param>
        /// <param name="arg">Workflow argument.</param>
        /// <param name="options">Continue as new options.</param>
        /// <returns>Exception for continuing as new.</returns>
        public static ContinueAsNewException CreateContinueAsNewException<T>(
            Func<T, Task> workflow, T arg, ContinueAsNewOptions? options = null) =>
            CreateContinueAsNewException(
                WorkflowDefinition.FromRunMethod(workflow.Method).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Create an exception that, when thrown out of the workflow, will continue-as-new with
        /// the given workflow.
        /// </summary>
        /// <typeparam name="TResult">Workflow return type.</typeparam>
        /// <param name="workflow">Workflow.</param>
        /// <param name="options">Continue as new options.</param>
        /// <returns>Exception for continuing as new.</returns>
        public static ContinueAsNewException CreateContinueAsNewException<TResult>(
            Func<Task<TResult>> workflow, ContinueAsNewOptions? options = null) =>
            CreateContinueAsNewException(
                WorkflowDefinition.FromRunMethod(workflow.Method).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Create an exception that, when thrown out of the workflow, will continue-as-new with
        /// the given workflow.
        /// </summary>
        /// <typeparam name="T">Workflow argument type.</typeparam>
        /// <typeparam name="TResult">Workflow return type.</typeparam>
        /// <param name="workflow">Workflow.</param>
        /// <param name="arg">Workflow argument.</param>
        /// <param name="options">Continue as new options.</param>
        /// <returns>Exception for continuing as new.</returns>
        public static ContinueAsNewException CreateContinueAsNewException<T, TResult>(
            Func<T, Task<TResult>> workflow, T arg, ContinueAsNewOptions? options = null) =>
            CreateContinueAsNewException(
                WorkflowDefinition.FromRunMethod(workflow.Method).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Create an exception that, when thrown out of the workflow, will continue-as-new with
        /// the given workflow.
        /// </summary>
        /// <param name="workflow">Workflow name.</param>
        /// <param name="args">Workflow arguments.</param>
        /// <param name="options">Continue as new options.</param>
        /// <returns>Exception for continuing as new.</returns>
        public static ContinueAsNewException CreateContinueAsNewException(
            string workflow,
            IReadOnlyCollection<object?> args,
            ContinueAsNewOptions? options = null) =>
            Context.CreateContinueAsNewException(workflow, args, options);

        /// <summary>
        /// Sleep in a workflow for the given time. See documentation of
        /// <see cref="DelayAsync(TimeSpan, CancellationToken?)" /> for details.
        /// </summary>
        /// <param name="millisecondsDelay">Delay amount. See documentation of
        /// <see cref="DelayAsync(TimeSpan, CancellationToken?)" /> for details.</param>
        /// <param name="cancellationToken">Cancellation token. See documentation of
        /// <see cref="DelayAsync(TimeSpan, CancellationToken?)" /> for details.</param>
        /// <returns>Task for completion. See documentation of
        /// <see cref="DelayAsync(TimeSpan, CancellationToken?)" /> for details.</returns>
        /// <seealso cref="DelayAsync(TimeSpan, CancellationToken?)" />
        public static Task DelayAsync(int millisecondsDelay, CancellationToken? cancellationToken = null) =>
            DelayAsync(TimeSpan.FromMilliseconds(millisecondsDelay), cancellationToken);

        /// <summary>
        /// Sleep in a workflow for the given time.
        /// </summary>
        /// <param name="delay">Amount of time to sleep.</param>
        /// <param name="cancellationToken">Cancellation token. If unset, this defaults to
        /// <see cref="CancellationToken" />.</param>
        /// <returns>Task that is complete when sleep completes.</returns>
        /// <remarks>
        /// <para>
        /// The <c>delay</c> value can be <see cref="Timeout.Infinite" /> or
        /// <see cref="Timeout.InfiniteTimeSpan" /> but otherwise cannot be negative. A server-side
        /// timer is not created for infinite delays, so it is non-deterministic to change a timer
        /// to/from infinite from/to an actual value.
        /// </para>
        /// <para>
        /// If the <c>delay</c> is 0, it is assumed to be 1 millisecond and still results in a
        /// server-side timer. Since Temporal timers are server-side, timer resolution may not end
        /// up as precise as system timers.
        /// </para>
        /// </remarks>
        public static Task DelayAsync(TimeSpan delay, CancellationToken? cancellationToken = null) =>
            Context.DelayAsync(delay, cancellationToken);

        /// <summary>
        /// Mark a patch as deprecated.
        /// </summary>
        /// <param name="patchID">Patch ID.</param>
        /// <remarks>
        /// This marks a workflow that had <see cref="Patched" /> in a previous version of the code
        /// as no longer applicable because all workflows that use the old code path are done and
        /// will never be queried again. Therefore the old code path is removed as well.
        /// </remarks>
        public static void DeprecatePatch(string patchID) =>
            Context.Patch(patchID, deprecated: true);

        /// <summary>
        /// Execute an activity with a result and no arguments.
        /// </summary>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion with result.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task<TResult> ExecuteActivityAsync<TResult>(
            Func<TResult> activity, ActivityOptions options) =>
            ExecuteActivityAsync<TResult>(
                Activities.ActivityDefinition.Create(activity).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Execute an activity with a result and one argument.
        /// </summary>
        /// <typeparam name="T">Activity argument type.</typeparam>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="arg">Activity argument.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion with result.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task<TResult> ExecuteActivityAsync<T, TResult>(
            Func<T, TResult> activity, T arg, ActivityOptions options) =>
            ExecuteActivityAsync<TResult>(
                Activities.ActivityDefinition.Create(activity).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Execute an activity with no result and no arguments.
        /// </summary>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task ExecuteActivityAsync(
            Action activity, ActivityOptions options) =>
            ExecuteActivityAsync(
                Activities.ActivityDefinition.Create(activity).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Execute an activity with no result and one argument.
        /// </summary>
        /// <typeparam name="T">Activity argument type.</typeparam>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="arg">Activity argument.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task ExecuteActivityAsync<T>(
            Action<T> activity, T arg, ActivityOptions options) =>
            ExecuteActivityAsync(
                Activities.ActivityDefinition.Create(activity).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Execute an activity with a result and no arguments.
        /// </summary>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion with result.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task<TResult> ExecuteActivityAsync<TResult>(
            Func<Task<TResult>> activity, ActivityOptions options) =>
            ExecuteActivityAsync<TResult>(
                Activities.ActivityDefinition.Create(activity).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Execute an activity with a result and one argument.
        /// </summary>
        /// <typeparam name="T">Activity argument type.</typeparam>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="arg">Activity argument.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion with result.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task<TResult> ExecuteActivityAsync<T, TResult>(
            Func<T, Task<TResult>> activity, T arg, ActivityOptions options) =>
            ExecuteActivityAsync<TResult>(
                Activities.ActivityDefinition.Create(activity).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Execute an activity with no result and no arguments.
        /// </summary>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task ExecuteActivityAsync(
            Func<Task> activity, ActivityOptions options) =>
            ExecuteActivityAsync(
                Activities.ActivityDefinition.Create(activity).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Execute an activity with no result and one argument.
        /// </summary>
        /// <typeparam name="T">Activity argument type.</typeparam>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="arg">Activity argument.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task ExecuteActivityAsync<T>(
            Func<T, Task> activity, T arg, ActivityOptions options) =>
            ExecuteActivityAsync(
                Activities.ActivityDefinition.Create(activity).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Execute an activity by name with no result and any number of arguments.
        /// </summary>
        /// <param name="activity">Activity name to execute.</param>
        /// <param name="args">Activity arguments.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task ExecuteActivityAsync(
            string activity, IReadOnlyCollection<object?> args, ActivityOptions options) =>
            ExecuteActivityAsync<ValueTuple>(activity, args, options);

        /// <summary>
        /// Execute an activity by name with a result and any number of arguments.
        /// </summary>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activity">Activity name to execute.</param>
        /// <param name="args">Activity arguments.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion with result.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task<TResult> ExecuteActivityAsync<TResult>(
            string activity, IReadOnlyCollection<object?> args, ActivityOptions options) =>
            Context.ExecuteActivityAsync<TResult>(activity, args, options);

        /// <summary>
        /// Shortcut for
        /// <see cref="StartChildWorkflowAsync{TResult}(Func{Task{TResult}}, ChildWorkflowOptions?)" />
        /// +
        /// <see cref="ChildWorkflowHandle{TResult}.GetResultAsync" />.
        /// </summary>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflow">Workflow to execute.</param>
        /// <param name="options">Workflow options.</param>
        /// <returns>Task for workflow completion.</returns>
        public static async Task<TResult> ExecuteChildWorkflowAsync<TResult>(
            Func<Task<TResult>> workflow, ChildWorkflowOptions? options = null)
        {
            var handle = await StartChildWorkflowAsync(workflow, options).ConfigureAwait(true);
            return await handle.GetResultAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="StartChildWorkflowAsync{T, TResult}(Func{T, Task{TResult}}, T, ChildWorkflowOptions?)" />
        /// +
        /// <see cref="ChildWorkflowHandle{TResult}.GetResultAsync" />.
        /// </summary>
        /// <typeparam name="T">Workflow argument type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflow">Workflow to execute.</param>
        /// <param name="arg">Workflow argument.</param>
        /// <param name="options">Workflow options.</param>
        /// <returns>Task for workflow completion.</returns>
        public static async Task<TResult> ExecuteChildWorkflowAsync<T, TResult>(
            Func<T, Task<TResult>> workflow, T arg, ChildWorkflowOptions? options = null)
        {
            var handle = await StartChildWorkflowAsync(
                workflow, arg, options).ConfigureAwait(true);
            return await handle.GetResultAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="StartChildWorkflowAsync(Func{Task}, ChildWorkflowOptions?)" />
        /// +
        /// <see cref="ChildWorkflowHandle.GetResultAsync" />.
        /// </summary>
        /// <param name="workflow">Workflow to execute.</param>
        /// <param name="options">Workflow options.</param>
        /// <returns>Task for workflow completion.</returns>
        public static async Task ExecuteChildWorkflowAsync(
            Func<Task> workflow, ChildWorkflowOptions? options = null)
        {
            var handle = await StartChildWorkflowAsync(workflow, options).ConfigureAwait(true);
            await handle.GetResultAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="StartChildWorkflowAsync{T}(Func{T, Task}, T, ChildWorkflowOptions?)" />
        /// +
        /// <see cref="ChildWorkflowHandle.GetResultAsync" />.
        /// </summary>
        /// <typeparam name="T">Workflow argument type.</typeparam>
        /// <param name="workflow">Workflow to execute.</param>
        /// <param name="arg">Workflow argument.</param>
        /// <param name="options">Workflow options.</param>
        /// <returns>Task for workflow completion.</returns>
        public static async Task ExecuteChildWorkflowAsync<T>(
            Func<T, Task> workflow, T arg, ChildWorkflowOptions? options = null)
        {
            var handle = await StartChildWorkflowAsync(
                workflow, arg, options).ConfigureAwait(true);
            await handle.GetResultAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="StartChildWorkflowAsync(string, IReadOnlyCollection{object?}, ChildWorkflowOptions?)" />
        /// +
        /// <see cref="ChildWorkflowHandle.GetResultAsync" />.
        /// </summary>
        /// <param name="workflow">Workflow name to execute.</param>
        /// <param name="args">Workflow arguments.</param>
        /// <param name="options">Workflow options.</param>
        /// <returns>Task for workflow completion.</returns>
        public static async Task ExecuteChildWorkflowAsync(
            string workflow, IReadOnlyCollection<object?> args, ChildWorkflowOptions? options = null)
        {
            var handle = await StartChildWorkflowAsync(
                workflow, args, options).ConfigureAwait(true);
            await handle.GetResultAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="StartChildWorkflowAsync{TResult}(string, IReadOnlyCollection{object?}, ChildWorkflowOptions?)" />
        /// +
        /// <see cref="ChildWorkflowHandle{TResult}.GetResultAsync" />.
        /// </summary>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflow">Workflow name to execute.</param>
        /// <param name="args">Workflow arguments.</param>
        /// <param name="options">Workflow options.</param>
        /// <returns>Task for workflow completion.</returns>
        public static async Task<TResult> ExecuteChildWorkflowAsync<TResult>(
            string workflow, IReadOnlyCollection<object?> args, ChildWorkflowOptions? options = null)
        {
            var handle = await StartChildWorkflowAsync<TResult>(
                workflow, args, options).ConfigureAwait(true);
            return await handle.GetResultAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Execute a local activity with a result and no arguments.
        /// </summary>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion with result.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task<TResult> ExecuteLocalActivityAsync<TResult>(
            Func<TResult> activity, LocalActivityOptions options) =>
            ExecuteLocalActivityAsync<TResult>(
                Activities.ActivityDefinition.Create(activity).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Execute a local activity with a result and one argument.
        /// </summary>
        /// <typeparam name="T">Activity argument type.</typeparam>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="arg">Activity argument.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion with result.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task<TResult> ExecuteLocalActivityAsync<T, TResult>(
            Func<T, TResult> activity, T arg, LocalActivityOptions options) =>
            ExecuteLocalActivityAsync<TResult>(
                Activities.ActivityDefinition.Create(activity).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Execute a local activity with no result and no arguments.
        /// </summary>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task ExecuteLocalActivityAsync(
            Action activity, LocalActivityOptions options) =>
            ExecuteLocalActivityAsync(
                Activities.ActivityDefinition.Create(activity).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Execute a local activity with no result and one argument.
        /// </summary>
        /// <typeparam name="T">Activity argument type.</typeparam>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="arg">Activity argument.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task ExecuteLocalActivityAsync<T>(
            Action<T> activity, T arg, LocalActivityOptions options) =>
            ExecuteLocalActivityAsync(
                Activities.ActivityDefinition.Create(activity).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Execute a local activity with a result and no arguments.
        /// </summary>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion with result.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task<TResult> ExecuteLocalActivityAsync<TResult>(
            Func<Task<TResult>> activity, LocalActivityOptions options) =>
            ExecuteLocalActivityAsync<TResult>(
                Activities.ActivityDefinition.Create(activity).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Execute a local activity with a result and one argument.
        /// </summary>
        /// <typeparam name="T">Activity argument type.</typeparam>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="arg">Activity argument.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion with result.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task<TResult> ExecuteLocalActivityAsync<T, TResult>(
            Func<T, Task<TResult>> activity, T arg, LocalActivityOptions options) =>
            ExecuteLocalActivityAsync<TResult>(
                Activities.ActivityDefinition.Create(activity).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Execute a local activity with no result and no arguments.
        /// </summary>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task ExecuteLocalActivityAsync(
            Func<Task> activity, LocalActivityOptions options) =>
            ExecuteLocalActivityAsync(
                Activities.ActivityDefinition.Create(activity).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Execute a local activity with no result and one argument.
        /// </summary>
        /// <typeparam name="T">Activity argument type.</typeparam>
        /// <param name="activity">Activity to execute.</param>
        /// <param name="arg">Activity argument.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task ExecuteLocalActivityAsync<T>(
            Func<T, Task> activity, T arg, LocalActivityOptions options) =>
            ExecuteLocalActivityAsync(
                Activities.ActivityDefinition.Create(activity).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Execute a local activity by name with no result and any number of arguments.
        /// </summary>
        /// <param name="activity">Activity name to execute.</param>
        /// <param name="args">Activity arguments.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task ExecuteLocalActivityAsync(
            string activity, IReadOnlyCollection<object?> args, LocalActivityOptions options) =>
            ExecuteLocalActivityAsync<ValueTuple>(activity, args, options);

        /// <summary>
        /// Execute a local activity by name with a result and any number of arguments.
        /// </summary>
        /// <typeparam name="TResult">Activity result type.</typeparam>
        /// <param name="activity">Activity name to execute.</param>
        /// <param name="args">Activity arguments.</param>
        /// <param name="options">Activity options. This is required and either
        /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
        /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
        /// <returns>Task for completion with result.</returns>
        /// <remarks>
        /// The task will throw an <see cref="Exceptions.ActivityFailureException" /> on activity
        /// failure.
        /// </remarks>
        public static Task<TResult> ExecuteLocalActivityAsync<TResult>(
            string activity, IReadOnlyCollection<object?> args, LocalActivityOptions options) =>
            Context.ExecuteLocalActivityAsync<TResult>(activity, args, options);

        /// <summary>
        /// Get a handle to an external workflow for cancelling and issuing signals.
        /// </summary>
        /// <param name="id">Workflow ID.</param>
        /// <param name="runID">Optional workflow run ID.</param>
        /// <returns>External workflow handle.</returns>
        public static ExternalWorkflowHandle GetExternalWorkflowHandle(
            string id, string? runID = null) => Context.GetExternalWorkflowHandle(id, runID);

        /// <summary>
        /// Deterministically create a new <see cref="Guid" /> similar to
        /// <see cref="Guid.NewGuid" /> (which cannot be used in workflows). The resulting GUID
        /// intentionally represents a version 4 UUID.
        /// </summary>
        /// <returns>A new GUID.</returns>
        public static Guid NewGuid()
        {
            var bytes = new byte[16];
#pragma warning disable CA5394 // We intentionally are not wanting strong random
            Random.NextBytes(bytes);
#pragma warning restore CA5394
            // Make it look like UUIDv4
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
            return new(bytes);
        }

        /// <summary>
        /// Patch a workflow.
        /// </summary>
        /// <param name="patchID">Patch ID.</param>
        /// <returns>True if this should take the newer patch, false if it should take the old
        /// path.</returns>
        /// <remarks>
        /// <para>
        /// When called, this will only return true if code should take the newer path which means
        /// this is either not replaying or is replaying and has seen this patch before. Results for
        /// successive calls to this function for the same ID and workflow are memoized.
        /// </para>
        /// <para>
        /// Use <see cref="DeprecatePatch" /> when all workflows are done and will never be queried
        /// again. The old code path can be removed at that time too.
        /// </para>
        /// </remarks>
        public static bool Patched(string patchID) => Context.Patch(patchID, deprecated: false);

        /// <summary>
        /// Start a child workflow with a result and no arguments.
        /// </summary>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflow">Workflow to execute.</param>
        /// <param name="options">Workflow options.</param>
        /// <returns>The child workflow handle once started.</returns>
        /// <remarks>
        /// The task can throw a <see cref="Exceptions.WorkflowAlreadyStartedException" /> if an ID
        /// is given in the options but it is already running.
        /// </remarks>
        public static Task<ChildWorkflowHandle<TResult>> StartChildWorkflowAsync<TResult>(
            Func<Task<TResult>> workflow, ChildWorkflowOptions? options = null) =>
            StartChildWorkflowAsync<TResult>(
                WorkflowDefinition.FromRunMethod(workflow.Method).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Start a child workflow with a result and one argument.
        /// </summary>
        /// <typeparam name="T">Workflow argument type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflow">Workflow to execute.</param>
        /// <param name="arg">Workflow argument.</param>
        /// <param name="options">Workflow options.</param>
        /// <returns>The child workflow handle once started.</returns>
        /// <remarks>
        /// The task can throw a <see cref="Exceptions.WorkflowAlreadyStartedException" /> if an ID
        /// is given in the options but it is already running.
        /// </remarks>
        public static Task<ChildWorkflowHandle<TResult>> StartChildWorkflowAsync<T, TResult>(
            Func<T, Task<TResult>> workflow, T arg, ChildWorkflowOptions? options = null) =>
            StartChildWorkflowAsync<TResult>(
                WorkflowDefinition.FromRunMethod(workflow.Method).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Start a child workflow with no result and no arguments.
        /// </summary>
        /// <param name="workflow">Workflow to execute.</param>
        /// <param name="options">Workflow options.</param>
        /// <returns>The child workflow handle once started.</returns>
        /// <remarks>
        /// The task can throw a <see cref="Exceptions.WorkflowAlreadyStartedException" /> if an ID
        /// is given in the options but it is already running.
        /// </remarks>
        public static Task<ChildWorkflowHandle> StartChildWorkflowAsync(
            Func<Task> workflow, ChildWorkflowOptions? options = null) =>
            StartChildWorkflowAsync(
                WorkflowDefinition.FromRunMethod(workflow.Method).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Start a child workflow with no result and one argument.
        /// </summary>
        /// <typeparam name="T">Workflow argument type.</typeparam>
        /// <param name="workflow">Workflow to execute.</param>
        /// <param name="arg">Workflow argument.</param>
        /// <param name="options">Workflow options.</param>
        /// <returns>The child workflow handle once started.</returns>
        /// <remarks>
        /// The task can throw a <see cref="Exceptions.WorkflowAlreadyStartedException" /> if an ID
        /// is given in the options but it is already running.
        /// </remarks>
        public static Task<ChildWorkflowHandle> StartChildWorkflowAsync<T>(
            Func<T, Task> workflow, T arg, ChildWorkflowOptions? options = null) =>
            StartChildWorkflowAsync(
                WorkflowDefinition.FromRunMethod(workflow.Method).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Start a child workflow by name with no result and any number of argument.
        /// </summary>
        /// <param name="workflow">Workflow name to execute.</param>
        /// <param name="args">Workflow arguments.</param>
        /// <param name="options">Workflow options.</param>
        /// <returns>The child workflow handle once started.</returns>
        /// <remarks>
        /// The task can throw a <see cref="Exceptions.WorkflowAlreadyStartedException" /> if an ID
        /// is given in the options but it is already running.
        /// </remarks>
        public static async Task<ChildWorkflowHandle> StartChildWorkflowAsync(
            string workflow, IReadOnlyCollection<object?> args, ChildWorkflowOptions? options = null) =>
            await StartChildWorkflowAsync<ValueTuple>(workflow, args, options).ConfigureAwait(true);

        /// <summary>
        /// Start a child workflow by name with a result and any number of arguments.
        /// </summary>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflow">Workflow name to execute.</param>
        /// <param name="args">Workflow arguments.</param>
        /// <param name="options">Workflow options.</param>
        /// <returns>The child workflow handle once started.</returns>
        /// <remarks>
        /// The task can throw a <see cref="Exceptions.WorkflowAlreadyStartedException" /> if an ID
        /// is given in the options but it is already running.
        /// </remarks>
        public static Task<ChildWorkflowHandle<TResult>> StartChildWorkflowAsync<TResult>(
            string workflow, IReadOnlyCollection<object?> args, ChildWorkflowOptions? options = null) =>
            Context.StartChildWorkflowAsync<TResult>(workflow, args, options ?? new());

        /// <summary>
        /// Issue updates to the workflow memo.
        /// </summary>
        /// <param name="updates">Updates to issue.</param>
        /// <exception cref="ArgumentException">If no updates given, two updates are given for a
        /// key, or an update value cannot be converted.</exception>
        public static void UpsertMemo(params MemoUpdate[] updates) =>
            Context.UpsertMemo(updates);

        /// <summary>
        /// Issue updates to the workflow search attributes.
        /// </summary>
        /// <param name="updates">Updates to issue.</param>
        /// <exception cref="ArgumentException">If no updates given or two updates are given for a
        /// key.</exception>
        public static void UpsertTypedSearchAttributes(params SearchAttributeUpdate[] updates) =>
            Context.UpsertTypedSearchAttributes(updates);

        /// <summary>
        /// Wait for the given function to return true. See documentation of
        /// <see cref="WaitConditionAsync(Func{bool}, TimeSpan, CancellationToken?)" /> for more
        /// details.
        /// </summary>
        /// <param name="conditionCheck">Condition function. See documentation of
        /// <see cref="WaitConditionAsync(Func{bool}, TimeSpan, CancellationToken?)" /> for more
        /// details.</param>
        /// <param name="cancellationToken">Cancellation token. See documentation of
        /// <see cref="WaitConditionAsync(Func{bool}, TimeSpan, CancellationToken?)" /> for more
        /// details.</param>
        /// <returns>Task when condition becomes true. See documentation
        /// of <see cref="WaitConditionAsync(Func{bool}, TimeSpan, CancellationToken?)" /> for more
        /// details.</returns>
        /// <seealso cref="WaitConditionAsync(Func{bool}, TimeSpan, CancellationToken?)" />.
        public static Task WaitConditionAsync(
            Func<bool> conditionCheck, CancellationToken? cancellationToken = null) =>
                Context.WaitConditionAsync(conditionCheck, null, cancellationToken);

        /// <summary>
        /// Wait for the given function to return true or a timeout. See documentation of
        /// <see cref="WaitConditionAsync(Func{bool}, TimeSpan, CancellationToken?)" /> for more
        /// details.
        /// </summary>
        /// <param name="conditionCheck">Condition function. See documentation of
        /// <see cref="WaitConditionAsync(Func{bool}, TimeSpan, CancellationToken?)" /> for more
        /// details.</param>
        /// <param name="timeoutMilliseconds">Timeout milliseconds. See documentation of
        /// <see cref="WaitConditionAsync(Func{bool}, TimeSpan, CancellationToken?)" /> for more
        /// details.</param>
        /// <param name="cancellationToken">Cancellation token. See documentation of
        /// <see cref="WaitConditionAsync(Func{bool}, TimeSpan, CancellationToken?)" /> for more
        /// details.</param>
        /// <returns>Task when condition becomes true or a timeout has occurred. See documentation
        /// of <see cref="WaitConditionAsync(Func{bool}, TimeSpan, CancellationToken?)" /> for more
        /// details.</returns>
        /// <seealso cref="WaitConditionAsync(Func{bool}, TimeSpan, CancellationToken?)" />.
        public static Task<bool> WaitConditionAsync(
            Func<bool> conditionCheck,
            int timeoutMilliseconds,
            CancellationToken? cancellationToken = null) =>
                Context.WaitConditionAsync(
                    conditionCheck,
                    TimeSpan.FromMilliseconds(timeoutMilliseconds),
                    cancellationToken);

        /// <summary>
        /// Wait for the given function to return true or a timeout.
        /// </summary>
        /// <param name="conditionCheck">Condition function.</param>
        /// <param name="timeout">Optional timeout for waiting.</param>
        /// <param name="cancellationToken">Cancellation token. If unset, this defaults to
        /// <see cref="CancellationToken" />.</param>
        /// <returns>Task with <c>true</c> when condition becomes true or <c>false</c> if a timeout
        /// occurs.</returns>
        /// <remarks>
        /// The <c>conditionCheck</c> function is invoked on each iteration of the event loop.
        /// Therefore, it should be fast and side-effect free.
        /// </remarks>
        public static Task<bool> WaitConditionAsync(
            Func<bool> conditionCheck, TimeSpan timeout, CancellationToken? cancellationToken = null) =>
                Context.WaitConditionAsync(conditionCheck, timeout, cancellationToken);

        /// <summary>
        /// Unsafe calls that can be made in a workflow.
        /// </summary>
        public static class Unsafe
        {
            /// <summary>
            /// Gets a value indicating whether this workflow is replaying.
            /// </summary>
            /// <remarks>
            /// This should not be used for most cases. It is only valuable for advanced cases like
            /// preventing a log or metric from being recorded on replay.
            /// </remarks>
            public static bool IsReplaying => Context.IsReplaying;
        }
    }
}