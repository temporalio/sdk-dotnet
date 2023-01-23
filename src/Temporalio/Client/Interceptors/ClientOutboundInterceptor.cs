using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Temporalio.Api.History.V1;

namespace Temporalio.Client.Interceptors
{
    public abstract class ClientOutboundInterceptor
    {

        protected ClientOutboundInterceptor(ClientOutboundInterceptor next)
        {
            MaybeNext = next;
        }

        private protected ClientOutboundInterceptor() { }

        private protected ClientOutboundInterceptor? MaybeNext { get; init; }

        protected ClientOutboundInterceptor Next => MaybeNext ?? throw new InvalidOperationException("No next interceptor");

        public virtual Task<WorkflowHandle<TResult>> StartWorkflowAsync<TResult>(StartWorkflowInput input) {
            return Next.StartWorkflowAsync<TResult>(input);
        }

        // TODO(cretz): Document that this never returns an empty page while with a next page token
        // is set (i.e. it immediately uses that token internally)
        public virtual Task<WorkflowHistoryEventPage> FetchWorkflowHistoryEventPage(
            FetchWorkflowHistoryEventPageInput input)
        {
            return Next.FetchWorkflowHistoryEventPage(input);
        }

        // TODO(cretz): Lots more
    }
}