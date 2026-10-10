using System;
using Temporalio.Api.Sdk.V1;
using Temporalio.Converters;

namespace Temporalio.Workflows
{
    /// <summary>
    /// A discrete token associating workflow commands, and the history events they produce, with a
    /// logical group for UI and observability purposes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Create instances with <see cref="Workflow.CreateEventGroup" />. Attach them to
    /// specific commands via options <c>EventGroups</c> properties, or to every command produced
    /// within a block of workflow code via <see cref="Workflow.WithEventGroups" />.
    /// </para>
    /// <para>WARNING: Event Groups are experimental.</para>
    /// </remarks>
    public sealed class EventGroup
    {
        private readonly EventGroupMarker marker;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventGroup"/> class.
        /// </summary>
        /// <param name="marker">Marker copied by <see cref="ToMarker" />.</param>
        /// <param name="id">Group identity.</param>
        private EventGroup(EventGroupMarker marker, string id)
        {
            this.marker = marker;
            Id = id;
        }

        /// <summary>
        /// Gets the group identity.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Create an explicit Event Group.
        /// </summary>
        /// <param name="id">Group identity.</param>
        /// <param name="label">Optional display label.</param>
        /// <returns>The Event Group.</returns>
        internal static EventGroup CreateExplicit(string id, string? label)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Event group id cannot be empty", nameof(id));
            }
            if (label != null && label.Length == 0)
            {
                throw new ArgumentException("Event group label cannot be empty", nameof(label));
            }
            var markerLabel = new EventGroupMarker.Types.Label { Id = id };
            if (label != null)
            {
                // Deliberately the SDK's default converter, not the worker-configured one.
                markerLabel.Label_ = DataConverter.Default.PayloadConverter.ToPayload(label);
            }
            return new EventGroup(new EventGroupMarker { Label = markerLabel }, id);
        }

        /// <summary>
        /// Create the implicit Event Group for an inbound signal.
        /// </summary>
        /// <param name="eventId">Originating signaled event ID.</param>
        /// <returns>The Event Group.</returns>
        internal static EventGroup CreateInboundEvent(long eventId) =>
            new(
                new EventGroupMarker
                {
                    InboundEvent = new EventGroupMarker.Types.InboundEvent
                    {
                        InboundEventId = eventId,
                    },
                },
                $"inbound-event:{eventId}");

        /// <summary>
        /// Create the implicit Event Group for an inbound update.
        /// </summary>
        /// <param name="updateId">Update ID.</param>
        /// <returns>The Event Group.</returns>
        internal static EventGroup CreateInboundUpdate(string updateId) =>
            new(
                new EventGroupMarker
                {
                    InboundUpdate = new EventGroupMarker.Types.InboundUpdate
                    {
                        InboundUpdateId = updateId,
                    },
                },
                $"inbound-update:{updateId}");

        /// <summary>
        /// Gets the marker stored at construction.
        /// </summary>
        /// <returns>The marker.</returns>
        internal EventGroupMarker ToMarker() => marker;
    }
}
