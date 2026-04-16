using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NexusRpc.Handlers;
using Temporalio.Client;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Context used to create workflow run handles. This is passed to functions passed to
    /// <c>FromHandleFactory</c> on <see cref="WorkflowRunOperationHandler"/>.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class WorkflowRunOperationContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowRunOperationContext"/> class.
        /// </summary>
        /// <param name="handlerContext">Nexus context.</param>
        internal WorkflowRunOperationContext(OperationStartContext handlerContext) =>
            HandlerContext = handlerContext;

        /// <summary>
        /// Gets the Nexus handler context for the start call.
        /// </summary>
        public OperationStartContext HandlerContext { get; private init; }

        /// <summary>
        /// Start a workflow via a lambda invoking the run method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method with no result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Nexus workflow run handle to return in handle factory.</returns>
        public Task<NexusWorkflowRunHandle> StartWorkflowAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return StartWorkflowAsync(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        /// <summary>
        /// Start a workflow via a lambda invoking the run method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method with a result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Nexus workflow run handle to return in handle factory.</returns>
        public Task<NexusWorkflowRunHandle<TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return StartWorkflowAsync<TResult>(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        /// <summary>
        /// Start a workflow by name with no result type.
        /// </summary>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Nexus workflow run handle to return in handle factory.</returns>
#pragma warning disable CA1822 // We don't want this static
        public async Task<NexusWorkflowRunHandle> StartWorkflowAsync(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options)
        {
#pragma warning restore CA1822
            var temporalContext = NexusOperationExecutionContext.Current;
            var ns = temporalContext.TemporalClient.Options.Namespace;
            var wfId = options.Id ?? string.Empty;
            await NexusWorkflowStartHelper.StartWorkflowAndGetTokenAsync(
                temporalContext.TemporalClient,
                (OperationStartContext)temporalContext.HandlerContext,
                temporalContext,
                workflow,
                args,
                options).ConfigureAwait(false);
            return new NexusWorkflowRunHandle(ns, wfId, version: 0);
        }

        /// <summary>
        /// Start a workflow by name with a result type.
        /// </summary>
        /// <typeparam name="TResult">Result type.</typeparam>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Nexus workflow run handle to return in handle factory.</returns>
#pragma warning disable CA1822 // We don't want this static
        public async Task<NexusWorkflowRunHandle<TResult>> StartWorkflowAsync<TResult>(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options)
        {
#pragma warning restore CA1822
            var temporalContext = NexusOperationExecutionContext.Current;
            var ns = temporalContext.TemporalClient.Options.Namespace;
            var wfId = options.Id ?? string.Empty;
            await NexusWorkflowStartHelper.StartWorkflowAndGetTokenAsync(
                temporalContext.TemporalClient,
                (OperationStartContext)temporalContext.HandlerContext,
                temporalContext,
                workflow,
                args,
                options).ConfigureAwait(false);
            return new NexusWorkflowRunHandle<TResult>(ns, wfId, version: 0);
        }
    }
}