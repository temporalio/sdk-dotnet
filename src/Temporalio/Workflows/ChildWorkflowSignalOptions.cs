using System;
using System.Collections.Generic;
using System.Threading;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for child workflow signalling.
    /// </summary>
    public class ChildWorkflowSignalOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the cancellation token to cancel the signal request. If the signal is
        /// already sent, this does nothing. If unset, this defaults to the workflow cancellation
        /// token.
        /// </summary>
        public CancellationToken? CancellationToken { get; set; }

        /// <summary>
        /// Gets or sets Event Groups to attach to this command, in addition to those active in the
        /// current <see cref="Workflow.WithEventGroups" /> scope. Markers describe the signal event
        /// in this workflow and do not propagate to the child.
        /// </summary>
        /// <remarks>WARNING: Event Groups are experimental.</remarks>
        public IReadOnlyCollection<EventGroup>? EventGroups { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}