using System;
using Microsoft.Extensions.Logging;
using Temporalio.Api.Sdk.V1;
using Temporalio.Converters;

namespace Temporalio.Workflows
{
    /// <summary>
    /// A discrete token associating workflow commands, and the history events they produce, with a
    /// logical group for UI and observability purposes.
    /// </summary>
    /// <remarks>
    /// Create instances with <see cref="Workflow.CreateEventGroup" />. Attach them to
    /// specific commands via options <c>EventGroups</c> properties, or to every command produced
    /// within a block of workflow code via <see cref="Workflow.WithEventGroups" />.
    /// </remarks>
    /// <remarks>WARNING: Event Groups are experimental.</remarks>
    public abstract class EventGroup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventGroup"/> class.
        /// </summary>
        internal EventGroup()
        {
        }

        /// <summary>
        /// Create an explicit Event Group.
        /// </summary>
        /// <param name="id">Group identity.</param>
        /// <param name="label">Optional display label.</param>
        /// <returns>The Event Group.</returns>
        internal static EventGroup Create(string id, string? label) => new Explicit(id, label);

        /// <summary>
        /// Create the implicit Event Group for an inbound signal.
        /// </summary>
        /// <param name="eventId">Originating signaled event ID.</param>
        /// <returns>The Event Group.</returns>
        internal static EventGroup ForInboundEvent(long eventId)
        {
            if (eventId <= 0)
            {
                Workflow.Logger.LogWarning(
                    "Cannot create implicit Event Group for signal with invalid originating event ID: {EventId}",
                    eventId);
                return new Stub();
            }
            return new Implicit(new EventGroupMarker
            {
                InboundEvent = new EventGroupMarker.Types.InboundEvent
                {
                    InboundEventId = eventId,
                },
            });
        }

        /// <summary>
        /// Create the implicit Event Group for an inbound update.
        /// </summary>
        /// <param name="updateId">Update ID.</param>
        /// <returns>The Event Group.</returns>
        internal static EventGroup ForInboundUpdate(string updateId) =>
            new Implicit(new EventGroupMarker
            {
                InboundUpdate = new EventGroupMarker.Types.InboundUpdate
                {
                    InboundUpdateId = updateId,
                },
            });

        /// <summary>
        /// Serialize as a command marker.
        /// </summary>
        /// <returns>The marker.</returns>
        internal abstract EventGroupMarker ToMarker();

        /// <summary>
        /// Explicit Event Group created by workflow code.
        /// </summary>
        internal sealed class Explicit : EventGroup
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Explicit"/> class.
            /// </summary>
            /// <param name="id">Group identity.</param>
            /// <param name="label">Optional display label.</param>
            internal Explicit(string id, string? label)
            {
                if (string.IsNullOrEmpty(id))
                {
                    throw new ArgumentException("Event group id cannot be empty", nameof(id));
                }
                if (label != null && label.Length == 0)
                {
                    throw new ArgumentException("Event group label cannot be empty", nameof(label));
                }
                Id = id;
                Label = label;
            }

            /// <summary>
            /// Gets the ID.
            /// </summary>
            internal string Id { get; }

            /// <summary>
            /// Gets the label.
            /// </summary>
            internal string? Label { get; }

            /// <inheritdoc />
            internal override EventGroupMarker ToMarker()
            {
                var label = new EventGroupMarker.Types.Label { Id = Id };
                if (Label != null)
                {
                    // Deliberately the SDK's default converter, not the worker-configured one.
                    label.Label_ = DataConverter.Default.PayloadConverter.ToPayload(Label);
                }
                return new EventGroupMarker { Label = label };
            }
        }

        /// <summary>
        /// Implicit Event Group for an inbound signal or update.
        /// </summary>
        internal sealed class Implicit : EventGroup
        {
            private readonly EventGroupMarker marker;

            /// <summary>
            /// Initializes a new instance of the <see cref="Implicit"/> class.
            /// </summary>
            /// <param name="marker">Inbound marker.</param>
            internal Implicit(EventGroupMarker marker) => this.marker = marker;

            /// <inheritdoc />
            internal override EventGroupMarker ToMarker() => marker.Clone();
        }

        /// <summary>
        /// No-op implicit group. It should theoretically never be required, but is used as a safe
        /// null-object fallback in the unexpected case that we're handed out an invalid inbound
        /// event ID.
        /// </summary>
        internal sealed class Stub : EventGroup
        {
            /// <inheritdoc />
            internal override EventGroupMarker ToMarker() =>
                // Never collected: PushImplicit does not install this as the active implicit group.
                new();
        }
    }
}
