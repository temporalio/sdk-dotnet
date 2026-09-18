using System;
using System.Collections.Generic;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for <see cref="Workflow.UpsertMemoWithOptions" />.
    /// </summary>
    /// <remarks>WARNING: This API is experimental.</remarks>
    public class UpsertMemoOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpsertMemoOptions"/> class.
        /// </summary>
        public UpsertMemoOptions()
            : this(Array.Empty<MemoUpdate>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpsertMemoOptions"/> class.
        /// </summary>
        /// <param name="updates">See <see cref="Updates" />.</param>
        public UpsertMemoOptions(params MemoUpdate[] updates) => Updates = updates;

        /// <summary>
        /// Gets or sets the memo updates to issue.
        /// </summary>
        public IReadOnlyCollection<MemoUpdate> Updates { get; set; } = Array.Empty<MemoUpdate>();

        /// <summary>
        /// Gets or sets Event Groups to attach to the memo upsert, in addition to those active in
        /// the current <see cref="Workflow.WithEventGroups" /> scope.
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
