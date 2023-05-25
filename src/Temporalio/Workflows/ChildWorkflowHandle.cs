#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
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
        /// Signal a child workflow via a lambda call to a WorkflowSignal attributed method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="signalCall">Invocation of a workflow signal method.</param>
        /// <param name="options">Options for the signal.</param>
        /// <returns>Task for completion of the signal send.</returns>
        public Task SignalAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> signalCall,
            ChildWorkflowSignalOptions? options = null)
        {
            var (method, args) = Common.ExpressionUtil.ExtractCall(signalCall);
            return SignalAsync(
                WorkflowSignalDefinition.FromMethod(method).Name,
                args,
                options);
        }

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
    public abstract class ChildWorkflowHandle<TWorkflow> : ChildWorkflowHandle
    {
        /// <summary>
        /// Signal a child workflow via a lambda call to a WorkflowSignal attributed method.
        /// </summary>
        /// <param name="signalCall">Invocation of a workflow signal method.</param>
        /// <param name="options">Options for the signal.</param>
        /// <returns>Task for completion of the signal send.</returns>
        public Task SignalAsync(
            Expression<Func<TWorkflow, Task>> signalCall,
            ChildWorkflowSignalOptions? options = null) =>
            SignalAsync<TWorkflow>(signalCall, options);
    }

    /// <inheritdoc />
    public abstract class ChildWorkflowHandle<TWorkflow, TResult> : ChildWorkflowHandle<TWorkflow>
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