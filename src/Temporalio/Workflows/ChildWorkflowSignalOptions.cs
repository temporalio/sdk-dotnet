using System;
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
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}