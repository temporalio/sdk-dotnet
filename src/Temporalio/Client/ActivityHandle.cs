using System;
using System.Threading.Tasks;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;

namespace Temporalio.Client
{
    /// <summary>
    /// Handle for a standalone activity to perform actions on.
    /// </summary>
    /// <param name="Client">Client used for activity handle calls.</param>
    /// <param name="Id">Activity ID.</param>
    /// <param name="RunId">Activity run ID if known.</param>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public record ActivityHandle(
        ITemporalClient Client,
        string Id,
        string? RunId = null)
    {
        /// <summary>
        /// Wait for the result of the activity, discarding the return value.
        /// </summary>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Task that completes when the activity completes.</returns>
        /// <exception cref="ActivityFailedException">
        /// Exception thrown for unsuccessful activity result.
        /// </exception>
        /// <exception cref="RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public Task GetResultAsync(RpcOptions? rpcOptions = null) =>
            GetResultAsync<ValueTuple>(rpcOptions);

        /// <summary>
        /// Wait for the result of the activity, deserializing into the given type.
        /// </summary>
        /// <typeparam name="TResult">Return type to deserialize result into.</typeparam>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Result of the activity.</returns>
        /// <exception cref="ActivityFailedException">
        /// Exception thrown for unsuccessful activity result.
        /// </exception>
        /// <exception cref="RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public virtual async Task<TResult> GetResultAsync<TResult>(
            RpcOptions? rpcOptions = null)
        {
            // Activity-specific data converter
            var dataConverter = Client.Options.DataConverter.WithSerializationContext(
                new ISerializationContext.Activity(
                    Namespace: Client.Options.Namespace,
                    ActivityId: Id,
                    WorkflowId: null,
                    WorkflowType: null,
                    ActivityType: null,
                    ActivityTaskQueue: null,
                    IsLocal: false));

            // Continually poll until outcome is available
            var req = new PollActivityExecutionRequest()
            {
                Namespace = Client.Options.Namespace,
                ActivityId = Id,
                RunId = RunId ?? string.Empty,
            };
            var rpc = TemporalClient.DefaultRetryOptions(rpcOptions);
            while (true)
            {
                var resp = await Client.Connection.WorkflowService.PollActivityExecutionAsync(
                    req, rpc).ConfigureAwait(false);

                // If no outcome, keep polling
                if (resp.Outcome == null)
                {
                    continue;
                }

                // Handle outcome
                switch (resp.Outcome.ValueCase)
                {
                    case Api.Activity.V1.ActivityExecutionOutcome.ValueOneofCase.Result:
                        // Use default if they are ignoring result or payload not present
                        if (typeof(TResult) == typeof(ValueTuple) ||
                            resp.Outcome.Result == null ||
                            resp.Outcome.Result.Payloads_.Count == 0)
                        {
                            return default!;
                        }
                        return await dataConverter.ToSingleValueAsync<TResult>(
                            resp.Outcome.Result.Payloads_).ConfigureAwait(false);
                    case Api.Activity.V1.ActivityExecutionOutcome.ValueOneofCase.Failure:
                        throw new ActivityFailedException(
                            await dataConverter.ToExceptionAsync(
                                resp.Outcome.Failure).ConfigureAwait(false));
                    default:
                        throw new InvalidOperationException(
                            $"Unexpected activity outcome type: {resp.Outcome.ValueCase}");
                }
            }
        }

        /// <summary>
        /// Describe this activity.
        /// </summary>
        /// <param name="options">Extra options.</param>
        /// <returns>Description for the activity.</returns>
        /// <exception cref="RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public virtual Task<ActivityExecutionDescription> DescribeAsync(
            ActivityDescribeOptions? options = null) =>
            Client.OutboundInterceptor.DescribeActivityAsync(new(
                Id: Id,
                RunId: RunId,
                Options: options));

        /// <summary>
        /// Request cancellation of this activity.
        /// </summary>
        /// <param name="options">Cancellation options.</param>
        /// <returns>Cancel accepted task.</returns>
        /// <exception cref="RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public virtual Task CancelAsync(ActivityCancelOptions? options = null) =>
            Client.OutboundInterceptor.CancelActivityAsync(new(
                Id: Id,
                RunId: RunId,
                Options: options));

        /// <summary>
        /// Terminate this activity.
        /// </summary>
        /// <param name="options">Termination options.</param>
        /// <returns>Terminate completed task.</returns>
        /// <exception cref="RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public virtual Task TerminateAsync(ActivityTerminateOptions? options = null) =>
            Client.OutboundInterceptor.TerminateActivityAsync(new(
                Id: Id,
                RunId: RunId,
                Options: options));
    }

    /// <summary>
    /// Handle for a standalone activity with a known result type.
    /// </summary>
    /// <typeparam name="TResult">Result type of the activity.</typeparam>
    /// <param name="Client">Client used for activity handle calls.</param>
    /// <param name="Id">Activity ID.</param>
    /// <param name="RunId">Activity run ID if known.</param>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public record ActivityHandle<TResult>(
        ITemporalClient Client,
        string Id,
        string? RunId = null) :
            ActivityHandle(Client, Id, RunId)
    {
        /// <summary>
        /// Wait for the result of the activity.
        /// </summary>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Result of the activity.</returns>
        /// <exception cref="ActivityFailedException">
        /// Exception thrown for unsuccessful activity result.
        /// </exception>
        /// <exception cref="RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public new Task<TResult> GetResultAsync(RpcOptions? rpcOptions = null) =>
            GetResultAsync<TResult>(rpcOptions);
    }
}
