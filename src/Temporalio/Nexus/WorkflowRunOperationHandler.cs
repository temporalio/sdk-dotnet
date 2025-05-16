using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NexusRpc;
using NexusRpc.Handler;
using Temporalio.Client;

namespace Temporalio.Nexus
{
    public static class WorkflowRunOperationHandler
    {
        public static IOperationHandler<TInput, TResult> FromHandleFactory<TInput, TResult>(
            Func<WorkflowRunOperationContext, TInput, Task<NexusWorkflowRunHandle<TResult>>> handleFactory) =>
            new WorkflowRunOperationHandler<TInput, TResult>(handleFactory);
    }

    internal class WorkflowRunOperationHandler<TInput, TResult> : IOperationHandler<TInput, TResult>
    {
        private readonly Func<WorkflowRunOperationContext, TInput, Task<NexusWorkflowRunHandle<TResult>>> handleFactory;

        internal WorkflowRunOperationHandler(
            Func<WorkflowRunOperationContext, TInput, Task<NexusWorkflowRunHandle<TResult>>> handleFactory) =>
            this.handleFactory = handleFactory;

        public async Task<OperationStartResult<TResult>> StartAsync(
            OperationStartContext context, TInput input)
        {
            var handle = await handleFactory(new(context), input).ConfigureAwait(false);
            return OperationStartResult.AsyncResult<TResult>(handle.ToToken());
        }

        public Task<TResult> FetchResultAsync(OperationFetchResultContext context) =>
            throw new NotImplementedException();

        public Task<OperationInfo> FetchInfoAsync(OperationFetchInfoContext context) =>
            throw new NotImplementedException();

        public Task CancelAsync(OperationCancelContext context)
        {
            NexusWorkflowRunHandle<object> handle;
            try
            {
                handle = NexusWorkflowRunHandle<object>.FromToken(context.OperationToken);
            }
            catch (ArgumentException e)
            {
                throw new HandlerException(HandlerErrorType.BadRequest, e.Message);
            }
            if (handle.Namespace != NexusOperationExecutionContext.Current.Info.Namespace)
            {
                throw new HandlerException(HandlerErrorType.BadRequest, "Invalid namespace");
            }
            return NexusOperationExecutionContext.Current.TemporalClient.
                GetWorkflowHandle(handle.WorkflowId).CancelAsync();
        }
    }
}