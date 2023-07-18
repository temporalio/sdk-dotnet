using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    public partial interface ITemporalClient
    {
        /// <summary>
        /// Start a workflow via lambda invoking the run method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method with a result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow handle for the started workflow.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, WorkflowOptions options);

        /// <summary>
        /// Start a workflow via lambda invoking the run method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method with no result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow handle for the started workflow.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        Task<WorkflowHandle<TWorkflow>> StartWorkflowAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> workflowRunCall, WorkflowOptions options);

        /// <summary>
        /// Start a workflow by name.
        /// </summary>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow handle for the started workflow.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        Task<WorkflowHandle> StartWorkflowAsync(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options);

        /// <summary>
        /// Get a workflow handle for an existing workflow with unknown return type.
        /// </summary>
        /// <param name="id">ID of the workflow.</param>
        /// <param name="runId">Run ID of the workflow or null for latest.</param>
        /// <param name="firstExecutionRunId">
        /// Optional first execution ID used for cancellation and termination.
        /// </param>
        /// <returns>Created workflow handle.</returns>
        WorkflowHandle GetWorkflowHandle(
            string id, string? runId = null, string? firstExecutionRunId = null);

        /// <summary>
        /// Get a workflow handle for an existing workflow with known type.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="id">ID of the workflow.</param>
        /// <param name="runId">Run ID of the workflow or null for latest.</param>
        /// <param name="firstExecutionRunId">
        /// Optional first execution ID used for cancellation and termination.
        /// </param>
        /// <returns>Created workflow handle.</returns>
        WorkflowHandle<TWorkflow> GetWorkflowHandle<TWorkflow>(
            string id, string? runId = null, string? firstExecutionRunId = null);

        /// <summary>
        /// Get a workflow handle for an existing workflow with known type and return type.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TResult">Result type of the workflow.</typeparam>
        /// <param name="id">ID of the workflow.</param>
        /// <param name="runId">Run ID of the workflow or null for latest.</param>
        /// <param name="firstExecutionRunId">
        /// Optional first execution ID used for cancellation and termination.
        /// </param>
        /// <returns>Created workflow handle.</returns>
        WorkflowHandle<TWorkflow, TResult> GetWorkflowHandle<TWorkflow, TResult>(
            string id, string? runId = null, string? firstExecutionRunId = null);

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// List workflows with the given query.
        /// </summary>
        /// <param name="query">Query to use for filtering.</param>
        /// <param name="options">Options for the list call.</param>
        /// <returns>Async enumerator for the workflows.</returns>
        public IAsyncEnumerable<WorkflowExecution> ListWorkflowsAsync(
            string query, WorkflowListOptions? options = null);
#endif
    }
}
