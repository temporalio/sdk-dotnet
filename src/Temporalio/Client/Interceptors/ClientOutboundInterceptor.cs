using System;
using System.Threading.Tasks;

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
            StartWorkflowInput input)
        {
            return Next.StartWorkflowAsync<TResult>(input);
        }

        // TODO(cretz): Document that this never returns an empty page while with a next page token
        // is set (i.e. it immediately uses that token internally)

        /// <summary>
        /// Intercept a history event page fetch.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>
        /// Event page. This will not return an empty event set and a next page token.
        /// </returns>
        public virtual Task<WorkflowHistoryEventPage> FetchWorkflowHistoryEventPage(
            FetchWorkflowHistoryEventPageInput input)
        {
            return Next.FetchWorkflowHistoryEventPage(input);
        }

        // TODO(cretz): Lots more
    }
}