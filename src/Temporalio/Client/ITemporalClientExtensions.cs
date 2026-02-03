using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

#if NETCOREAPP3_0_OR_GREATER
using Temporalio.Common;
#endif

namespace Temporalio.Client
{
    /// <summary>
    /// Extensions for <see cref="ITemporalClient" />.
    /// </summary>
    public static class ITemporalClientExtensions
    {
        /// <summary>
        /// Shortcut for
        /// <see cref="ITemporalClient.StartWorkflowAsync{T, TResult}(Expression{Func{T, Task{TResult}}}, WorkflowOptions)" />
        /// +
        /// <see cref="WorkflowHandle{TWorkflow, TResult}.GetResultAsync(bool, RpcOptions?)" />.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="client">Client to use.</param>
        /// <param name="workflowRunCall">Invocation of workflow run method with a result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow result.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.WorkflowFailedException">
        /// Workflow did not complete successfully.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task<TResult> ExecuteWorkflowAsync<TWorkflow, TResult>(
            this ITemporalClient client,
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall,
            WorkflowOptions options)
        {
            var handle = await client.StartWorkflowAsync(workflowRunCall, options).ConfigureAwait(false);
            return await handle.GetResultAsync(rpcOptions: options.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Signal and (optionally) start a workflow via lambda invoking the signalWithStart method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="client">Client to use.</param>
        /// <param name="workflowRunCall">Invocation of workflow run method with a result.</param>
        /// <param name="signalCall">Invocation of signal method without a result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow handle for the started workflow.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task<WorkflowHandle<TWorkflow>> SignalWorkflowWithStartAsync<TWorkflow>(
            this ITemporalClient client,
            Expression<Func<TWorkflow, Task>> workflowRunCall,
            Expression<Func<TWorkflow, Task>> signalCall,
            WorkflowOptions options)
        {
            options.SignalWithStart(signalCall);
            return await client.StartWorkflowAsync(workflowRunCall, options).ConfigureAwait(false);
        }

        /// <summary>
        /// Signal and (optionally) start a workflow via lambda invoking the signalWithStart method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="client">Client to use.</param>
        /// <param name="workflowRunCall">Invocation of workflow run method with a result.</param>
        /// <param name="signalCall">Invocation of signal method without a result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow handle for the started workflow.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task<WorkflowHandle<TWorkflow, TResult>> SignalWorkflowWithStartAsync<TWorkflow, TResult>(
            this ITemporalClient client,
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall,
            Expression<Func<TWorkflow, Task>> signalCall,
            WorkflowOptions options)
        {
            options.SignalWithStart(signalCall);
            return await client.StartWorkflowAsync(workflowRunCall, options).ConfigureAwait(false);
        }

