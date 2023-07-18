#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Handle representing an external workflow.
    /// </summary>
    public abstract class ExternalWorkflowHandle
    {
        /// <summary>
        /// Gets the ID of the external workflow.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Gets the initial run ID of the child workflow.
        /// </summary>
        public abstract string? RunId { get; }

        /// <summary>
        /// Signal an external workflow via a lambda call to a WorkflowSignal attributed method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="signalCall">Invocation of a workflow signal method.</param>
        /// <param name="options">Options for the signal.</param>
        /// <returns>Task for completion of the signal send.</returns>
        public Task SignalAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> signalCall,
            ExternalWorkflowSignalOptions? options = null)
        {
            var (method, args) = Common.ExpressionUtil.ExtractCall(signalCall);
            return SignalAsync(
                WorkflowSignalDefinition.NameFromMethodForCall(method),
                args,
                options);
        }

        /// <summary>
        /// Signal an external workflow.
        /// </summary>
        /// <param name="signal">Signal to send.</param>
        /// <param name="args">Signal arguments.</param>
        /// <param name="options">Options for the signal.</param>
        /// <returns>Task for completion of the signal send.</returns>
        public abstract Task SignalAsync(
            string signal,
            IReadOnlyCollection<object?> args,
            ExternalWorkflowSignalOptions? options = null);

        /// <summary>
        /// Cancel an external workflow.
        /// </summary>
        /// <returns>Task for completion of the cancellation request.</returns>
        public abstract Task CancelAsync();
    }

    /// <inheritdoc />
    public abstract class ExternalWorkflowHandle<TWorkflow> : ExternalWorkflowHandle
    {
        /// <summary>
        /// Signal an external workflow via a lambda call to a WorkflowSignal attributed method.
        /// </summary>
        /// <param name="signalCall">Invocation of a workflow signal method.</param>
        /// <param name="options">Options for the signal.</param>
        /// <returns>Task for completion of the signal send.</returns>
        public Task SignalAsync(
            Expression<Func<TWorkflow, Task>> signalCall,
            ExternalWorkflowSignalOptions? options = null) =>
            SignalAsync<TWorkflow>(signalCall, options);
    }
}