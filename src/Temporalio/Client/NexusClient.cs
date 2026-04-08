#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NexusRpc;
using Temporalio.Common;

namespace Temporalio.Client
{
    /// <summary>
    /// Client for making standalone Nexus service calls.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public abstract class NexusClient
    {
        /// <summary>
        /// Gets the service name.
        /// </summary>
        public abstract string Service { get; }

        /// <summary>
        /// Gets the endpoint name.
        /// </summary>
        public abstract string Endpoint { get; }

        /// <summary>
        /// Gets the underlying Temporal client.
        /// </summary>
        public abstract ITemporalClient Client { get; }

        /// <summary>
        /// Shortcut for
        /// <see cref="StartNexusOperationAsync(string, object?, NexusOperationOptions?)"/>
        /// +
        /// <see cref="NexusOperationHandle.GetResultAsync"/>.
        /// </summary>
        /// <param name="operationName">Operation name to start.</param>
        /// <param name="arg">Operation argument.</param>
        /// <param name="options">Operation options.</param>
        /// <returns>Task representing completion of the Nexus operation.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public async Task ExecuteNexusOperationAsync(
            string operationName, object? arg, NexusOperationOptions? options = null)
        {
            var handle = await StartNexusOperationAsync(operationName, arg, options).ConfigureAwait(false);
            await handle.GetResultAsync(
                options?.Rpc == null ? null : new NexusOperationGetResultOptions { Rpc = options.Rpc }).ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="StartNexusOperationAsync{TResult}(string, object?, NexusOperationOptions?)"/>
        /// +
        /// <see cref="NexusOperationHandle{TResult}.GetResultAsync"/>.
        /// </summary>
        /// <typeparam name="TResult">Operation result type.</typeparam>
        /// <param name="operationName">Operation name to start.</param>
        /// <param name="arg">Operation argument.</param>
        /// <param name="options">Operation options.</param>
        /// <returns>Task with the result of the Nexus operation.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public async Task<TResult> ExecuteNexusOperationAsync<TResult>(
            string operationName, object? arg, NexusOperationOptions? options = null)
        {
            var handle = await StartNexusOperationAsync<TResult>(operationName, arg, options).ConfigureAwait(false);
            return await handle.GetResultAsync(
                options?.Rpc == null ? null : new NexusOperationGetResultOptions { Rpc = options.Rpc }).ConfigureAwait(false);
        }

        /// <summary>
        /// Start a Nexus operation by name.
        /// </summary>
        /// <param name="operationName">Operation name to start.</param>
        /// <param name="arg">Operation argument.</param>
        /// <param name="options">Operation options.</param>
        /// <returns>Handle to the started operation once started.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public async Task<NexusOperationHandle> StartNexusOperationAsync(
            string operationName, object? arg, NexusOperationOptions? options = null) =>
            await StartNexusOperationAsync<ValueTuple>(operationName, arg, options).ConfigureAwait(false);

        /// <summary>
        /// Start a Nexus operation by name with specific expected result type.
        /// </summary>
        /// <typeparam name="TResult">Operation result type.</typeparam>
        /// <param name="operationName">Operation name to start.</param>
        /// <param name="arg">Operation argument.</param>
        /// <param name="options">Operation options.</param>
        /// <returns>Handle to the started operation once started.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public abstract Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
            string operationName, object? arg, NexusOperationOptions? options = null);
    }

    /// <inheritdoc />
    /// <typeparam name="TService">Nexus service type.</typeparam>
    public abstract class NexusClient<TService> : NexusClient
    {
        /// <summary>
        /// Gets the service name.
        /// </summary>
        public override string Service => ServiceDefinition.Name;

        /// <summary>
        /// Gets the service definition.
        /// </summary>
        public abstract ServiceDefinition ServiceDefinition { get; }

        /// <summary>
        /// Shortcut for
        /// <see cref="StartNexusOperationAsync(Expression{Action{TService}}, NexusOperationOptions?)"/>
        /// +
        /// <see cref="NexusOperationHandle.GetResultAsync"/>.
        /// </summary>
        /// <param name="operationStartCall">Invocation of operation without a result.</param>
        /// <param name="options">Operation options.</param>
        /// <returns>Task representing completion of the Nexus operation.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public async Task ExecuteNexusOperationAsync(
            Expression<Action<TService>> operationStartCall,
            NexusOperationOptions? options = null)
        {
            var handle = await StartNexusOperationAsync(operationStartCall, options).ConfigureAwait(false);
            await handle.GetResultAsync(
                options?.Rpc == null ? null : new NexusOperationGetResultOptions { Rpc = options.Rpc }).ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="StartNexusOperationAsync{TResult}(Expression{Func{TService, TResult}}, NexusOperationOptions?)"/>
        /// +
        /// <see cref="NexusOperationHandle{TResult}.GetResultAsync"/>.
        /// </summary>
        /// <typeparam name="TResult">Operation result type.</typeparam>
        /// <param name="operationStartCall">Invocation of operation with a result.</param>
        /// <param name="options">Operation options.</param>
        /// <returns>Task with the result of the Nexus operation.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public async Task<TResult> ExecuteNexusOperationAsync<TResult>(
            Expression<Func<TService, TResult>> operationStartCall,
            NexusOperationOptions? options = null)
        {
            var handle = await StartNexusOperationAsync(operationStartCall, options).ConfigureAwait(false);
            return await handle.GetResultAsync(
                options?.Rpc == null ? null : new NexusOperationGetResultOptions { Rpc = options.Rpc }).ConfigureAwait(false);
        }

        /// <summary>
        /// Start a Nexus operation via a lambda invoking the operation on the service.
        /// </summary>
        /// <param name="operationStartCall">Invocation of operation without a result.</param>
        /// <param name="options">Operation options.</param>
        /// <returns>Handle to the started operation once started.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public Task<NexusOperationHandle> StartNexusOperationAsync(
            Expression<Action<TService>> operationStartCall,
            NexusOperationOptions? options = null)
        {
            var (method, args) = ExpressionUtil.ExtractCall(operationStartCall);
            var opDefn = ServiceDefinition.Operations.Values.FirstOrDefault(v => v.MethodInfo == method);
            if (opDefn == null)
            {
                throw new ArgumentException($"Method {method} not marked as a Nexus service operation");
            }
            if (args.Count > 1)
            {
                throw new ArgumentException("Can only have 0 or 1 Nexus argument");
            }
            return StartNexusOperationAsync(opDefn.Name, args.SingleOrDefault(), options);
        }

        /// <summary>
        /// Start a Nexus operation via a lambda invoking the operation on the service.
        /// </summary>
        /// <typeparam name="TResult">Operation result type.</typeparam>
        /// <param name="operationStartCall">Invocation of operation with a result.</param>
        /// <param name="options">Operation options.</param>
        /// <returns>Handle to the started operation once started.</returns>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
            Expression<Func<TService, TResult>> operationStartCall,
            NexusOperationOptions? options = null)
        {
            var (method, args) = ExpressionUtil.ExtractCall(operationStartCall);
            var opDefn = ServiceDefinition.Operations.Values.FirstOrDefault(v => v.MethodInfo == method);
            if (opDefn == null)
            {
                throw new ArgumentException($"Method {method} not marked as a Nexus service operation");
            }
            if (args.Count > 1)
            {
                throw new ArgumentException("Can only have 0 or 1 Nexus argument");
            }
            return StartNexusOperationAsync<TResult>(opDefn.Name, args.SingleOrDefault(), options);
        }
    }
}
