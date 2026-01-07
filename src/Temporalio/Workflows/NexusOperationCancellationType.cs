namespace Temporalio.Workflows
{
    /// <summary>
    /// How a Nexus operation cancellation is treated by the workflow.
    /// </summary>
    public enum NexusOperationCancellationType
    {
        /// <summary>
        /// Wait for operation cancellation completion.
        /// </summary>
        WaitCancellationCompleted = Bridge.Api.Nexus.NexusOperationCancellationType.WaitCancellationCompleted,

        /// <summary>
        /// Do not request cancellation of the nexus operation if already scheduled.
        /// </summary>
        Abandon = Bridge.Api.Nexus.NexusOperationCancellationType.Abandon,

        /// <summary>
        /// Initiate a cancellation request for the Nexus operation and immediately report
        /// cancellation to the caller. Note that it doesn't guarantee that cancellation is
        /// delivered to the operation if calling workflow exits before the delivery is done. If you
        /// want to ensure that cancellation is delivered to the operation, use
        /// <see cref="WaitCancellationRequested"/>.
        /// </summary>
        TryCancel = Bridge.Api.Nexus.NexusOperationCancellationType.TryCancel,

        /// <summary>
        /// Request cancellation of the operation and wait for confirmation that the request was
        /// received.
        /// </summary>
        WaitCancellationRequested = Bridge.Api.Nexus.NexusOperationCancellationType.WaitCancellationRequested,
    }
}