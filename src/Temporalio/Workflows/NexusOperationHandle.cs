using System;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    public abstract class NexusOperationHandle
    {
        public abstract string? OperationToken { get; }

        public Task GetResultAsync() => GetResultAsync<ValueTuple>();

        public abstract Task<TResult> GetResultAsync<TResult>();
    }

    public abstract class NexusOperationHandle<TResult> : NexusOperationHandle
    {
        public new Task<TResult> GetResultAsync() => GetResultAsync<TResult>();
    }
}