#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Threading.Tasks;
using NexusRpc.Handlers;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Factory for creating generic Nexus operation handlers backed by Temporal.
    /// </summary>
    /// <remarks>
    /// <para>WARNING: Nexus support is experimental.</para>
    /// <para>Usage example — starting a workflow from a Nexus operation:</para>
    /// <code>
    /// [OperationImpl]
    /// public IOperationHandler&lt;TransferInput, TransferResult&gt; StartTransfer() =>
    ///     TemporalNexusOperationHandler.FromHandleFactory&lt;TransferInput, TransferResult&gt;(
    ///         async (context, client, input) =>
    ///             await client.StartWorkflowAsync&lt;TransferWorkflow, TransferResult&gt;(
    ///                 wf => wf.RunAsync(input),
    ///                 new(id: $"transfer-{input.TransferId}", taskQueue: "my-task-queue")));
    /// </code>
    /// <para>To perform a synchronous operation (e.g., sending a signal and returning immediately):</para>
    /// <code>
    /// [OperationImpl]
    /// public IOperationHandler&lt;CancelOrderInput, NoValue&gt; CancelOrder() =>
    ///     TemporalNexusOperationHandler.FromHandleFactory&lt;CancelOrderInput, NoValue&gt;(
    ///         async (context, client, input) =>
    ///         {
    ///             await client.TemporalClient
    ///                 .GetWorkflowHandle($"order-{input.OrderId}")
    ///                 .SignalAsync("requestCancellation", new[] { input });
    ///             return TemporalOperationResult&lt;NoValue&gt;.Sync(default);
    ///         });
    /// </code>
    /// </remarks>
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
        public static TemporalNexusOperationHandler<TInput, TResult> FromHandleFactory<TInput, TResult>(
            Func<OperationStartContext, ITemporalNexusClient, TInput,
                Task<TemporalOperationResult<TResult>>> startFunc) =>
            new(startFunc);

        /// <summary>
        /// Create an operation handler with no input from the given start function.
        /// </summary>
        /// <typeparam name="TResult">Operation result type.</typeparam>
        /// <param name="startFunc">Function invoked on every operation start. Receives the Nexus
        /// start context and a Temporal Nexus client for starting workflows. Should return a
        /// <see cref="TemporalOperationResult{TResult}"/>.</param>
        /// <returns>Operation handler backed by Temporal.</returns>
        public static TemporalNexusOperationHandler<NoValue, TResult> FromHandleFactory<TResult>(
            Func<OperationStartContext, ITemporalNexusClient,
                Task<TemporalOperationResult<TResult>>> startFunc) =>
            new((context, client, _) => startFunc(context, client));
    }

    /// <summary>
    /// Generic Nexus operation handler backed by Temporal. Implements
    /// <see cref="IOperationHandler{TInput, TResult}"/> and provides a composable way to map
    /// Temporal operations to Nexus operations.
    /// </summary>
    /// <typeparam name="TInput">Operation input type.</typeparam>
    /// <typeparam name="TResult">Operation result type.</typeparam>
    /// <remarks>
    /// <para>WARNING: Nexus support is experimental.</para>
    /// <para>This class supports inheritance to customize cancel behavior. Override
    /// <see cref="CancelWorkflowRunAsync"/> to change how workflow-run cancellations are handled.
    /// The <see cref="StartAsync"/> and <see cref="CancelAsync"/> methods should not be
    /// overridden — they contain the core dispatch logic.</para>
    /// </remarks>
    public class TemporalNexusOperationHandler<TInput, TResult> : IOperationHandler<TInput, TResult>
    {
        private readonly Func<OperationStartContext, ITemporalNexusClient, TInput,
            Task<TemporalOperationResult<TResult>>> startFunc;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="TemporalNexusOperationHandler{TInput, TResult}"/> class.
        /// </summary>
        /// <param name="startFunc">Start function delegate.</param>
        public TemporalNexusOperationHandler(
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
            NexusWorkflowRunHandle.OperationToken token;
            try
            {
                token = NexusWorkflowRunHandle.ParseToken(context.OperationToken);
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
                1 => CancelWorkflowRunAsync(context, token.WorkflowId),
                _ => throw new HandlerException(
                    HandlerErrorType.BadRequest,
                    $"Unsupported token type: {token.Type}"),
            };
        }

        /// <summary>
        /// Called when a cancel request is received for a workflow-run token (type=1). Override to
        /// customize cancel behavior.
        /// <para>Default behavior: cancels the underlying workflow.</para>
        /// </summary>
        /// <param name="context">The cancel context.</param>
        /// <param name="workflowId">The workflow ID extracted from the operation token.</param>
        /// <returns>Task for cancel completion.</returns>
        protected virtual Task CancelWorkflowRunAsync(
            OperationCancelContext context, string workflowId) =>
            NexusOperationExecutionContext.Current.TemporalClient
                .GetWorkflowHandle(workflowId).CancelAsync();
    }
}