        /// <summary>
        /// Shortcut for
        /// <see cref="ITemporalClient.StartWorkflowAsync{T}(Expression{Func{T, Task}}, WorkflowOptions)" />
        /// +
        /// <see cref="WorkflowHandle.GetResultAsync(bool, RpcOptions?)" />.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="client">Client to use.</param>
        /// <param name="workflowRunCall">Invocation of workflow run method without a result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Workflow result.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse policy.
        /// </exception>
        /// <exception cref="Exceptions.WorkflowFailedException">
        /// Workflow did not complete successfully.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task ExecuteWorkflowAsync<TWorkflow>(
            this ITemporalClient client,
            Expression<Func<TWorkflow, Task>> workflowRunCall,
            WorkflowOptions options)
        {
            var handle = await client.StartWorkflowAsync(
                workflowRunCall, options).ConfigureAwait(false);
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
        /// <see cref="ITemporalClient.StartWorkflowAsync(string, IReadOnlyCollection{object?}, WorkflowOptions)" />
        /// +
        /// <see cref="WorkflowHandle.GetResultAsync{TResult}(bool, RpcOptions?)" />.
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
            var handle = await client.StartWorkflowAsync(workflow, args, options).
                ConfigureAwait(false);
            return await handle.GetResultAsync<TResult>().ConfigureAwait(false);
        }

        /// <summary>
        /// Start an update via a call to a WorkflowUpdate attributed method, possibly starting the
        /// workflow at the same time. Note that in some cases this call may fail but the workflow
        /// will still be started. This is a shortcut for
        /// <see cref="ITemporalClient.StartUpdateWithStartWorkflowAsync{TWorkflow}(Expression{Func{TWorkflow, Task}}, WorkflowStartUpdateWithStartOptions)"/>
        /// +
        /// <see cref="WorkflowUpdateHandle.GetResultAsync(RpcOptions?)"/>.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="client">Client to use.</param>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Update options.</param>
        /// <returns>Completed update task.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse and conflict policy.
        /// </exception>
        /// <exception cref="Exceptions.WorkflowUpdateFailedException">
        /// Workflow update failed, but the with-start operation still got a workflow handle.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task ExecuteUpdateWithStartWorkflowAsync<TWorkflow>(
            this ITemporalClient client,
            Expression<Func<TWorkflow, Task>> updateCall,
            WorkflowUpdateWithStartOptions options)
        {
            var handle = await client.StartUpdateWithStartWorkflowAsync(
                updateCall, UpdateWithStartOptionsWithDefaultsForExecute(options)).ConfigureAwait(false);
            await handle.GetResultAsync(rpcOptions: options.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Start an update via a call to a WorkflowUpdate attributed method, possibly starting the
        /// workflow at the same time. Note that in some cases this call may fail but the workflow
        /// will still be started. This is a shortcut for
        /// <see cref="ITemporalClient.StartUpdateWithStartWorkflowAsync{TWorkflow, TUpdateResult}(Expression{Func{TWorkflow, Task{TUpdateResult}}}, WorkflowStartUpdateWithStartOptions)"/>
        /// +
        /// <see cref="WorkflowUpdateHandle.GetResultAsync(RpcOptions?)"/>.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="client">Client to use.</param>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Update options.</param>
        /// <returns>Completed update task.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse and conflict policy.
        /// </exception>
        /// <exception cref="Exceptions.WorkflowUpdateFailedException">
        /// Workflow update failed, but the with-start operation still got a workflow handle.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task<TUpdateResult> ExecuteUpdateWithStartWorkflowAsync<TWorkflow, TUpdateResult>(
            this ITemporalClient client,
            Expression<Func<TWorkflow, Task<TUpdateResult>>> updateCall,
            WorkflowUpdateWithStartOptions options)
        {
            var handle = await client.StartUpdateWithStartWorkflowAsync(
                updateCall, UpdateWithStartOptionsWithDefaultsForExecute(options)).ConfigureAwait(false);
            return await handle.GetResultAsync(rpcOptions: options.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Start an update using its name, possibly starting the workflow at the same time. Note
        /// that in some cases this call may fail but the workflow will still be started. This is a
        /// shortcut for
        /// <see cref="ITemporalClient.StartUpdateWithStartWorkflowAsync(string, IReadOnlyCollection{object?}, WorkflowStartUpdateWithStartOptions)"/>
        /// +
        /// <see cref="WorkflowUpdateHandle.GetResultAsync(RpcOptions?)"/>.
        /// </summary>
        /// <param name="client">Client to use.</param>
        /// <param name="update">Name of the update.</param>
        /// <param name="args">Arguments for the update.</param>
        /// <param name="options">Update options.</param>
        /// <returns>Completed update task.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse and conflict policy.
        /// </exception>
        /// <exception cref="Exceptions.WorkflowUpdateFailedException">
        /// Workflow update failed, but the with-start operation still got a workflow handle.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task ExecuteUpdateWithStartWorkflowAsync(
            this ITemporalClient client,
            string update,
            IReadOnlyCollection<object?> args,
            WorkflowUpdateWithStartOptions options)
        {
            var handle = await client.StartUpdateWithStartWorkflowAsync(
                update, args, UpdateWithStartOptionsWithDefaultsForExecute(options)).ConfigureAwait(false);
            await handle.GetResultAsync(rpcOptions: options.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Start an update using its name, possibly starting the workflow at the same time. Note
        /// that in some cases this call may fail but the workflow will still be started. This is a
        /// shortcut for
        /// <see cref="ITemporalClient.StartUpdateWithStartWorkflowAsync{TUpdateResult}(string, IReadOnlyCollection{object?}, WorkflowStartUpdateWithStartOptions)"/>
        /// +
        /// <see cref="WorkflowUpdateHandle.GetResultAsync(RpcOptions?)"/>.
        /// </summary>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="client">Client to use.</param>
        /// <param name="update">Name of the update.</param>
        /// <param name="args">Arguments for the update.</param>
        /// <param name="options">Update options.</param>
        /// <returns>Completed update task.</returns>
        /// <exception cref="ArgumentException">Invalid run call or options.</exception>
        /// <exception cref="Exceptions.WorkflowAlreadyStartedException">
        /// Workflow was already started according to ID reuse and conflict policy.
        /// </exception>
        /// <exception cref="Exceptions.WorkflowUpdateFailedException">
        /// Workflow update failed, but the with-start operation still got a workflow handle.
        /// </exception>
        /// <exception cref="Exceptions.RpcException">Server-side error.</exception>
        public static async Task<TUpdateResult> ExecuteUpdateWithStartWorkflowAsync<TUpdateResult>(
            this ITemporalClient client,
            string update,
            IReadOnlyCollection<object?> args,
            WorkflowUpdateWithStartOptions options)
        {
            var handle = await client.StartUpdateWithStartWorkflowAsync<TUpdateResult>(
                update, args, UpdateWithStartOptionsWithDefaultsForExecute(options)).ConfigureAwait(false);
            return await handle.GetResultAsync(rpcOptions: options.Rpc).ConfigureAwait(false);
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
            await foreach (var exec in client.ListWorkflowsAsync(query, listOptions).ConfigureAwait(false))
            {
                yield return await client.GetWorkflowHandle(
                    exec.Id, exec.RunId).FetchHistoryAsync(historyFetchOptions).ConfigureAwait(false);
            }
        }
#endif

        private static WorkflowStartUpdateWithStartOptions UpdateWithStartOptionsWithDefaultsForExecute(
            WorkflowUpdateWithStartOptions options) =>
            (WorkflowStartUpdateWithStartOptions)new WorkflowStartUpdateWithStartOptions()
            {
                Id = options.Id,
                Rpc = options.Rpc,
                StartWorkflowOperation = options.StartWorkflowOperation,
                WaitForStage = WorkflowUpdateStage.Completed,
            }.Clone();
    }
}
