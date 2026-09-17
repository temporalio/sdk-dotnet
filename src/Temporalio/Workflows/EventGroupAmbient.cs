using System;
using System.Collections.Generic;
using System.Threading;
using Temporalio.Api.Sdk.V1;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Ambient Event Groups for the current workflow task, stored in <see cref="AsyncLocal{T}" />
    /// so nested awaits inherit the active set.
    /// </summary>
    internal static class EventGroupAmbient
    {
        private static readonly AsyncLocal<State?> Current = new();

        /// <summary>
        /// Gets the Event Groups active in the current asynchronous flow.
        /// </summary>
        internal static State CurrentState => Current.Value ?? new State();

        /// <summary>
        /// Push user-created Event Groups onto the explicit set, keeping any implicit inbound
        /// group. Nested scopes compose; the same ID overwrites.
        /// </summary>
        /// <param name="groups">Groups to add.</param>
        /// <returns>Scope that restores the previous ambient set on dispose.</returns>
        internal static EventGroupScope PushExplicit(IReadOnlyCollection<EventGroup> groups)
        {
            var previous = Current.Value;
            var previousState = previous ?? new State();
            var nextExplicit = new Dictionary<string, EventGroup.Explicit>(previousState.Explicit);
            foreach (var group in groups)
            {
                if (group == null)
                {
                    throw new ArgumentException("Event group cannot be null", nameof(groups));
                }
                var explicitGroup = (EventGroup.Explicit)group;
                nextExplicit[explicitGroup.Id] = explicitGroup;
            }
            Current.Value = new State
            {
                Implicit = previousState.Implicit,
                Explicit = nextExplicit,
            };
            return new EventGroupScope(() => Current.Value = previous);
        }

        /// <summary>
        /// Enter an isolating implicit inbound scope. A handler must not inherit an explicit scope
        /// that happened to be active at registration or dispatch.
        /// </summary>
        /// <param name="group">Implicit inbound group, or <see cref="EventGroup.Stub" />.
        /// </param>
        /// <returns>Scope that restores the previous ambient set on dispose.</returns>
        internal static EventGroupScope PushImplicit(EventGroup group)
        {
            var previous = Current.Value;
            Current.Value = new State
            {
                Implicit = group as EventGroup.Implicit,
                Explicit = new Dictionary<string, EventGroup.Explicit>(),
            };
            return new EventGroupScope(() => Current.Value = previous);
        }

        /// <summary>
        /// Snapshot ambient and directly attached Event Groups as command markers.
        /// </summary>
        /// <param name="directs">Directly attached groups, if any.</param>
        /// <returns>Markers to stamp on the command. Must be called from the request context.
        /// </returns>
        internal static IReadOnlyList<EventGroupMarker> CaptureMarkers(
            IReadOnlyCollection<EventGroup>? directs)
        {
            var state = CurrentState;
            var explicitGroups = state.Explicit;
            if (directs != null && directs.Count > 0)
            {
                explicitGroups = new Dictionary<string, EventGroup.Explicit>(state.Explicit);
                foreach (var group in directs)
                {
                    if (group == null)
                    {
                        throw new ArgumentException("Event group cannot be null", nameof(directs));
                    }
                    var explicitGroup = (EventGroup.Explicit)group;
                    explicitGroups[explicitGroup.Id] = explicitGroup;
                }
            }
            var markers = new List<EventGroupMarker>(
                explicitGroups.Count + (state.Implicit != null ? 1 : 0));
            if (state.Implicit != null)
            {
                markers.Add(state.Implicit.ToMarker());
            }
            foreach (var group in explicitGroups.Values)
            {
                markers.Add(group.ToMarker());
            }
            return markers;
        }

        /// <summary>
        /// Ambient Event Groups for one asynchronous flow.
        /// </summary>
        internal sealed class State
        {
            /// <summary>
            /// Gets the implicit inbound signal or update group, if any.
            /// </summary>
            internal EventGroup.Implicit? Implicit { get; init; }

            /// <summary>
            /// Gets user-created groups keyed by ID. Later scopes overwrite the same ID.
            /// </summary>
            internal Dictionary<string, EventGroup.Explicit> Explicit { get; init; } =
                new Dictionary<string, EventGroup.Explicit>();
        }
    }
}
