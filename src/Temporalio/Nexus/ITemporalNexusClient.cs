using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NexusRpc.Handlers;
using Temporalio.Client;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Nexus-aware client wrapping the Temporal client. Provides methods for starting workflows
    /// from within a Nexus operation handler.
    /// </summary>
    /// <remarks>
    /// <para>WARNING: Nexus support is experimental.</para>
    /// <para>Obtained via the <see cref="TemporalNexusOperationHandler.FromHandleFactory{TInput, TResult}"/>
    /// start function parameter.</para>
    /// <para>Example usage — starting a workflow from an operation handler:</para>
    /// <code>
    /// await client.StartWorkflowAsync&lt;MyWorkflow, MyResult&gt;(
    ///     wf => wf.RunAsync(input),
    ///     new(id: "my-workflow-id", taskQueue: "my-task-queue"));
    /// </code>
    /// <para>To perform a synchronous operation (e.g., sending a signal), use the underlying
    /// <see cref="TemporalClient"/> and return a sync result:</para>
    /// <code>
    /// await client.TemporalClient
    ///     .GetWorkflowHandle($"order-{input.OrderId}")
    ///     .SignalAsync("requestCancellation", new[] { input });
    /// return TemporalOperationResult&lt;NoValue&gt;.SyncResult(default);
    /// </code>
    /// </remarks>
    public interface ITemporalNexusClient
    {
        /// <summary>
        /// Gets the underlying Temporal client for advanced use cases such as sending signals
        /// or queries.
        /// </summary>
        ITemporalClient TemporalClient { get; }

        /// <summary>
        /// Start a workflow via a lambda invoking the run method. Always returns an async result
        /// with a workflow-run operation token.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method with a result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>An async operation result containing the workflow-run token.</returns>
        Task<TemporalOperationResult<TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, WorkflowOptions options);

        /// <summary>
        /// Start a workflow via a lambda invoking the run method with no return value. Always
        /// returns an async result with a workflow-run operation token.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method with no result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>An async operation result containing the workflow-run token.</returns>
        Task<TemporalOperationResult<NoValue>> StartWorkflowAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> workflowRunCall, WorkflowOptions options);

        /// <summary>
        /// Start a workflow by name. Always returns an async result with a workflow-run operation
        /// token.
        /// </summary>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>An async operation result containing the workflow-run token.</returns>
        Task<TemporalOperationResult<TResult>> StartWorkflowAsync<TResult>(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options);
    }
}
