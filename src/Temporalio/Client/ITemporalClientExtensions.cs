using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    /// <summary>
    /// Extensions for <see cref="ITemporalClient" />.
    /// </summary>
    public static class ITemporalClientExtensions
    {
        /// <summary>
        /// Shortcut for
        /// <see cref="ITemporalClient.StartWorkflowAsync{TResult}(Func{Task{TResult}}, WorkflowOptions)" />
        /// +
        /// <see cref="WorkflowHandle{TResult}.GetResultAsync(bool, RpcOptions?)" />.
        /// </summary>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="client">Client to use.</param>
        /// <param name="workflow">Workflow run method with a result but no argument.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow result.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.WorkflowFailedException">
        /// Workflow did not complete successfully.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task<TResult> ExecuteWorkflowAsync<TResult>(
            this ITemporalClient client,
            Func<Task<TResult>> workflow,
            WorkflowOptions options)
        {
            var handle = await client.StartWorkflowAsync(workflow, options).ConfigureAwait(false);
            return await handle.GetResultAsync(rpcOptions: options.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="ITemporalClient.StartWorkflowAsync{T, TResult}(Func{T, Task{TResult}}, T, WorkflowOptions)" />
        /// +
        /// <see cref="WorkflowHandle{TResult}.GetResultAsync(bool, RpcOptions?)" />.
        /// </summary>
        /// <typeparam name="T">Workflow argument type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="client">Client to use.</param>
        /// <param name="workflow">Workflow run method with a result and argument.</param>
        /// <param name="arg">Workflow argument.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow result.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.WorkflowFailedException">
        /// Workflow did not complete successfully.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task<TResult> ExecuteWorkflowAsync<T, TResult>(
            this ITemporalClient client,
            Func<T, Task<TResult>> workflow,
            T arg,
            WorkflowOptions options)
        {
            var handle = await client.StartWorkflowAsync(
                workflow, arg, options).ConfigureAwait(false);
            return await handle.GetResultAsync(rpcOptions: options.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="ITemporalClient.StartWorkflowAsync(Func{Task}, WorkflowOptions)" />
        /// +
        /// <see cref="WorkflowHandle.GetResultAsync(bool, RpcOptions?)" />.
        /// </summary>
        /// <param name="client">Client to use.</param>
        /// <param name="workflow">Workflow run method with no result or argument.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow completion task.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.WorkflowFailedException">
        /// Workflow did not complete successfully.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task ExecuteWorkflowAsync(
            this ITemporalClient client,
            Func<Task> workflow,
            WorkflowOptions options)
        {
            var handle = await client.StartWorkflowAsync(workflow, options).ConfigureAwait(false);
            await handle.GetResultAsync(rpcOptions: options.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="ITemporalClient.StartWorkflowAsync{T}(Func{T, Task}, T, WorkflowOptions)" />
        /// +
        /// <see cref="WorkflowHandle.GetResultAsync(bool, RpcOptions?)" />.
        /// </summary>
        /// <typeparam name="T">Workflow argument type.</typeparam>
        /// <param name="client">Client to use.</param>
        /// <param name="workflow">Workflow run method with an argument but no result.</param>
        /// <param name="arg">Workflow argument.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow completion task.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.WorkflowFailedException">
        /// Workflow did not complete successfully.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task ExecuteWorkflowAsync<T>(
            this ITemporalClient client,
            Func<T, Task> workflow,
            T arg,
            WorkflowOptions options)
        {
            var handle = await client.StartWorkflowAsync(
                workflow, arg, options).ConfigureAwait(false);
            await handle.GetResultAsync(rpcOptions: options.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="ITemporalClient.StartWorkflowAsync(string, IReadOnlyCollection{object?}, WorkflowOptions)" />
        /// +
        /// <see cref="WorkflowHandle.GetResultAsync(bool, RpcOptions?)" />.
        /// </summary>
        /// <param name="client">Client to use.</param>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow completion task.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.WorkflowFailedException">
        /// Workflow did not complete successfully.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task ExecuteWorkflowAsync(
            this ITemporalClient client,
            string workflow,
            IReadOnlyCollection<object?> args,
            WorkflowOptions options)
        {
            var handle = await client.StartWorkflowAsync(
                workflow, args, options).ConfigureAwait(false);
            await handle.GetResultAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="ITemporalClient.StartWorkflowAsync{TResult}(string, IReadOnlyCollection{object?}, WorkflowOptions)" />
        /// +
        /// <see cref="WorkflowHandle{TResult}.GetResultAsync(bool, RpcOptions?)" />.
        /// </summary>
        /// <typeparam name="TResult">Result type that will be set on the handle.</typeparam>
        /// <param name="client">Client to use.</param>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow result.</returns>
        /// <exception cref="ArgumentException">Invalid options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.WorkflowFailedException">
        /// Workflow did not complete successfully.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task<TResult> ExecuteWorkflowAsync<TResult>(
            this ITemporalClient client,
            string workflow,
            IReadOnlyCollection<object?> args,
            WorkflowOptions options)
        {
            var handle = await client.StartWorkflowAsync<TResult>(workflow, args, options).
                ConfigureAwait(false);
            return await handle.GetResultAsync().ConfigureAwait(false);
        }

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// List workflow histories. This is just a helper combining
        /// <see cref="ITemporalClient.ListWorkflowsAsync" /> and
        /// <see cref="WorkflowHandle.FetchHistoryAsync" />.
        /// </summary>
        /// <param name="client">Client to use.</param>
        /// <param name="query">List query.</param>
        /// <param name="listOptions">Options for the list call.</param>
        /// <param name="historyFetchOptions">Options for each history fetch call.</param>
        /// <returns>Async enumerable of histories.</returns>
        public static async IAsyncEnumerable<WorkflowHistory> ListWorkflowHistoriesAsync(
            this ITemporalClient client,
            string query,
            WorkflowListOptions? listOptions = null,
            WorkflowHistoryEventFetchOptions? historyFetchOptions = null)
        {
            await foreach (var exec in client.ListWorkflowsAsync(query, listOptions))
            {
                yield return await client.GetWorkflowHandle(
                    exec.ID, exec.RunID).FetchHistoryAsync(historyFetchOptions).ConfigureAwait(false);
            }
        }
#endif
    }
}