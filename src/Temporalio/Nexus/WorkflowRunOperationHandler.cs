#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Threading.Tasks;
using NexusRpc;
using NexusRpc.Handlers;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Helpers for creating operation handlers backed by Temporal workflows.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public static class WorkflowRunOperationHandler
    {
        /// <summary>
        /// Create an operation handler with no return and no input from the given handle factory.
        /// </summary>
        /// <param name="handleFactory">Handle factory invoked on every operation start. The factory
        /// function is expected to use the given context to <c>StartWorkflowAsync</c> and return
        /// the workflow run handle.</param>
        /// <returns>Operation handler backed by a workflow.</returns>
        public static IOperationHandler<NoValue, NoValue> FromHandleFactory(
            Func<WorkflowRunOperationContext, Task<NexusWorkflowRunHandle>> handleFactory) =>
            new WorkflowRunOperationHandler<NoValue, NoValue>(
                async (context, _) => await handleFactory(context).ConfigureAwait(false));

        /// <summary>
        /// Create an operation handler with no return from the given handle factory.
        /// </summary>
        /// <typeparam name="TInput">Operation input type.</typeparam>
        /// <param name="handleFactory">Handle factory invoked on every operation start. The factory
        /// function is expected to use the given context to <c>StartWorkflowAsync</c> and return
        /// the workflow run handle.</param>
        /// <returns>Operation handler backed by a workflow.</returns>
        public static IOperationHandler<TInput, NoValue> FromHandleFactory<TInput>(
            Func<WorkflowRunOperationContext, TInput, Task<NexusWorkflowRunHandle>> handleFactory) =>
            new WorkflowRunOperationHandler<TInput, NoValue>(
                async (context, input) => await handleFactory(context, input).ConfigureAwait(false));

        /// <summary>
        /// Create an operation handler with no input from the given handle factory.
        /// </summary>
        /// <typeparam name="TResult">Operation return type.</typeparam>
        /// <param name="handleFactory">Handle factory invoked on every operation start. The factory
        /// function is expected to use the given context to <c>StartWorkflowAsync</c> and return
        /// the workflow run handle.</param>
        /// <returns>Operation handler backed by a workflow.</returns>
        public static IOperationHandler<NoValue, TResult> FromHandleFactory<TResult>(
            Func<WorkflowRunOperationContext, Task<NexusWorkflowRunHandle<TResult>>> handleFactory) =>
            new WorkflowRunOperationHandler<NoValue, TResult>(
                async (context, _) => await handleFactory(context).ConfigureAwait(false));

        /// <summary>
        /// Create an operation handler from the given handle factory.
        /// </summary>
        /// <typeparam name="TInput">Operation input type.</typeparam>
        /// <typeparam name="TResult">Operation return type.</typeparam>
        /// <param name="handleFactory">Handle factory invoked on every operation start. The factory
        /// function is expected to use the given context to <c>StartWorkflowAsync</c> and return
        /// the workflow run handle.</param>
        /// <returns>Operation handler backed by a workflow.</returns>
        public static IOperationHandler<TInput, TResult> FromHandleFactory<TInput, TResult>(
            Func<WorkflowRunOperationContext, TInput, Task<NexusWorkflowRunHandle<TResult>>> handleFactory) =>
            new WorkflowRunOperationHandler<TInput, TResult>(
                async (context, input) => await handleFactory(context, input).ConfigureAwait(false));
    }

    /// <summary>
    /// Operation handler implementation backed by workflows.
    /// </summary>
    /// <typeparam name="TInput">Operation input type.</typeparam>
    /// <typeparam name="TResult">Operation return type.</typeparam>
    internal class WorkflowRunOperationHandler<TInput, TResult> : IOperationHandler<TInput, TResult>
    {
        private readonly Func<WorkflowRunOperationContext, TInput, Task<NexusWorkflowRunHandle>> handleFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowRunOperationHandler{TInput, TResult}"/> class.
        /// </summary>
        /// <param name="handleFactory">Factory run on start to build handle.</param>
        internal WorkflowRunOperationHandler(
            Func<WorkflowRunOperationContext, TInput, Task<NexusWorkflowRunHandle>> handleFactory) =>
            this.handleFactory = handleFactory;

        /// <inheritdoc/>
        public async Task<OperationStartResult<TResult>> StartAsync(
            OperationStartContext context, TInput input)
        {
            var handle = await handleFactory(new(context), input).ConfigureAwait(false);
            return OperationStartResult.AsyncResult<TResult>(handle.ToToken());
        }

        /// <inheritdoc/>
        public Task<TResult> FetchResultAsync(OperationFetchResultContext context) =>
            throw new NotImplementedException();

        /// <inheritdoc/>
        public Task<OperationInfo> FetchInfoAsync(OperationFetchInfoContext context) =>
            throw new NotImplementedException();

        /// <inheritdoc/>
        public Task CancelAsync(OperationCancelContext context)
        {
            NexusWorkflowRunHandle handle;
            try
            {
                handle = NexusWorkflowRunHandle.FromToken(context.OperationToken);
            }
            catch (ArgumentException e)
            {
                throw new HandlerException(HandlerErrorType.BadRequest, e.Message);
            }
            if (handle.Namespace != NexusOperationExecutionContext.Current.Info.Namespace)
            {
                throw new HandlerException(HandlerErrorType.BadRequest, "Invalid namespace");
            }
            return NexusOperationExecutionContext.Current.TemporalClient.
                GetWorkflowHandle(handle.WorkflowId).CancelAsync();
        }
    }
}