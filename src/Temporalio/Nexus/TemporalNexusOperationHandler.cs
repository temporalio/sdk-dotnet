#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Threading.Tasks;
using NexusRpc.Handlers;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Factory for creating generic Nexus operation handlers backed by Temporal.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public static class TemporalNexusOperationHandler
    {
        /// <summary>
        /// Create an operation handler from the given start function.
        /// </summary>
        /// <typeparam name="TInput">Operation input type.</typeparam>
        /// <typeparam name="TResult">Operation result type.</typeparam>
        /// <param name="startFunc">Function invoked on every operation start. Receives the Nexus
        /// start context, a Temporal Nexus client for starting workflows, and the operation input.
        /// Should return a <see cref="TemporalOperationResult{TResult}"/>.</param>
        /// <returns>Operation handler backed by Temporal.</returns>
        public static IOperationHandler<TInput, TResult> Create<TInput, TResult>(
            Func<OperationStartContext, ITemporalNexusClient, TInput,
                Task<TemporalOperationResult<TResult>>> startFunc) =>
            new TemporalNexusOperationHandler<TInput, TResult>(startFunc);

        /// <summary>
        /// Create an operation handler with no input from the given start function.
        /// </summary>
        /// <typeparam name="TResult">Operation result type.</typeparam>
        /// <param name="startFunc">Function invoked on every operation start. Receives the Nexus
        /// start context and a Temporal Nexus client for starting workflows. Should return a
        /// <see cref="TemporalOperationResult{TResult}"/>.</param>
        /// <returns>Operation handler backed by Temporal.</returns>
        public static IOperationHandler<NoValue, TResult> Create<TResult>(
            Func<OperationStartContext, ITemporalNexusClient,
                Task<TemporalOperationResult<TResult>>> startFunc) =>
            new TemporalNexusOperationHandler<NoValue, TResult>(
                (context, client, _) => startFunc(context, client));
    }

    /// <summary>
    /// Internal generic Nexus operation handler backed by Temporal. Implements
    /// <see cref="IOperationHandler{TInput, TResult}"/> with hard-coded cancel routing by token
    /// type.
    /// </summary>
    /// <typeparam name="TInput">Operation input type.</typeparam>
    /// <typeparam name="TResult">Operation result type.</typeparam>
    internal class TemporalNexusOperationHandler<TInput, TResult> : IOperationHandler<TInput, TResult>
    {
        private readonly Func<OperationStartContext, ITemporalNexusClient, TInput,
            Task<TemporalOperationResult<TResult>>> startFunc;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="TemporalNexusOperationHandler{TInput, TResult}"/> class.
        /// </summary>
        /// <param name="startFunc">Start function delegate.</param>
        internal TemporalNexusOperationHandler(
            Func<OperationStartContext, ITemporalNexusClient, TInput,
                Task<TemporalOperationResult<TResult>>> startFunc) =>
            this.startFunc = startFunc;

        /// <inheritdoc/>
        public async Task<OperationStartResult<TResult>> StartAsync(
            OperationStartContext context, TInput input)
        {
            var client = new TemporalNexusClient(context);
            var result = await startFunc(context, client, input).ConfigureAwait(false);
            if (result.IsSyncResult)
            {
                return OperationStartResult.SyncResult(result.SyncValue!);
            }
            return OperationStartResult.AsyncResult<TResult>(result.AsyncToken!);
        }

        /// <inheritdoc/>
        public Task CancelAsync(OperationCancelContext context)
        {
            NexusWorkflowStartHelper.OperationToken token;
            try
            {
                token = NexusWorkflowStartHelper.ParseToken(context.OperationToken);
            }
            catch (ArgumentException e)
            {
                throw new HandlerException(HandlerErrorType.BadRequest, e.Message);
            }
            if (token.Namespace != NexusOperationExecutionContext.Current.Info.Namespace)
            {
                throw new HandlerException(HandlerErrorType.BadRequest, "Invalid namespace");
            }
            return token.Type switch
            {
                1 => NexusOperationExecutionContext.Current.TemporalClient
                    .GetWorkflowHandle(token.WorkflowId).CancelAsync(),
                _ => throw new HandlerException(
                    HandlerErrorType.BadRequest,
                    $"Unsupported token type: {token.Type}"),
            };
        }
    }
}
