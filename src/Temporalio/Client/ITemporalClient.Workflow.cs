using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    public partial interface ITemporalClient
    {
        /// <summary>
        /// Start a workflow with the given run method.
        /// </summary>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflow">Workflow run method with a result but no argument.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow handle for the started workflow.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        Task<WorkflowHandle<TResult>> StartWorkflowAsync<TResult>(
            Func<Task<TResult>> workflow, WorkflowStartOptions options);

        /// <summary>
        /// Start a workflow with the given run method.
        /// </summary>
        /// <typeparam name="T">Workflow argument type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflow">Workflow run method with a result and argument.</param>
        /// <param name="arg">Workflow argument.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow handle for the started workflow.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        Task<WorkflowHandle<TResult>> StartWorkflowAsync<T, TResult>(
            Func<T, Task<TResult>> workflow, T arg, WorkflowStartOptions options);

        /// <summary>
        /// Start a workflow with the given run method.
        /// </summary>
        /// <param name="workflow">Workflow run method with no result or argument.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow handle for the started workflow.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        Task<WorkflowHandle> StartWorkflowAsync(
            Func<Task> workflow, WorkflowStartOptions options);

        /// <summary>
        /// Start a workflow with the given run method.
        /// </summary>
        /// <typeparam name="T">Workflow argument type.</typeparam>
        /// <param name="workflow">Workflow run method with an argument but no result.</param>
        /// <param name="arg">Workflow argument.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow handle for the started workflow.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        Task<WorkflowHandle> StartWorkflowAsync<T>(
            Func<T, Task> workflow, T arg, WorkflowStartOptions options);

        /// <summary>
        /// Start a workflow with the given run method.
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
            string workflow, IReadOnlyCollection<object?> args, WorkflowStartOptions options);

        /// <summary>
        /// Start a workflow with the given run method.
        /// </summary>
        /// <typeparam name="TResult">Result type that will be set on the handle.</typeparam>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow handle for the started workflow.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        Task<WorkflowHandle<TResult>> StartWorkflowAsync<TResult>(
            string workflow, IReadOnlyCollection<object?> args, WorkflowStartOptions options);

        /// <summary>
        /// Get a workflow handle for an existing workflow with unknown return type.
        /// </summary>
        /// <param name="id">ID of the workflow.</param>
        /// <param name="runID">Run ID of the workflow or null for latest.</param>
        /// <param name="firstExecutionRunID">
        /// Optional first execution ID used for cancellation and termination.
        /// </param>
        /// <returns>Created workflow handle.</returns>
        WorkflowHandle GetWorkflowHandle(
            string id, string? runID = null, string? firstExecutionRunID = null);

        /// <summary>
        /// Get a workflow handle for an existing workflow with known return type.
        /// </summary>
        /// <typeparam name="TResult">Result type of the workflow.</typeparam>
        /// <param name="id">ID of the workflow.</param>
        /// <param name="runID">Run ID of the workflow or null for latest.</param>
        /// <param name="firstExecutionRunID">
        /// Optional first execution ID used for cancellation and termination.
        /// </param>
        /// <returns>Created workflow handle.</returns>
        WorkflowHandle<TResult> GetWorkflowHandle<TResult>(
            string id, string? runID = null, string? firstExecutionRunID = null);

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
