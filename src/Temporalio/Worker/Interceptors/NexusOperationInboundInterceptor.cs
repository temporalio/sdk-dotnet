using System;
using System.Threading.Tasks;
using NexusRpc.Handler;

namespace Temporalio.Worker.Interceptors
{
    public abstract class NexusOperationInboundInterceptor
    {
        protected NexusOperationInboundInterceptor(NexusOperationInboundInterceptor next) =>
            MaybeNext = next;

        private protected NexusOperationInboundInterceptor()
        {
        }

        protected NexusOperationInboundInterceptor Next =>
            MaybeNext ?? throw new InvalidOperationException("No next interceptor");

        private protected NexusOperationInboundInterceptor? MaybeNext { get; init; }

        public virtual Task<OperationStartResult<object?>> ExecuteNexusOperationStartAsync(
            ExecuteNexusOperationStartInput input) => Next.ExecuteNexusOperationStartAsync(input);

        public virtual Task ExecuteNexusOperationCancelAsync(
            ExecuteNexusOperationCancelInput input) => Next.ExecuteNexusOperationCancelAsync(input);
    }
}