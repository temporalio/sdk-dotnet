using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for <see cref="Workflow.CreateEventGroup" />.
    /// </summary>
    /// <remarks>WARNING: Event Groups are experimental.</remarks>
    public class EventGroupOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets optional non-empty display text for the UI / CLI. When set, it is persisted
        /// to history as a codec-encoded payload. The group ID remains plaintext.
        /// </summary>
        /// <remarks>WARNING: Event Groups are experimental.</remarks>
        public string? Label { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}
