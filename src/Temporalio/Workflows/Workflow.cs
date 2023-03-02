#pragma warning disable CA1724 // We know this clashes with Temporalio.Api.Workflow namespace

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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