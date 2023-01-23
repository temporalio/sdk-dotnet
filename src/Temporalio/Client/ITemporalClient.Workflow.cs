using System;
using System.Threading.Tasks;

#if NETCOREAPP3_0_OR_GREATER
using System.Collections.Generic;
#endif

namespace Temporalio.Client
{
    public partial interface ITemporalClient
    {
        Task<WorkflowHandle<TResult>> StartWorkflowAsync<TResult>(
            Func<Task<TResult>> workflow, StartWorkflowOptions options);

        Task<WorkflowHandle<TResult>> StartWorkflowAsync<T, TResult>(
            Func<T, Task<TResult>> workflow, T arg, StartWorkflowOptions options);

        Task<WorkflowHandle> StartWorkflowAsync(
            Func<Task> workflow, StartWorkflowOptions options);

        Task<WorkflowHandle> StartWorkflowAsync<T>(
            Func<T, Task> workflow, T arg, StartWorkflowOptions options);

        Task<WorkflowHandle> StartWorkflowAsync(
            string workflow, object?[] args, StartWorkflowOptions options);

        Task<WorkflowHandle<TResult>> StartWorkflowAsync<TResult>(
            string workflow, object?[] args, StartWorkflowOptions options);

        WorkflowHandle GetWorkflowHandle(
            string workflowID, string? runID = null, string? firstExecutionRunID = null);

        WorkflowHandle<TResult> GetWorkflowHandle<TResult>(
            string workflowID, string? runID = null, string? firstExecutionRunID = null);

        #if NETCOREAPP3_0_OR_GREATER

        public IAsyncEnumerator<WorkflowExecution> ListWorkflows(
            string query, ListWorkflowsOptions? options = null);

        #endif
    }
}
