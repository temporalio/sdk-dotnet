#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Handle representing a started Nexus operation.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public abstract class NexusOperationHandle
    {
        /// <summary>
        /// Gets the operation token.
        /// </summary>
        public abstract string? OperationToken { get; }

        /// <summary>
        /// Wait for result of the operation, ignoring the return value.
        /// </summary>
        /// <returns>Task representing completion.</returns>
        /// <exception cref="Exceptions.NexusOperationFailureException">Any Nexus operation failure.
        /// </exception>
        public Task GetResultAsync() => GetResultAsync<ValueTuple>();

        /// <summary>
        /// Wait for result of the operation.
        /// </summary>
        /// <typeparam name="TResult">Operation result type.</typeparam>
        /// <returns>Task representing completion.</returns>
        /// <exception cref="Exceptions.NexusOperationFailureException">Any Nexus operation failure.
        /// </exception>
        public abstract Task<TResult> GetResultAsync<TResult>();
    }

    /// <inheritdoc />
    public abstract class NexusOperationHandle<TResult> : NexusOperationHandle
    {
        /// <summary>
        /// Wait for result of the operation.
        /// </summary>
        /// <returns>Task representing completion.</returns>
        /// <exception cref="Exceptions.NexusOperationFailureException">Any Nexus operation failure.
        /// </exception>
        public new Task<TResult> GetResultAsync() => GetResultAsync<TResult>();
    }
}