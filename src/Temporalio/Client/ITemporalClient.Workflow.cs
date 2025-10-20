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

        /// <summary>
        /// Start an update via a call to a WorkflowUpdate attributed method, possibly starting the
        /// workflow at the same time. Note that in some cases this call may fail but the workflow
        /// will still be started.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Update options. Currently <c>WaitForStage</c> is required.</param>
        /// <returns>Workflow update handle.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse and conflict policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        Task<WorkflowUpdateHandle> StartUpdateWithStartWorkflowAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> updateCall,
            WorkflowStartUpdateWithStartOptions options);

        /// <summary>
        /// Start an update via a call to a WorkflowUpdate attributed method, possibly starting the
        /// workflow at the same time. Note that in some cases this call may fail but the workflow
        /// will still be started.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Update options. Currently <c>WaitForStage</c> is required.</param>
        /// <returns>Workflow update handle.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse and conflict policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        Task<WorkflowUpdateHandle<TUpdateResult>> StartUpdateWithStartWorkflowAsync<TWorkflow, TUpdateResult>(
            Expression<Func<TWorkflow, Task<TUpdateResult>>> updateCall,
            WorkflowStartUpdateWithStartOptions options);

        /// <summary>
        /// Start an update using its name, possibly starting the workflow at the same time. Note
        /// that in some cases this call may fail but the workflow will still be started.
        /// </summary>
        /// <param name="update">Name of the update.</param>
        /// <param name="args">Arguments for the update.</param>
        /// <param name="options">Update options. Currently <c>WaitForStage</c> is required.</param>
        /// <returns>Workflow update handle.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse and conflict policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        Task<WorkflowUpdateHandle> StartUpdateWithStartWorkflowAsync(
            string update, IReadOnlyCollection<object?> args, WorkflowStartUpdateWithStartOptions options);

        /// <summary>
        /// Start an update using its name, possibly starting the workflow at the same time. Note
        /// that in some cases this call may fail but the workflow will still be started.
        /// </summary>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="update">Name of the update.</param>
        /// <param name="args">Arguments for the update.</param>
        /// <param name="options">Update options. Currently <c>WaitForStage</c> is required.</param>
        /// <returns>Workflow update handle.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse and conflict policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        Task<WorkflowUpdateHandle<TUpdateResult>> StartUpdateWithStartWorkflowAsync<TUpdateResult>(
            string update, IReadOnlyCollection<object?> args, WorkflowStartUpdateWithStartOptions options);

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// List workflows with the given query.
        /// </summary>
        /// <param name="query">Query to use for filtering.</param>
        /// <param name="options">Options for the list call.</param>
        /// <returns>Async enumerator for the workflows.</returns>
        /// <seealso href="https://docs.temporal.io/visibility">Visibility docs.</seealso>
        public IAsyncEnumerable<WorkflowExecution> ListWorkflowsAsync(
            string query, WorkflowListOptions? options = null);
#endif

        /// <summary>
        /// Count workflows with the given query.
        /// </summary>
        /// <param name="query">Query to use for counting.</param>
        /// <param name="options">Options for the count call.</param>
        /// <returns>Count information for the workflows.</returns>
        /// <seealso href="https://docs.temporal.io/visibility">Visibility docs.</seealso>
        public Task<WorkflowExecutionCount> CountWorkflowsAsync(
            string query, WorkflowCountOptions? options = null);

        /// <summary>
        /// List workflows with the given query using manual paging.
        /// </summary>
        /// <param name="query">
        /// Query to use for filtering. Subsequent pages must have the same query as the initial call.
        /// </param>
        /// <param name="nextPageToken">
        /// Set to null for the initial call to retrieve the first page.
        /// Set to <see cref="ListWorkflowsPage.NextPageToken"/> returned by a previous call to retrieve the next page.
        /// </param>
        /// <param name="options">Options for the list call.</param>
        /// <returns>
        /// A single page of a list of workflows.
        /// Repeat the call using <see cref="ListWorkflowsPage.NextPageToken"/> to get more pages.
        /// </returns>
        /// <seealso href="https://docs.temporal.io/visibility">Visibility docs.</seealso>
        Task<ListWorkflowsPage> GetListWorkflowsPageAsync(
            string query, byte[]? nextPageToken, GetListWorkflowsPageOptions? options = null);
    }
}
