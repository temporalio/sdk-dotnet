using System;
using System.Threading.Tasks;
using Temporalio.Api.Update.V1;

namespace Temporalio.Client
{
    /// <summary>
    /// Workflow update handle to perform actions on an individual workflow update.
    /// </summary>
    /// <param name="Client">Client used for update handle calls.</param>
    /// <param name="Id">Update ID.</param>
    /// <param name="WorkflowId">Workflow ID.</param>
    /// <param name="WorkflowRunId">Run ID if any.</param>
    /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
    public record WorkflowUpdateHandle(
        ITemporalClient Client,
        string Id,
        string WorkflowId,
        string? WorkflowRunId = null)
    {
        /// <summary>
        /// Gets the known outcome.
        /// </summary>
        internal Outcome? KnownOutcome { private get; init; }

        /// <summary>
        /// Wait for an update result disregarding any return value.
        /// </summary>
        /// <param name="timeout">Optional timeout. Defaults to 1 minute, but can be set to
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan" /> but probably shouldn't. A
        /// timeout will throw a <see cref="OperationCanceledException" />.</param>
        /// <param name="rpcOptions">Extra RPC options.</param>
        /// <returns>Completed update task.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public Task GetResultAsync(TimeSpan? timeout = null, RpcOptions? rpcOptions = null) =>
            GetResultAsync<ValueTuple>(timeout, rpcOptions);

        /// <summary>
        /// Wait for an update result.
        /// </summary>
        /// <typeparam name="TResult">Update result type.</typeparam>
        /// <param name="timeout">Optional timeout. Defaults to 1 minute, but can be set to
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan" /> but probably shouldn't. A
        /// timeout will throw a <see cref="OperationCanceledException" />.</param>
        /// <param name="rpcOptions">Extra RPC options.</param>
        /// <returns>Completed update result.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public virtual Task<TResult> GetResultAsync<TResult>(
            TimeSpan? timeout = null, RpcOptions? rpcOptions = null) =>
            // If there is a known outcome, use that, otherwise poll. We intentionally are not
            // memoizing the poll result, so each call to GetResultAsync when a known outcome is not
            // present results in a poll call.
            KnownOutcome is { } knownOutcome ?
                TemporalClient.Impl.ConvertWorkflowUpdateOutcomeToTaskAsync<TResult>(Client, knownOutcome) :
                Client.OutboundInterceptor.PollWorkflowUpdateAsync<TResult>(new(
                    Id: Id,
                    WorkflowId: WorkflowId,
                    WorkflowRunId: WorkflowRunId,
                    Timeout: timeout ?? TimeSpan.FromMinutes(1),
                    RpcOptions: rpcOptions));
    }

    /// <summary>
    /// Workflow update handle to perform actions on an individual workflow update.
    /// </summary>
    /// <typeparam name="TResult">Update result type.</typeparam>
    /// <param name="Client">Client used for update handle calls.</param>
    /// <param name="Id">Update ID.</param>
    /// <param name="WorkflowId">Workflow ID.</param>
    /// <param name="WorkflowRunId">Run ID if any.</param>
    /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
    public record WorkflowUpdateHandle<TResult>(
        ITemporalClient Client,
        string Id,
        string WorkflowId,
        string? WorkflowRunId = null) :
            WorkflowUpdateHandle(Client, Id, WorkflowId, WorkflowRunId)
    {
        /// <summary>
        /// Wait for an update result.
        /// </summary>
        /// <param name="timeout">Optional timeout. Defaults to 1 minute, but can be set to
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan" /> but probably shouldn't. A
        /// timeout will throw a <see cref="OperationCanceledException" />.</param>
        /// <param name="rpcOptions">Extra RPC options.</param>
        /// <returns>Completed update result.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public new Task<TResult> GetResultAsync(
            TimeSpan? timeout = null, RpcOptions? rpcOptions = null) =>
            GetResultAsync<TResult>(timeout, rpcOptions);
    }
}