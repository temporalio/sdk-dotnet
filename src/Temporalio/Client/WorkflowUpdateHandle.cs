using System;
using System.Threading.Tasks;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Update.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;

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
        /// <param name="rpcOptions">Extra RPC options.</param>
        /// <returns>Completed update task.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public Task GetResultAsync(RpcOptions? rpcOptions = null) =>
            GetResultAsync<ValueTuple>(rpcOptions);

        /// <summary>
        /// Wait for an update result.
        /// </summary>
        /// <typeparam name="TResult">Update result type.</typeparam>
        /// <param name="rpcOptions">Extra RPC options.</param>
        /// <returns>Completed update result.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public virtual async Task<TResult> GetResultAsync<TResult>(RpcOptions? rpcOptions = null)
        {
            // If there is not a known outcome, we must poll for one. We intentionally do not
            // memoize poll result, so each call to this function when a known outcome is not
            // present results in a poll call.
            var outcome = KnownOutcome;
            if (outcome == null)
            {
                // No known outcome means poll
                var req = new PollWorkflowExecutionUpdateRequest()
                {
                    Namespace = Client.Options.Namespace,
                    UpdateRef = new()
                    {
                        WorkflowExecution = new()
                        {
                            WorkflowId = WorkflowId,
                            RunId = WorkflowRunId ?? string.Empty,
                        },
                        UpdateId = Id,
                    },
                    Identity = Client.Connection.Options.Identity,
                    WaitPolicy = new() { LifecycleStage = UpdateWorkflowExecutionLifecycleStage.Completed },
                };
                // Continually retry to poll while we either get empty response or while we get a gRPC
                // deadline exceeded but our cancellation token isn't complete.
                while (outcome == null)
                {
                    try
                    {
                        var resp = await Client.Connection.WorkflowService.PollWorkflowExecutionUpdateAsync(
                            req, rpcOptions).ConfigureAwait(false);
                        outcome = resp.Outcome;
                    }
                    catch (RpcException e) when (
                        e.Code == RpcException.StatusCode.DeadlineExceeded &&
                        rpcOptions?.CancellationToken?.IsCancellationRequested != true)
                    {
                        // Do nothing, our cancellation token wasn't done, continue
                        // TODO(cretz): Remove when server stops using gRPC status to signal not done yet
                    }
                }
            }

            // Convert outcome to result
            if (outcome.Failure is { } failure)
            {
                throw new WorkflowUpdateFailedException(
                    await Client.Options.DataConverter.ToExceptionAsync(failure).ConfigureAwait(false));
            }
            else if (outcome.Success is { } success)
            {
                // Ignore return if they didn't want it
                if (typeof(TResult) == typeof(ValueTuple))
                {
                    return default!;
                }
                return await Client.Options.DataConverter.ToSingleValueAsync<TResult>(
                    success.Payloads_).ConfigureAwait(false);
            }
            throw new InvalidOperationException($"Unrecognized outcome case: {outcome.ValueCase}");
        }
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
        /// <param name="rpcOptions">Extra RPC options.</param>
        /// <returns>Completed update result.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public new Task<TResult> GetResultAsync(RpcOptions? rpcOptions = null) =>
            GetResultAsync<TResult>(rpcOptions);
    }
}