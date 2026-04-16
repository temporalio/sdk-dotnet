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
    /// This client is created by <see cref="TemporalNexusOperationHandler"/> and passed to the
    /// user's start function. It should not be instantiated directly.
    /// </remarks>
    public class TemporalNexusClient : ITemporalNexusClient
    {
        private readonly ITemporalClient temporalClient;
        private readonly OperationStartContext nexusStartContext;
        private readonly NexusOperationExecutionContext temporalContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalNexusClient"/> class.
        /// </summary>
        /// <param name="nexusStartContext">Nexus start context for callbacks and links.</param>
        internal TemporalNexusClient(OperationStartContext nexusStartContext)
        {
            this.nexusStartContext = nexusStartContext;
            temporalContext = NexusOperationExecutionContext.Current;
            temporalClient = temporalContext.TemporalClient;
        }

        /// <summary>
        /// Start a workflow via a lambda invoking the run method. Always returns an async result
        /// with a workflow-run operation token.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method with a result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>An async operation result containing the workflow-run token.</returns>
        public Task<TemporalOperationResult<TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return StartWorkflowAsync<TResult>(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        /// <summary>
        /// Start a workflow via a lambda invoking the run method with no return value. Always
        /// returns an async result with a workflow-run operation token.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method with no result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>An async operation result containing the workflow-run token.</returns>
        public async Task<TemporalOperationResult<NoValue>> StartWorkflowAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            var token = await NexusWorkflowStartHelper.StartWorkflowAndGetTokenAsync(
                temporalClient,
                nexusStartContext,
                temporalContext,
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options).ConfigureAwait(false);
            return TemporalOperationResult<NoValue>.Async(token);
        }

        /// <summary>
        /// Start a workflow by name. Always returns an async result with a workflow-run operation
        /// token.
        /// </summary>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>An async operation result containing the workflow-run token.</returns>
        public async Task<TemporalOperationResult<TResult>> StartWorkflowAsync<TResult>(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options)
        {
            var token = await NexusWorkflowStartHelper.StartWorkflowAndGetTokenAsync(
                temporalClient,
                nexusStartContext,
                temporalContext,
                workflow,
                args,
                options).ConfigureAwait(false);
            return TemporalOperationResult<TResult>.Async(token);
        }
    }
}
