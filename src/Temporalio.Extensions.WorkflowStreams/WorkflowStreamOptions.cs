using System;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>Options for constructing a workflow-side stream.</summary>
    /// <remarks>WARNING: Workflow Streams is experimental and may change.</remarks>
    public class WorkflowStreamOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets how long publisher deduplication state is retained in snapshots.
        /// </summary>
        public TimeSpan PublisherTtl { get; set; } = WorkflowStreamConstants.DefaultPublisherTtl;

        /// <summary>Creates a copy of these options.</summary>
        /// <returns>A copied options instance.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}
