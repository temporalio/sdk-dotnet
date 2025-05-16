using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexusRpc;
using NexusRpc.Handler;
using Temporalio.Worker.Interceptors;

namespace Temporalio.Worker
{
    internal class NexusMiddlewareForInterceptors : IOperationMiddleware
    {
        private readonly IReadOnlyCollection<IWorkerInterceptor> interceptors;

        public NexusMiddlewareForInterceptors(
            IReadOnlyCollection<IWorkerInterceptor> interceptors) =>
            this.interceptors = interceptors;

        public IOperationHandler<object?, object?> Intercept(
            OperationContext context, IOperationHandler<object?, object?> nextHandler)
        {
            var inbound = interceptors.Reverse().Aggregate(
                (NexusOperationInboundInterceptor)new InboundImpl(nextHandler),
                (v, impl) => impl.InterceptNexusOperation(v));
            return new HandlerImpl(inbound);
        }

        private class HandlerImpl : IOperationHandler<object?, object?>
        {
            private readonly NexusOperationInboundInterceptor nextInterceptor;

            public HandlerImpl(NexusOperationInboundInterceptor nextInterceptor) =>
                this.nextInterceptor = nextInterceptor;

            public Task<OperationStartResult<object?>> StartAsync(
                OperationStartContext context, object? input) =>
                nextInterceptor.ExecuteNexusOperationStartAsync(new(context, input));

            public Task<object?> FetchResultAsync(OperationFetchResultContext context) =>
                throw new System.NotImplementedException();

            public Task<OperationInfo> FetchInfoAsync(OperationFetchInfoContext context) =>
                throw new System.NotImplementedException();

            public Task CancelAsync(OperationCancelContext context) =>
                nextInterceptor.ExecuteNexusOperationCancelAsync(new(context));
        }

        private class InboundImpl : NexusOperationInboundInterceptor
        {
            private readonly IOperationHandler<object?, object?> nextHandler;

            public InboundImpl(IOperationHandler<object?, object?> nextHandler) =>
                this.nextHandler = nextHandler;

            public override Task<OperationStartResult<object?>> ExecuteNexusOperationStartAsync(
                ExecuteNexusOperationStartInput input) =>
                nextHandler.StartAsync(input.Context, input.Input);

            public override Task ExecuteNexusOperationCancelAsync(
                ExecuteNexusOperationCancelInput input) =>
                nextHandler.CancelAsync(input.Context);
        }
    }
}