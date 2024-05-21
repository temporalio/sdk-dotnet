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
        /// Gets or sets the known outcome.
        /// </summary>
        internal Outcome? KnownOutcome { get; set; }

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
            await PollUntilOutcomeAsync(rpcOptions).ConfigureAwait(false);

            // Convert outcome to result
            if (KnownOutcome!.Failure is { } failure)
            {
                throw new WorkflowUpdateFailedException(
                    await Client.Options.DataConverter.ToExceptionAsync(failure).ConfigureAwait(false));
            }
            else if (KnownOutcome.Success is { } success)
            {
                // Ignore return if they didn't want it
                if (typeof(TResult) == typeof(ValueTuple))
                {
                    return default!;
                }
                return await Client.Options.DataConverter.ToSingleValueAsync<TResult>(
                    success.Payloads_).ConfigureAwait(false);
            }
            throw new InvalidOperationException($"Unrecognized outcome case: {KnownOutcome.ValueCase}");
        }

        /// <summary>
        /// Poll until a memoized outcome is set on this handle.
        /// </summary>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal async Task PollUntilOutcomeAsync(RpcOptions? rpcOptions = null)
        {
            // If there is not a known outcome, we must poll for one. We intentionally do not lock
            // while obtaining the outcome. In the case of concurrent get-result calls, they will
            // poll independently and first one can set the known outcome (setting properties in
            // .NET is thread safe).
            if (KnownOutcome != null)
            {
                return;
            }
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
            // Continually retry to poll while we get an empty response
            while (KnownOutcome == null)
            {
                var resp = await Client.Connection.WorkflowService.PollWorkflowExecutionUpdateAsync(
                    req, rpcOptions).ConfigureAwait(false);
#pragma warning disable CA1508
                // .NET incorrectly assumes KnownOutcome cannot be null here because they assume a
                // single thread. We accept there is technically a race condition here since this is
                // not an atomic CAS operation, but outcome is the same server side for the same
                // update.
                KnownOutcome ??= resp.Outcome;
#pragma warning restore CA1508
            }
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