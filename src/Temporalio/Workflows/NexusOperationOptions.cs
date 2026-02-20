using System;
using System.Threading;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for starting a Nexus operation.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class NexusOperationOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the schedule to close timeout.
        /// </summary>
        /// <remarks>Indicates how long the caller is willing to wait for operation completion.
        /// Calls are retried internally by the server.</remarks>
        public TimeSpan? ScheduleToCloseTimeout { get; set; }

        /// <summary>
        /// Gets or sets the schedule to start timeout.
        /// </summary>
        /// <remarks>Indicates how long the caller is willing to wait for the operation to be started (or completed if synchronous)
        /// by the handler. If the operation is not started within this timeout, it will fail with TIMEOUT_TYPE_SCHEDULE_TO_START.
        /// If not set or zero, no schedule-to-start timeout is enforced.
        /// Requires server version 1.31.0 or later.</remarks>
        public TimeSpan? ScheduleToStartTimeout { get; set; }

        /// <summary>
        /// Gets or sets the start to close timeout.
        /// </summary>
        /// <remarks>Indicates how long the caller is willing to wait for an asynchronous operation to complete after it has been
        /// started. If the operation does not complete within this timeout after starting, it will fail with TIMEOUT_TYPE_START_TO_CLOSE.
        /// Only applies to asynchronous operations. Synchronous operations ignore this timeout.
        /// If not set or zero, no start-to-close timeout is enforced.
        /// Requires server version 1.31.0 or later.</remarks>
        public TimeSpan? StartToCloseTimeout { get; set; }

        /// <summary>
        /// Gets or sets the summary.
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Gets or sets how the workflow will send/wait for cancellation of the Nexus operation.
        /// Default is <see cref="NexusOperationCancellationType.WaitCancellationCompleted" />.
        /// </summary>
        public NexusOperationCancellationType CancellationType { get; set; } =
            NexusOperationCancellationType.WaitCancellationCompleted;

        /// <summary>
        /// Gets or sets the cancellation token. If unset, defaults to the workflow cancellation
        /// token.
        /// </summary>
        public CancellationToken? CancellationToken { get; set; }

        // TODO(cretz): Cancellation type - https://github.com/temporalio/sdk-dotnet/issues/514

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}