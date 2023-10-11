using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    /// <summary>
    /// Extensions for <see cref="WorkflowHandle" />.
    /// </summary>
    public static class WorkflowHandleExtensions
    {
        /// <summary>
        /// Shortcut for
        /// <see cref="WorkflowHandle.StartUpdateAsync{TWorkflow}(Expression{Func{TWorkflow, Task}}, WorkflowUpdateOptions?)" />
        /// +
        /// <see cref="WorkflowUpdateHandle.GetResultAsync(TimeSpan?, RpcOptions?)" />.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="workflowHandle">Workflow handle to use.</param>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Completed update task.</returns>
        public static async Task ExecuteUpdateAsync<TWorkflow>(
            this WorkflowHandle workflowHandle,
            Expression<Func<TWorkflow, Task>> updateCall,
            WorkflowUpdateOptions? options = null)
        {
            // TODO(cretz): Should we accept a timeout for GetResult? Or just reuse RPC timeout and
            // cancellation token?
            // TODO(cretz): Should we force-set wait for stage as completed? Still need to have
            // discussion on what we should do on timeout of StartUpdateAsync while waiting for
            // completion.
            var handle = await workflowHandle.StartUpdateAsync(
                updateCall, options).ConfigureAwait(false);
            await handle.GetResultAsync(rpcOptions: options?.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="WorkflowHandle.StartUpdateAsync{TWorkflow, TUpdateResult}(Expression{Func{TWorkflow, Task{TUpdateResult}}}, WorkflowUpdateOptions?)" />
        /// +
        /// <see cref="WorkflowUpdateHandle{TResult}.GetResultAsync(TimeSpan?, RpcOptions?)" />.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="workflowHandle">Workflow handle to use.</param>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Completed update task.</returns>
        public static async Task<TUpdateResult> ExecuteUpdateAsync<TWorkflow, TUpdateResult>(
            this WorkflowHandle workflowHandle,
            Expression<Func<TWorkflow, Task<TUpdateResult>>> updateCall,
            WorkflowUpdateOptions? options = null)
        {
            var handle = await workflowHandle.StartUpdateAsync(
                updateCall, options).ConfigureAwait(false);
            return await handle.GetResultAsync(rpcOptions: options?.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="WorkflowHandle.StartUpdateAsync(string, IReadOnlyCollection{object?}, WorkflowUpdateOptions?)" />
        /// +
        /// <see cref="WorkflowUpdateHandle.GetResultAsync(TimeSpan?, RpcOptions?)" />.
        /// </summary>
        /// <param name="workflowHandle">Workflow handle to use.</param>
        /// <param name="update">Update name.</param>
        /// <param name="args">Update args.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Completed update task.</returns>
        public static async Task ExecuteUpdateAsync(
            this WorkflowHandle workflowHandle,
            string update,
            IReadOnlyCollection<object?> args,
            WorkflowUpdateOptions? options = null)
        {
            var handle = await workflowHandle.StartUpdateAsync(
                update, args, options).ConfigureAwait(false);
            await handle.GetResultAsync(rpcOptions: options?.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="WorkflowHandle.StartUpdateAsync{TUpdateResult}(string, IReadOnlyCollection{object?}, WorkflowUpdateOptions?)" />
        /// +
        /// <see cref="WorkflowUpdateHandle.GetResultAsync(TimeSpan?, RpcOptions?)" />.
        /// </summary>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="workflowHandle">Workflow handle to use.</param>
        /// <param name="update">Update name.</param>
        /// <param name="args">Update args.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Completed update task.</returns>
        public static async Task<TUpdateResult> ExecuteUpdateAsync<TUpdateResult>(
            this WorkflowHandle workflowHandle,
            string update,
            IReadOnlyCollection<object?> args,
            WorkflowUpdateOptions? options = null)
        {
            var handle = await workflowHandle.StartUpdateAsync<TUpdateResult>(
                update, args, options).ConfigureAwait(false);
            return await handle.GetResultAsync(rpcOptions: options?.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="WorkflowHandle{TWorkflow}.StartUpdateAsync(Expression{Func{TWorkflow, Task}}, WorkflowUpdateOptions?)" />
        /// +
        /// <see cref="WorkflowUpdateHandle.GetResultAsync(TimeSpan?, RpcOptions?)" />.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="workflowHandle">Workflow handle to use.</param>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Completed update task.</returns>
        public static async Task ExecuteUpdateAsync<TWorkflow>(
            this WorkflowHandle<TWorkflow> workflowHandle,
            Expression<Func<TWorkflow, Task>> updateCall,
            WorkflowUpdateOptions? options = null)
        {
            var handle = await workflowHandle.StartUpdateAsync(
                updateCall, options).ConfigureAwait(false);
            await handle.GetResultAsync(rpcOptions: options?.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="WorkflowHandle{TWorkflow}.StartUpdateAsync{TUpdateResult}(Expression{Func{TWorkflow, Task{TUpdateResult}}}, WorkflowUpdateOptions?)" />
        /// +
        /// <see cref="WorkflowUpdateHandle{TResult}.GetResultAsync(TimeSpan?, RpcOptions?)" />.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="workflowHandle">Workflow handle to use.</param>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Completed update task.</returns>
        public static async Task<TUpdateResult> ExecuteUpdateAsync<TWorkflow, TUpdateResult>(
            this WorkflowHandle<TWorkflow> workflowHandle,
            Expression<Func<TWorkflow, Task<TUpdateResult>>> updateCall,
            WorkflowUpdateOptions? options = null)
        {
            var handle = await workflowHandle.StartUpdateAsync(
                updateCall, options).ConfigureAwait(false);
            return await handle.GetResultAsync(rpcOptions: options?.Rpc).ConfigureAwait(false);
        }
    }
}