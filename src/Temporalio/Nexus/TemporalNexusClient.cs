using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NexusRpc.Handlers;
using Temporalio.Client;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Nexus-aware client wrapping the Temporal client. Provides methods for starting workflows
    /// from within Nexus operation handlers, handling all Nexus plumbing (links, callbacks, token
    /// generation) internally.
    /// </summary>
    /// <remarks>
    /// WARNING: Nexus support is experimental.
    /// This client is created by <see cref="TemporalOperationHandler"/> and passed to the
    /// user's start function. It should not be instantiated directly.
    /// </remarks>
    public class TemporalNexusClient : ITemporalNexusClient
    {
        private readonly OperationStartContext nexusStartContext;
        private readonly NexusOperationExecutionContext temporalContext;
        private bool asyncStarted;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalNexusClient"/> class.
        /// </summary>
        /// <param name="nexusStartContext">Nexus start context for callbacks and links.</param>
        /// <param name="temporalContext">Temporal operation context. </param>
        internal TemporalNexusClient(
            OperationStartContext nexusStartContext,
            NexusOperationExecutionContext temporalContext)
        {
            this.nexusStartContext = nexusStartContext;
            this.temporalContext = temporalContext;
        }

        /// <inheritdoc/>
        public ITemporalClient TemporalClient => temporalContext.TemporalClient;

        /// <inheritdoc/>
        public Task<TemporalOperationResult<TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return StartWorkflowAsync<TResult>(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        /// <inheritdoc/>
        public async Task<TemporalOperationResult<NoValue>> StartWorkflowAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            var handle = await NexusWorkflowStartHelper.StartWorkflowAsync(
                nexusStartContext,
                temporalContext,
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options).ConfigureAwait(false);
            return TemporalOperationResult<NoValue>.AsyncResult(handle.ToToken());
        }

        /// <inheritdoc/>
        public async Task<TemporalOperationResult<TResult>> StartWorkflowAsync<TResult>(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options)
        {
            var handle = await NexusWorkflowStartHelper.StartWorkflowAsync(
                nexusStartContext,
                temporalContext,
                workflow,
                args,
                options).ConfigureAwait(false);
            return TemporalOperationResult<TResult>.AsyncResult(handle.ToToken());
        }

        /// <inheritdoc/>
        public Task<TemporalOperationResult<TResult>> StartUpdateWorkflowAsync<TWorkflow, TResult>(
            string workflowId,
            Expression<Func<TWorkflow, Task<TResult>>> updateCall,
            WorkflowUpdateStartOptions options)
        {
            var (method, args) = Common.ExpressionUtil.ExtractCall(updateCall);
            return StartUpdateWorkflowAsync<TResult>(
                workflowId,
                Workflows.WorkflowUpdateDefinition.NameFromMethodForCall(method),
                args,
                options);
        }

        /// <inheritdoc/>
        public Task<TemporalOperationResult<NoValue>> StartUpdateWorkflowAsync<TWorkflow>(
            string workflowId,
            Expression<Func<TWorkflow, Task>> updateCall,
            WorkflowUpdateStartOptions options)
        {
            var (method, args) = Common.ExpressionUtil.ExtractCall(updateCall);
            return StartUpdateWorkflowAsync<NoValue>(
                workflowId,
                Workflows.WorkflowUpdateDefinition.NameFromMethodForCall(method),
                args,
                options);
        }

        /// <inheritdoc/>
        public async Task<TemporalOperationResult<TResult>> StartUpdateWorkflowAsync<TResult>(
            string workflowId,
            string update,
            IReadOnlyCollection<object?> args,
            WorkflowUpdateStartOptions options)
        {
            // Reserve the single async-operation slot for this operation invocation. Only a genuine
            // async result consumes it; a sync result or a failure releases it. A Nexus operation
            // Start handler runs single-threaded per invocation, so no synchronization is needed.
            if (asyncStarted)
            {
                throw new HandlerException(
                    HandlerErrorType.BadRequest,
                    "only one async operation can be started per operation invocation");
            }
            asyncStarted = true;
            var keepSlot = false;
            try
            {
                var result = await NexusWorkflowStartHelper.StartUpdateWorkflowAsync<TResult>(
                    nexusStartContext,
                    temporalContext,
                    workflowId,
                    update,
                    args,
                    options).ConfigureAwait(false);
                keepSlot = !result.IsSyncResult;
                return result;
            }
            finally
            {
                if (!keepSlot)
                {
                    asyncStarted = false;
                }
            }
        }
    }
}
