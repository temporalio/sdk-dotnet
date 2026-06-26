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
        public Task<TemporalOperationResult<TResult>> StartActivityAsync<TResult>(
            Expression<Func<Task<TResult>>> activityCall, StartActivityOptions options)
        {
            var (method, args) = Common.ExpressionUtil.ExtractCall(activityCall);
            return StartActivityAsync<TResult>(
                Activities.ActivityDefinition.NameFromMethodForCall(method),
                args,
                options);
        }

        /// <inheritdoc/>
        public async Task<TemporalOperationResult<NoValue>> StartActivityAsync(
            Expression<Func<Task>> activityCall, StartActivityOptions options)
        {
            var (method, args) = Common.ExpressionUtil.ExtractCall(activityCall);
            var token = await NexusActivityStartHelper.StartActivityAsync(
                TemporalClient,
                nexusStartContext,
                temporalContext,
                Activities.ActivityDefinition.NameFromMethodForCall(method),
                args,
                options).ConfigureAwait(false);
            return TemporalOperationResult<NoValue>.AsyncResult(token);
        }

        /// <inheritdoc/>
        public async Task<TemporalOperationResult<TResult>> StartActivityAsync<TResult>(
            string activity, IReadOnlyCollection<object?> args, StartActivityOptions options)
        {
            var token = await NexusActivityStartHelper.StartActivityAsync(
                TemporalClient,
                nexusStartContext,
                temporalContext,
                activity,
                args,
                options).ConfigureAwait(false);
            return TemporalOperationResult<TResult>.AsyncResult(token);
        }
    }
}
