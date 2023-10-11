using System.Threading.Tasks;

#if NETCOREAPP3_0_OR_GREATER
using System.Collections.Generic;
#endif

namespace Temporalio.Client.Interceptors
{
    public partial class ClientOutboundInterceptor
    {
        /// <summary>
        /// Intercept start workflow calls.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type. May be ValueTuple if unknown.</typeparam>
        /// <typeparam name="TResult">Result type of the workflow. May be ValueTuple if unknown.</typeparam>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Handle for the workflow.</returns>
        public virtual Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            StartWorkflowInput input) =>
            Next.StartWorkflowAsync<TWorkflow, TResult>(input);

        /// <summary>
        /// Intercept signal workflow calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for acceptance of the signal.</returns>
        public virtual Task SignalWorkflowAsync(SignalWorkflowInput input) =>
            Next.SignalWorkflowAsync(input);

        /// <summary>
        /// Intercept query workflow calls.
        /// </summary>
        /// <typeparam name="TResult">Return type of the query.</typeparam>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Result of the query.</returns>
        public virtual Task<TResult> QueryWorkflowAsync<TResult>(QueryWorkflowInput input) =>
            Next.QueryWorkflowAsync<TResult>(input);

        /// <summary>
        /// Intercept start workflow update calls.
        /// </summary>
        /// <typeparam name="TResult">Return type of the update. This may be ValueTuple if user
        /// is discarding result.</typeparam>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Handle for the update.</returns>
        public virtual Task<WorkflowUpdateHandle<TResult>> StartWorkflowUpdateAsync<TResult>(
            StartWorkflowUpdateInput input) =>
            Next.StartWorkflowUpdateAsync<TResult>(input);

        /// <summary>
        /// Intercept poll workflow update calls.
        /// </summary>
        /// <typeparam name="TResult">Return type of the update. This may be ValueTuple if user
        /// is discarding result.</typeparam>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Result for the update.</returns>
        public virtual Task<TResult> PollWorkflowUpdateAsync<TResult>(
            PollWorkflowUpdateInput input) =>
            Next.PollWorkflowUpdateAsync<TResult>(input);

        /// <summary>
        /// Intercept describe workflow calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Workflow execution description.</returns>
        public virtual Task<WorkflowExecutionDescription> DescribeWorkflowAsync(
            DescribeWorkflowInput input) =>
            Next.DescribeWorkflowAsync(input);

        /// <summary>
        /// Intercept cancel workflow calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for acceptance of the cancel.</returns>
        public virtual Task CancelWorkflowAsync(CancelWorkflowInput input) =>
            Next.CancelWorkflowAsync(input);

        /// <summary>
        /// Intercept terminate workflow calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for termination completion.</returns>
        public virtual Task TerminateWorkflowAsync(TerminateWorkflowInput input) =>
            Next.TerminateWorkflowAsync(input);

        /// <summary>
        /// Intercept a history event page fetch.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>
        /// Event page. This will not return an empty event set and a next page token.
        /// </returns>
        public virtual Task<WorkflowHistoryEventPage> FetchWorkflowHistoryEventPageAsync(
            FetchWorkflowHistoryEventPageInput input) =>
            Next.FetchWorkflowHistoryEventPageAsync(input);

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Intercept listing workflows.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Async enumerator for the workflows.</returns>
        public virtual IAsyncEnumerable<WorkflowExecution> ListWorkflowsAsync(
            ListWorkflowsInput input) =>
            Next.ListWorkflowsAsync(input);
#endif
    }
}