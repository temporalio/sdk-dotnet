#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Handle representing a started child workflow.
    /// </summary>
    public abstract class ChildWorkflowHandle
    {
        /// <summary>
        /// Gets the ID of the child workflow.
        /// </summary>
        public abstract string ID { get; }

        /// <summary>
        /// Gets the initial run ID of the child workflow.
        /// </summary>
        public abstract string FirstExecutionRunID { get; }

        /// <summary>
        /// Wait for result of the child workflow, ignoring the return value.
        /// </summary>
        /// <returns>Task representing completion.</returns>
        /// <exception cref="Exceptions.ChildWorkflowFailureException">Any child workflow
        /// failure.</exception>
        public Task GetResultAsync() => GetResultAsync<ValueTuple>();

        /// <summary>
        /// Wait for the result of the child workflow.
        /// </summary>
        /// <typeparam name="TResult">Result type to convert to.</typeparam>
        /// <returns>Task with result.</returns>
        /// <exception cref="Exceptions.ChildWorkflowFailureException">Any child workflow
        /// failure.</exception>
        /// <exception cref="Exception">Failure converting to result type.</exception>
        public abstract Task<TResult> GetResultAsync<TResult>();

        /// <summary>
        /// Signal a child workflow.
        /// </summary>
        /// <param name="signal">Signal to send.</param>
        /// <param name="options">Options for the signal.</param>
        /// <returns>Task for completion of the signal send.</returns>
        public Task SignalAsync(Func<Task> signal, ChildWorkflowSignalOptions? options = null) =>
            SignalAsync(
                WorkflowSignalDefinition.FromMethod(signal.Method).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Signal a child workflow.
        /// </summary>
        /// <typeparam name="T">Signal arg type.</typeparam>
        /// <param name="signal">Signal to send.</param>
        /// <param name="arg">Signal argument.</param>
        /// <param name="options">Options for the signal.</param>
        /// <returns>Task for completion of the signal send.</returns>
        public Task SignalAsync<T>(
            Func<T, Task> signal, T arg, ChildWorkflowSignalOptions? options = null) =>
            SignalAsync(
                WorkflowSignalDefinition.FromMethod(signal.Method).Name,
                new object?[] { arg },
                options);

        /// <summary>
        /// Signal a child workflow.
        /// </summary>
        /// <param name="signal">Signal to send.</param>
        /// <param name="args">Signal arguments.</param>
        /// <param name="options">Options for the signal.</param>
        /// <returns>Task for completion of the signal send.</returns>
        public abstract Task SignalAsync(
            string signal,
            IReadOnlyCollection<object?> args,
            ChildWorkflowSignalOptions? options = null);
    }

    /// <inheritdoc />
    public abstract class ChildWorkflowHandle<TResult> : ChildWorkflowHandle
    {
        /// <summary>
        /// Get a result with the known result type.
        /// </summary>
        /// <returns>Task with result.</returns>
        /// <exception cref="Exceptions.ChildWorkflowFailureException">Any child workflow
        /// failure.</exception>
        public new Task<TResult> GetResultAsync() => GetResultAsync<TResult>();
    }
}