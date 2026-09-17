using System;
using System.Collections.Generic;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for <see cref="Workflow.UpsertTypedSearchAttributesWithOptions" />.
    /// </summary>
    /// <remarks>WARNING: This API is experimental.</remarks>
    public class UpsertSearchAttributesOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpsertSearchAttributesOptions"/> class.
        /// </summary>
        public UpsertSearchAttributesOptions()
            : this(Array.Empty<SearchAttributeUpdate>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpsertSearchAttributesOptions"/> class.
        /// </summary>
        /// <param name="updates">See <see cref="Updates" />.</param>
        public UpsertSearchAttributesOptions(params SearchAttributeUpdate[] updates) =>
            Updates = updates;

        /// <summary>
        /// Gets or sets the search-attribute updates to issue.
        /// </summary>
        public IReadOnlyCollection<SearchAttributeUpdate> Updates { get; set; } =
            Array.Empty<SearchAttributeUpdate>();

        /// <summary>
        /// Gets or sets Event Groups to attach to the search-attribute upsert, in addition to those
        /// active in the current <see cref="Workflow.WithEventGroups" /> scope.
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
