using System;
using System.Threading.Tasks;

#if NETCOREAPP3_0_OR_GREATER
using System.Collections.Generic;
#endif

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Base class for all outbound interceptors.
    /// </summary>
    public abstract class ClientOutboundInterceptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClientOutboundInterceptor"/> class.
        /// </summary>
        /// <param name="next">Next outbound interceptor in the chain.</param>
        protected ClientOutboundInterceptor(ClientOutboundInterceptor next)
        {
            MaybeNext = next;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientOutboundInterceptor"/> class.
        /// </summary>
        private protected ClientOutboundInterceptor()
        {
        }

        /// <summary>
        /// Gets the next interceptor in the chain.
        /// </summary>
        protected ClientOutboundInterceptor Next =>
            MaybeNext ?? throw new InvalidOperationException("No next interceptor");

        /// <summary>
        /// Gets the next interceptor in the chain if any.
        /// </summary>
        private protected ClientOutboundInterceptor? MaybeNext { get; init; }

        /// <summary>
        /// Intercept start workflow calls.
        /// </summary>
        /// <typeparam name="TResult">Result type of the workflow.</typeparam>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Handle for the workflow.</returns>
        public virtual Task<WorkflowHandle<TResult>> StartWorkflowAsync<TResult>(
            StartWorkflowInput input) =>
            Next.StartWorkflowAsync<TResult>(input);

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

        /// <summary>
        /// Intercept async activity heartbeat calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task completion.</returns>
        public virtual Task HeartbeatAsyncActivityAsync(HeartbeatAsyncActivityInput input) =>
            Next.HeartbeatAsyncActivityAsync(input);

        /// <summary>
        /// Intercept async activity complete calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task completion.</returns>
        public virtual Task CompleteAsyncActivityAsync(CompleteAsyncActivityInput input) =>
            Next.CompleteAsyncActivityAsync(input);

        /// <summary>
        /// Intercept async activity fail calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task completion.</returns>
        public virtual Task FailAsyncActivityAsync(FailAsyncActivityInput input) =>
            Next.FailAsyncActivityAsync(input);

        /// <summary>
        /// Intercept async activity report cancellation calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task completion.</returns>
        public virtual Task ReportCancellationAsyncActivityAsync(
            ReportCancellationAsyncActivityInput input) =>
            Next.ReportCancellationAsyncActivityAsync(input);
    }
}