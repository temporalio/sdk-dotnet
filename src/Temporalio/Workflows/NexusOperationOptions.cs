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
        public TimeSpan? ScheduleToCloseTimeout { get; set; }

        /// <summary>
        /// Gets or sets the summary.
        /// </summary>
        public string? Summary { get; set; }

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