using System;
using System.Collections.Generic;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for <see cref="Workflow.DeprecatePatch(string, DeprecatePatchOptions)" />.
    /// </summary>
    /// <remarks>WARNING: This API is experimental.</remarks>
    public class DeprecatePatchOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets Event Groups to attach to the patch marker, in addition to those active in
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
