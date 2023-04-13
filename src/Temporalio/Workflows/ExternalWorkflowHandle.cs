#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Collections.Generic;
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
        public abstract string ID { get; }

        /// <summary>
        /// Gets the initial run ID of the child workflow.
        /// </summary>
        public abstract string? RunID { get; }

        /// <summary>
        /// Signal an external workflow.
        /// </summary>
        /// <param name="signal">Signal to send.</param>
        /// <param name="options">Options for the signal.</param>
        /// <returns>Task for completion of the signal send.</returns>
        public Task SignalAsync(Func<Task> signal, ExternalWorkflowSignalOptions? options = null) =>
            SignalAsync(
                WorkflowSignalDefinition.FromMethod(signal.Method).Name,
                Array.Empty<object?>(),
                options);

        /// <summary>
        /// Signal an external workflow.
        /// </summary>
        /// <typeparam name="T">Signal arg type.</typeparam>
        /// <param name="signal">Signal to send.</param>
        /// <param name="arg">Signal argument.</param>
        /// <param name="options">Options for the signal.</param>
        /// <returns>Task for completion of the signal send.</returns>
        public Task SignalAsync<T>(
            Func<T, Task> signal, T arg, ExternalWorkflowSignalOptions? options = null) =>
            SignalAsync(
                WorkflowSignalDefinition.FromMethod(signal.Method).Name,
                new object?[] { arg },
                options);

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
}