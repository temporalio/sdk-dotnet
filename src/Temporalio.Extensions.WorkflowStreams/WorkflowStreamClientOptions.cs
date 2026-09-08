using System;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>Options for constructing a <see cref="WorkflowStreamClient"/>.</summary>
    /// <remarks>WARNING: Workflow Streams is experimental and may change.</remarks>
    public class WorkflowStreamClientOptions : ICloneable
    {
        /// <summary>Gets or sets the interval between automatic flush attempts.</summary>
        public TimeSpan BatchInterval { get; set; } = WorkflowStreamConstants.DefaultBatchInterval;

        /// <summary>
        /// Gets or sets the item count that triggers a flush. Zero disables size-based flushing.
        /// </summary>
        public int MaxBatchSize { get; set; }

        /// <summary>Gets or sets how long an ambiguous batch is retained for retry.</summary>
        public TimeSpan MaxRetryDuration { get; set; } =
            WorkflowStreamConstants.DefaultMaxRetryDuration;

        /// <summary>Gets or sets the test override for the bounded transport attempt.</summary>
        internal TimeSpan RpcTimeout { get; set; } = WorkflowStreamConstants.DefaultRpcTimeout;

        /// <summary>Creates a copy of these options.</summary>
        /// <returns>A copied options instance.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}
