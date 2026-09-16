#pragma warning disable SA1402 // We allow multiple types of the same name

using System;
using System.Threading.Tasks;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;

namespace Temporalio.Client
{
    /// <summary>
    /// Handle for a standalone Nexus operation to perform actions on.
    /// </summary>
    /// <param name="Client">Client used for operation handle calls.</param>
    /// <param name="Id">Operation ID.</param>
    /// <param name="RunId">Operation run ID if known.</param>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public record NexusOperationHandle(
        ITemporalClient Client,
        string Id,
        string? RunId = null)
    {
        /// <summary>
        /// Gets the Nexus endpoint the operation was started on, used with <see cref="Service"/>
        /// and <see cref="Operation"/> to decode the operation's result and failure the way they
        /// were encoded. Null for a handle obtained by operation ID, which never saw a start
        /// request and therefore decodes without a Nexus context.
        /// </summary>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public string? Endpoint { get; init; }

        /// <summary>
        /// Gets the Nexus service the operation was started on, or null. Set exactly when
        /// <see cref="Endpoint"/> is set.
        /// </summary>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public string? Service { get; init; }

        /// <summary>
        /// Gets the Nexus operation that was started, or null. Set exactly when
        /// <see cref="Endpoint"/> is set.
        /// </summary>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public string? Operation { get; init; }

        /// <summary>
        /// Gets the serialization context for this operation, or null when the operation is not
        /// identified.
        /// </summary>
        /// <remarks>
        /// These are init-only properties that can be set independently, so the all-or-nothing
        /// invariant is checked here rather than in a constructor. A partially identified operation
        /// would silently decode without a context, which for a converter that varies by context
        /// means reading the payload the wrong way rather than failing.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// If only some of <see cref="Endpoint"/>, <see cref="Service"/> and
        /// <see cref="Operation"/> are set.
        /// </exception>
        private ISerializationContext.Nexus? NexusSerializationContext =>
            (Endpoint, Service, Operation) switch
            {
                (null, null, null) => null,
                (string endpoint, string service, string operation) =>
                    new(endpoint, service, operation),
                _ => throw new InvalidOperationException(
                    "Endpoint, Service and Operation must all be set or all be null, got " +
                    $"Endpoint={Endpoint}, Service={Service}, Operation={Operation}"),
            };

        /// <summary>
        /// Wait for the result of the operation, discarding the return value.
        /// </summary>
        /// <param name="rpcOptions">RPC options for the call.</param>
        /// <returns>Task that completes when the operation completes.</returns>
        /// <exception cref="NexusOperationFailedException">
        /// Exception thrown for unsuccessful operation result.
        /// </exception>
        /// <exception cref="RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public Task GetResultAsync(RpcOptions? rpcOptions = null) =>
            GetResultAsync<ValueTuple>(rpcOptions);

        /// <summary>
        /// Wait for the result of the operation, deserializing into the given type.
        /// </summary>
        /// <typeparam name="TResult">Return type to deserialize result into.</typeparam>
        /// <param name="rpcOptions">RPC options for the call.</param>
        /// <returns>Result of the operation.</returns>
        /// <exception cref="NexusOperationFailedException">
        /// Exception thrown for unsuccessful operation result.
        /// </exception>
        /// <exception cref="RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public virtual async Task<TResult> GetResultAsync<TResult>(
            RpcOptions? rpcOptions = null)
        {
            // Decode the result and failure with the context the operation was started with, so
            // they are decoded the way they were encoded.
            var dataConverter = NexusSerializationContext is { } context ?
                Client.Options.DataConverter.WithSerializationContext(context) :
                Client.Options.DataConverter;

            // Continually poll until outcome is available
            var req = new PollNexusOperationExecutionRequest()
            {
                Namespace = Client.Options.Namespace,
                OperationId = Id,
                RunId = RunId ?? string.Empty,
                WaitStage = NexusOperationWaitStage.Closed,
            };
            var rpc = TemporalClient.DefaultRetryOptions(rpcOptions);
            while (true)
            {
                var resp = await Client.Connection.WorkflowService.PollNexusOperationExecutionAsync(
                    req, rpc).ConfigureAwait(false);

                // If no outcome, keep polling
                if (resp.OutcomeCase == PollNexusOperationExecutionResponse.OutcomeOneofCase.None)
                {
                    continue;
                }

                // Handle outcome
                switch (resp.OutcomeCase)
                {
                    case PollNexusOperationExecutionResponse.OutcomeOneofCase.Result:
                        // Use default if they are ignoring result or payload not present
                        if (typeof(TResult) == typeof(ValueTuple) || resp.Result == null)
                        {
                            return default!;
                        }
                        return await dataConverter.ToSingleValueAsync<TResult>(
                            new[] { resp.Result }).ConfigureAwait(false);
                    case PollNexusOperationExecutionResponse.OutcomeOneofCase.Failure:
                        var failure = await dataConverter.ToExceptionAsync(
                            resp.Failure).ConfigureAwait(false);
                        throw new NexusOperationFailedException(Id, resp.RunId, failure);
                    default:
                        throw new InvalidOperationException(
                            $"Unexpected Nexus operation outcome type: {resp.OutcomeCase}");
                }
            }
        }

        /// <summary>
        /// Describe this operation.
        /// </summary>
        /// <param name="options">Extra options.</param>
        /// <returns>Description for the operation.</returns>
        /// <exception cref="RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public virtual Task<NexusOperationExecutionDescription> DescribeAsync(
            NexusOperationDescribeOptions? options = null) =>
            Client.OutboundInterceptor.DescribeNexusOperationAsync(new(
                Id: Id,
                RunId: RunId,
                Options: options));

        /// <summary>
        /// Request cancellation of this operation.
        /// </summary>
        /// <param name="options">Cancellation options.</param>
        /// <returns>Cancel accepted task.</returns>
        /// <exception cref="RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public virtual Task CancelAsync(NexusOperationCancelOptions? options = null) =>
            Client.OutboundInterceptor.CancelNexusOperationAsync(new(
                Id: Id,
                RunId: RunId,
                Options: options));

        /// <summary>
        /// Terminate this operation.
        /// </summary>
        /// <param name="options">Termination options.</param>
        /// <returns>Terminate completed task.</returns>
        /// <exception cref="RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public virtual Task TerminateAsync(NexusOperationTerminateOptions? options = null) =>
            Client.OutboundInterceptor.TerminateNexusOperationAsync(new(
                Id: Id,
                RunId: RunId,
                Options: options));
    }

    /// <summary>
    /// Handle for a standalone Nexus operation with a known result type.
    /// </summary>
    /// <typeparam name="TResult">Result type of the operation.</typeparam>
    /// <param name="Client">Client used for operation handle calls.</param>
    /// <param name="Id">Operation ID.</param>
    /// <param name="RunId">Operation run ID if known.</param>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public record NexusOperationHandle<TResult>(
        ITemporalClient Client,
        string Id,
        string? RunId = null) :
            NexusOperationHandle(Client, Id, RunId)
    {
        /// <summary>
        /// Wait for the result of the operation.
        /// </summary>
        /// <param name="rpcOptions">RPC options for the call.</param>
        /// <returns>Result of the operation.</returns>
        /// <exception cref="NexusOperationFailedException">
        /// Exception thrown for unsuccessful operation result.
        /// </exception>
        /// <exception cref="RpcException">Server-side error.</exception>
        /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
        public new Task<TResult> GetResultAsync(RpcOptions? rpcOptions = null) =>
            GetResultAsync<TResult>(rpcOptions);
    }
}
