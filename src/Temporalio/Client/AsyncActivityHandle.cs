using System;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    /// <summary>
    /// Handle to perform activity actions for activities that will complete asynchronously.
    /// </summary>
    /// <param name="Client">Client used for async activity handle calls.</param>
    /// <param name="Activity">Reference to the activity for this handle.</param>
    public record AsyncActivityHandle(
        ITemporalClient Client, AsyncActivityHandle.Reference Activity)
    {
        /// <summary>
        /// Issue a heartbeat for this activity.
        /// </summary>
        /// <param name="options">Heartbeat options.</param>
        /// <returns>Completion task.</returns>
        /// <exception cref="Exceptions.AsyncActivityCancelledException">
        /// If the server has requested that this activity be cancelled. Users should catch this and
        /// invoke <see cref="ReportCancellationAsync" /> for proper behavior.
        /// </exception>
        public Task HeartbeatAsync(AsyncActivityHeartbeatOptions? options = null) =>
            Client.OutboundInterceptor.HeartbeatAsyncActivityAsync(new(
                Activity: Activity, Options: options));

        /// <summary>
        /// Complete this activity.
        /// </summary>
        /// <param name="result">Result of the activity.</param>
        /// <param name="options">Completion options.</param>
        /// <returns>Completion task.</returns>
        public Task CompleteAsync(
            object? result = null, AsyncActivityCompleteOptions? options = null) =>
            Client.OutboundInterceptor.CompleteAsyncActivityAsync(new(
                Activity: Activity, Result: result, Options: options));

        /// <summary>
        /// Fail this activity.
        /// </summary>
        /// <param name="exception">Exception for the activity.</param>
        /// <param name="options">Fail options.</param>
        /// <returns>Completion task.</returns>
        public Task FailAsync(Exception exception, AsyncActivityFailOptions? options = null) =>
            Client.OutboundInterceptor.FailAsyncActivityAsync(new(
                Activity: Activity, Exception: exception, Options: options));

        /// <summary>
        /// Report this activity as cancelled..
        /// </summary>
        /// <param name="options">Cancel options.</param>
        /// <returns>Completion task.</returns>
        public Task ReportCancellationAsync(
            AsyncActivityReportCancellationOptions? options = null) =>
            Client.OutboundInterceptor.ReportCancellationAsyncActivityAsync(new(
                Activity: Activity, Options: options));

        /// <summary>
        /// Reference to an existing activity.
        /// </summary>
        public abstract record Reference
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Reference"/> class.
            /// </summary>
            private protected Reference()
            {
            }
        }

        /// <summary>
        /// Reference to an activity by its workflow ID, workflow run ID, and activity ID.
        /// </summary>
        /// <param name="WorkflowId">ID for the activity's workflow.</param>
        /// <param name="RunId">Run ID for the activity's workflow.</param>
        /// <param name="ActivityId">ID for the activity.</param>
        public record IdReference(
            string WorkflowId, string? RunId, string ActivityId) : Reference;

        /// <summary>
        /// Reference to an activity by its task token.
        /// </summary>
        /// <param name="TaskToken">Task token for the activity.</param>
        public record TaskTokenReference(byte[] TaskToken) : Reference;
    }
}
