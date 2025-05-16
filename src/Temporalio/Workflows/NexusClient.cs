using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NexusRpc;
using Temporalio.Common;

namespace Temporalio.Workflows
{
    public abstract class NexusClient
    {
        public abstract string Service { get; }

        public abstract NexusClientOptions Options { get; }

        public async Task ExecuteNexusOperationAsync(
            string operationName, object? arg, NexusOperationOptions? options = null)
        {
            var handle = await StartNexusOperationAsync(operationName, arg, options).ConfigureAwait(true);
            await handle.GetResultAsync().ConfigureAwait(true);
        }

        public async Task<TResult> ExecuteNexusOperationAsync<TResult>(
            string operationName, object? arg, NexusOperationOptions? options = null)
        {
            var handle = await StartNexusOperationAsync<TResult>(operationName, arg, options).ConfigureAwait(true);
            return await handle.GetResultAsync().ConfigureAwait(true);
        }

        public async Task<NexusOperationHandle> StartNexusOperationAsync(
            string operationName, object? arg, NexusOperationOptions? options = null) =>
            await StartNexusOperationAsync<ValueTuple>(operationName, arg, options).ConfigureAwait(true);

        public abstract Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
            string operationName, object? arg, NexusOperationOptions? options = null);
    }

    public abstract class NexusClient<TService> : NexusClient
    {
        public override string Service => ServiceDefinition.Name;

        public abstract ServiceDefinition ServiceDefinition { get; }

        public async Task ExecuteNexusOperationAsync(
            Expression<Action<TService>> operationStartCall,
            NexusOperationOptions? options = null)
        {
            var handle = await StartNexusOperationAsync(operationStartCall, options).ConfigureAwait(true);
            await handle.GetResultAsync().ConfigureAwait(true);
        }

        public async Task<TResult> ExecuteNexusOperationAsync<TResult>(
            Expression<Func<TService, TResult>> operationStartCall,
            NexusOperationOptions? options = null)
        {
            var handle = await StartNexusOperationAsync(operationStartCall, options).ConfigureAwait(true);
            return await handle.GetResultAsync().ConfigureAwait(true);
        }

        public Task<NexusOperationHandle> StartNexusOperationAsync(
            Expression<Action<TService>> operationStartCall,
            NexusOperationOptions? options = null)
        {
            var (method, args) = ExpressionUtil.ExtractCall(operationStartCall);
            // Find name from method
            var opDefn = ServiceDefinition.Operations.Values.FirstOrDefault(v => v.MethodInfo == method);
            if (opDefn == null)
            {
                throw new ArgumentException($"Method {method} not marked as a Nexus service operation");
            }
            // Must only be a single arg
            if (args.Count > 1)
            {
                throw new ArgumentException("Can only have 0 or 1 Nexus argument");
            }
            return StartNexusOperationAsync(opDefn.Name, args.SingleOrDefault(), options);
        }

        public Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
            Expression<Func<TService, TResult>> operationStartCall,
            NexusOperationOptions? options = null)
        {
            var (method, args) = ExpressionUtil.ExtractCall(operationStartCall);
            // Find name from method
            var opDefn = ServiceDefinition.Operations.Values.FirstOrDefault(v => v.MethodInfo == method);
            if (opDefn == null)
            {
                throw new ArgumentException($"Method {method} not marked as a Nexus service operation");
            }
            // Must only be a single arg
            if (args.Count > 1)
            {
                throw new ArgumentException("Can only have 0 or 1 Nexus argument");
            }
            return StartNexusOperationAsync<TResult>(opDefn.Name, args.SingleOrDefault(), options);
        }
    }
}