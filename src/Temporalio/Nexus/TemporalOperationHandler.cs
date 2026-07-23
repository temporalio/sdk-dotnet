#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NexusRpc;
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
    ///     TemporalOperationHandler.FromHandleFactory&lt;TransferInput, TransferResult&gt;(
    ///         async (context, client, input) =>
    ///             await client.StartWorkflowAsync&lt;TransferWorkflow, TransferResult&gt;(
    ///                 wf => wf.RunAsync(input),
    ///                 new(id: $"transfer-{input.TransferId}", taskQueue: "my-task-queue")));
    /// </code>
    /// <para>To perform a synchronous operation (e.g., sending a signal and returning immediately):</para>
    /// <code>
    /// [OperationImpl]
    /// public IOperationHandler&lt;CancelOrderInput, NoValue&gt; CancelOrder() =>
    ///     TemporalOperationHandler.FromHandleFactory&lt;CancelOrderInput, NoValue&gt;(
    ///         async (context, client, input) =>
    ///         {
    ///             await client.TemporalClient
    ///                 .GetWorkflowHandle($"order-{input.OrderId}")
    ///                 .SignalAsync("requestCancellation", new[] { input });
    ///             return TemporalOperationResult&lt;NoValue&gt;.SyncResult(default);
    ///         });
    /// </code>
    /// </remarks>
    public static class TemporalOperationHandler
    {
        /// <summary>
        /// Create an operation handler from the given start function.
        /// </summary>
        /// <typeparam name="TInput">Operation input type.</typeparam>
        /// <typeparam name="TResult">Operation result type.</typeparam>
        /// <param name="startFunc">Function invoked on every operation start. Receives the Nexus
        /// start context, a Temporal operation client for starting workflows, and the operation input.
        /// Should return a <see cref="TemporalOperationResult{TResult}"/>.</param>
        /// <returns>Operation handler backed by Temporal.</returns>
        public static TemporalOperationHandler<TInput, TResult> FromHandleFactory<TInput, TResult>(
            Func<TemporalOperationStartContext, ITemporalNexusClient, TInput,
                Task<TemporalOperationResult<TResult>>> startFunc) =>
            new(startFunc);

        /// <summary>
        /// Create an operation handler with no input from the given start function.
        /// </summary>
        /// <typeparam name="TResult">Operation result type.</typeparam>
        /// <param name="startFunc">Function invoked on every operation start. Receives the Nexus
        /// start context and a Temporal operation client for starting workflows. Should return a
        /// <see cref="TemporalOperationResult{TResult}"/>.</param>
        /// <returns>Operation handler backed by Temporal.</returns>
        public static TemporalOperationHandler<NoValue, TResult> FromHandleFactory<TResult>(
            Func<TemporalOperationStartContext, ITemporalNexusClient,
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
    /// <see cref="CancelWorkflowRunAsync"/> to change how workflow-run cancellations are handled.</para>
    /// </remarks>
    public class TemporalOperationHandler<TInput, TResult> : IOperationHandler<TInput, TResult>
    {
        private readonly Func<TemporalOperationStartContext, ITemporalNexusClient, TInput,
            Task<TemporalOperationResult<TResult>>> startFunc;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="TemporalOperationHandler{TInput, TResult}"/> class.
        /// </summary>
        /// <param name="startFunc">Start function delegate.</param>
        public TemporalOperationHandler(
            Func<TemporalOperationStartContext, ITemporalNexusClient, TInput,
                Task<TemporalOperationResult<TResult>>> startFunc) =>
            this.startFunc = startFunc;

        /// <inheritdoc/>
        public async Task<OperationStartResult<TResult>> StartAsync(
            OperationStartContext context, TInput input)
        {
            var client = new TemporalNexusClient(context, NexusOperationExecutionContext.Current);
            var result = await startFunc(
                new TemporalOperationStartContext(context), client, input).ConfigureAwait(false);
            if (result.IsSyncResult)
            {
                return OperationStartResult.SyncResult(result.SyncValue!);
            }
            return OperationStartResult.AsyncResult<TResult>(result.AsyncToken);
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
                NexusWorkflowRunHandle.WorkflowRunOperationTokenType =>
                    CancelWorkflowRunAsync(
                        new TemporalOperationCancelContext(context),
                        new CancelWorkflowRunInput(token.WorkflowId)),
                NexusWorkflowRunHandle.UpdateWorkflowOperationTokenType =>
                    CancelWorkflowUpdateFromTokenAsync(),
                _ => throw new HandlerException(
                    HandlerErrorType.BadRequest,
                    $"Unsupported token type: {token.Type}"),
            };

            Task CancelWorkflowUpdateFromTokenAsync()
            {
                // Decode via the update-workflow handle so a malformed token (e.g. missing workflow
                // ID or update ID) is rejected as a bad request rather than passing empty strings
                // through to a user-supplied cancel override.
                NexusWorkflowUpdateHandle updateHandle;
                try
                {
                    updateHandle = NexusWorkflowUpdateHandle.FromToken(context.OperationToken);
                }
                catch (ArgumentException e)
                {
                    throw new HandlerException(HandlerErrorType.BadRequest, e.Message);
                }
                return CancelWorkflowUpdateAsync(
                    new TemporalOperationCancelContext(context),
                    new CancelWorkflowUpdateInput(
                        updateHandle.WorkflowId, updateHandle.RunId, updateHandle.UpdateId));
            }
        }

        /// <summary>
        /// Called when a cancel request is received for a workflow-run token. Override to
        /// customize cancel behavior.
        /// <para>Default behavior: cancels the underlying workflow.</para>
        /// </summary>
        /// <param name="context">The cancel context.</param>
        /// <param name="input">Workflow-run cancel input.</param>
        /// <returns>Task for cancel completion.</returns>
        protected virtual Task CancelWorkflowRunAsync(
            TemporalOperationCancelContext context, CancelWorkflowRunInput input) =>
            NexusOperationExecutionContext.Current.TemporalClient
                .GetWorkflowHandle(input.WorkflowId).CancelAsync();

        /// <summary>
        /// Called when a cancel request is received for an update-workflow token. Override to
        /// customize cancel behavior.
        /// <para>Default behavior: throws a not-implemented handler error, as there is no default
        /// way to cancel an update-workflow operation.</para>
        /// </summary>
        /// <param name="context">The cancel context.</param>
        /// <param name="input">Update-workflow cancel input.</param>
        /// <returns>Task for cancel completion.</returns>
        protected virtual Task CancelWorkflowUpdateAsync(
            TemporalOperationCancelContext context, CancelWorkflowUpdateInput input) =>
            throw new HandlerException(
                HandlerErrorType.NotImplemented, "cannot cancel an UpdateWorkflow operation");
    }

    /// <summary>
    /// Input passed to
    /// <see cref="TemporalOperationHandler{TInput, TResult}.CancelWorkflowRunAsync"/>.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class CancelWorkflowRunInput
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CancelWorkflowRunInput"/> class.
        /// </summary>
        /// <param name="workflowId">Workflow ID extracted from the operation token.</param>
        public CancelWorkflowRunInput(string workflowId) => WorkflowId = workflowId;

        /// <summary>
        /// Gets the workflow ID extracted from the operation token.
        /// </summary>
        public string WorkflowId { get; }
    }

    /// <summary>
    /// Input passed to
    /// <see cref="TemporalOperationHandler{TInput, TResult}.CancelWorkflowUpdateAsync"/>.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class CancelWorkflowUpdateInput
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CancelWorkflowUpdateInput"/> class.
        /// </summary>
        /// <param name="workflowId">Workflow ID extracted from the operation token.</param>
        /// <param name="runId">Workflow run ID extracted from the operation token. May be empty.</param>
        /// <param name="updateId">Update ID extracted from the operation token.</param>
        public CancelWorkflowUpdateInput(string workflowId, string runId, string updateId)
        {
            WorkflowId = workflowId;
            RunId = runId;
            UpdateId = updateId;
        }

        /// <summary>
        /// Gets the workflow ID extracted from the operation token.
        /// </summary>
        public string WorkflowId { get; }

        /// <summary>
        /// Gets the workflow run ID extracted from the operation token.
        /// </summary>
        public string RunId { get; }

        /// <summary>
        /// Gets the update ID extracted from the operation token.
        /// </summary>
        public string UpdateId { get; }
    }

    /// <summary>
    /// Context passed to the start function of a
    /// <see cref="TemporalOperationHandler{TInput, TResult}"/>.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class TemporalOperationStartContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalOperationStartContext"/> class.
        /// </summary>
        /// <param name="underlying">Underlying Nexus start context.</param>
        internal TemporalOperationStartContext(OperationStartContext underlying) =>
            Underlying = underlying;

        /// <summary>
        /// Gets the Nexus service name.
        /// </summary>
        public string Service => Underlying.Service;

        /// <summary>
        /// Gets the Nexus operation name.
        /// </summary>
        public string Operation => Underlying.Operation;

        /// <summary>
        /// Gets the unique identifier for this start call, used for deduplication.
        /// </summary>
        public string RequestId => Underlying.RequestId;

        /// <summary>
        /// Gets the cancellation token for this start call (not operation cancellation).
        /// </summary>
        public CancellationToken CancellationToken => Underlying.CancellationToken;

        /// <summary>
        /// Gets the request headers (case-insensitive keys).
        /// </summary>
        public IReadOnlyDictionary<string, string>? Headers => Underlying.Headers;

        /// <summary>
        /// Gets the deadline for the start call to complete, if any.
        /// </summary>
        public DateTime? RequestDeadline => Underlying.RequestDeadline;

        /// <summary>
        /// Gets the inbound links carrying caller information.
        /// </summary>
        public IReadOnlyCollection<NexusLink> InboundLinks => Underlying.InboundLinks;

        /// <summary>
        /// Gets the mutable outbound links collection. Handlers and middleware may add to this.
        /// </summary>
        public IList<NexusLink> OutboundLinks => Underlying.OutboundLinks;

        /// <summary>
        /// Gets the underlying Nexus start context. SDK-internal for callback wiring and tests.
        /// </summary>
        internal OperationStartContext Underlying { get; }
    }

    /// <summary>
    /// Context passed to
    /// <see cref="TemporalOperationHandler{TInput, TResult}.CancelWorkflowRunAsync"/>.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class TemporalOperationCancelContext
    {
        private readonly OperationCancelContext underlying;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalOperationCancelContext"/> class.
        /// </summary>
        /// <param name="underlying">Underlying Nexus cancel context.</param>
        internal TemporalOperationCancelContext(OperationCancelContext underlying) =>
            this.underlying = underlying;

        /// <summary>
        /// Gets the Nexus service name.
        /// </summary>
        public string Service => underlying.Service;

        /// <summary>
        /// Gets the Nexus operation name.
        /// </summary>
        public string Operation => underlying.Operation;

        /// <summary>
        /// Gets the cancellation token for this cancel call (not operation cancellation).
        /// </summary>
        public CancellationToken CancellationToken => underlying.CancellationToken;

        /// <summary>
        /// Gets the request headers (case-insensitive keys).
        /// </summary>
        public IReadOnlyDictionary<string, string>? Headers => underlying.Headers;

        /// <summary>
        /// Gets the deadline for the cancel call to complete, if any.
        /// </summary>
        public DateTime? RequestDeadline => underlying.RequestDeadline;
    }
}
