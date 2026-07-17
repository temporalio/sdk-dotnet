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
    /// <para>Obtained via the <see cref="TemporalOperationHandler.FromHandleFactory{TInput, TResult}"/>
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

        /// <summary>
        /// Start a workflow update via a lambda invoking the update method, backing this Nexus
        /// operation with the update.
        /// </summary>
        /// <remarks>
        /// <para>Only <see cref="WorkflowUpdateStage.Accepted"/> is supported for
        /// <c>WaitForStage</c>. </para>
        /// <para>Returns an async result carrying an update-workflow token, unless the update has
        /// already completed (e.g. a retried request with the same update ID), in which case a sync
        /// result is returned; an update that completed with an error surfaces as a failed
        /// operation.</para>
        /// </remarks>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TResult">Update result type.</typeparam>
        /// <param name="workflowId">Target workflow ID.</param>
        /// <param name="updateCall">Invocation of the workflow update method with a result.</param>
        /// <param name="options">Update start options. <c>WaitForStage</c> must be
        /// <see cref="WorkflowUpdateStage.Accepted"/>. If the update ID is unset, the Nexus request
        /// ID is used.</param>
        /// <param name="runId">Target workflow run ID, or null for the latest run.</param>
        /// <returns>An operation result for the update.</returns>
        Task<TemporalOperationResult<TResult>> StartWorkflowUpdateAsync<TWorkflow, TResult>(
            string workflowId,
            Expression<Func<TWorkflow, Task<TResult>>> updateCall,
            WorkflowUpdateStartOptions options,
            string? runId = null);

        /// <summary>
        /// Start a workflow update with no result via a lambda invoking the update method, backing
        /// this Nexus operation with the update.
        /// </summary>
        /// <remarks>
        /// <para>Only <see cref="WorkflowUpdateStage.Accepted"/> is supported for
        /// <c>WaitForStage</c>. The operation requires a callback URL to be present.</para>
        /// </remarks>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="workflowId">Target workflow ID.</param>
        /// <param name="updateCall">Invocation of the workflow update method with no result.</param>
        /// <param name="options">Update start options. <c>WaitForStage</c> must be
        /// <see cref="WorkflowUpdateStage.Accepted"/>. If the update ID is unset, the Nexus request
        /// ID is used.</param>
        /// <param name="runId">Target workflow run ID, or null for the latest run.</param>
        /// <returns>An operation result for the update.</returns>
        Task<TemporalOperationResult<NoValue>> StartWorkflowUpdateAsync<TWorkflow>(
            string workflowId,
            Expression<Func<TWorkflow, Task>> updateCall,
            WorkflowUpdateStartOptions options,
            string? runId = null);

        /// <summary>
        /// Start a workflow update by name, backing this Nexus operation with the update.
        /// </summary>
        /// <remarks>
        /// <para>Only <see cref="WorkflowUpdateStage.Accepted"/> is supported for
        /// <c>WaitForStage</c>. The operation requires a callback URL to be present.</para>
        /// <para>Returns an async result carrying an update-workflow token, unless the update has
        /// already completed (e.g. a retried request with the same update ID), in which case a sync
        /// result is returned; an update that completed with an error surfaces as a failed
        /// operation.</para>
        /// </remarks>
        /// <typeparam name="TResult">Update result type.</typeparam>
        /// <param name="workflowId">Target workflow ID.</param>
        /// <param name="update">Update name.</param>
        /// <param name="args">Update arguments.</param>
        /// <param name="options">Update start options. <c>WaitForStage</c> must be
        /// <see cref="WorkflowUpdateStage.Accepted"/>. If the update ID is unset, the Nexus request
        /// ID is used.</param>
        /// <param name="runId">Target workflow run ID, or null for the latest run.</param>
        /// <returns>An operation result for the update.</returns>
        Task<TemporalOperationResult<TResult>> StartWorkflowUpdateAsync<TResult>(
            string workflowId,
            string update,
            IReadOnlyCollection<object?> args,
            WorkflowUpdateStartOptions options,
            string? runId = null);
    }
}
