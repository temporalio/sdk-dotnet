using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Temporalio.Api.Sdk.V1;
using Temporalio.Common;

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
        internal static State CurrentState => Current.Value ?? State.Empty;

        /// <summary>
        /// Push user-created Event Groups onto the explicit set, keeping any implicit inbound
        /// group. Nested scopes compose; the same ID overwrites.
        /// </summary>
        /// <param name="groups">Groups to add.</param>
        /// <returns>Scope that restores the previous ambient set on dispose.</returns>
        internal static EventGroupScope PushExplicit(IReadOnlyCollection<EventGroup> groups)
        {
            var previous = CurrentState;
            var nextExplicit = new Dictionary<string, EventGroup>(previous.Explicit.Count + groups.Count);
            foreach (var entry in previous.Explicit)
            {
                nextExplicit.Add(entry.Key, entry.Value);
            }
            foreach (var group in groups)
            {
                if (group == null)
                {
                    throw new ArgumentException("Event group cannot be null", nameof(groups));
                }
                nextExplicit[group.Id] = group;
            }
            var installed = new State
            {
                Implicit = previous.Implicit,
                Explicit = nextExplicit,
            };
            Current.Value = installed;
            return new EventGroupScope(installed, previous);
        }

        /// <summary>
        /// Enter an isolating implicit inbound scope. A handler must not inherit an explicit scope
        /// that happened to be active at registration or dispatch.
        /// </summary>
        /// <param name="group">Implicit inbound group.</param>
        /// <returns>Scope that restores the previous ambient set on dispose.</returns>
        internal static EventGroupScope PushImplicit(EventGroup group)
        {
            var previous = CurrentState;
            var installed = new State
            {
                Implicit = group,
                Explicit = State.Empty.Explicit,
            };
            Current.Value = installed;
            return new EventGroupScope(installed, previous);
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
            IReadOnlyDictionary<string, EventGroup> mergedGroups;
            if (directs == null || directs.Count == 0)
            {
                mergedGroups = state.Explicit;
            }
            else if (state.Explicit.Count == 0 && directs.Count == 1)
            {
                // One direct group and no ambient explicit groups needs no merge dictionary.
                var group = directs.Single();
                if (group == null)
                {
                    throw new ArgumentException("Event group cannot be null", nameof(directs));
                }
                return state.Implicit == null
                    ? new[] { group.ToMarker() }
                    : new[] { state.Implicit.ToMarker(), group.ToMarker() };
            }
            else
            {
                var merged = new Dictionary<string, EventGroup>(state.Explicit.Count + directs.Count);
                foreach (var entry in state.Explicit)
                {
                    merged.Add(entry.Key, entry.Value);
                }
                foreach (var group in directs)
                {
                    if (group == null)
                    {
                        throw new ArgumentException("Event group cannot be null", nameof(directs));
                    }
                    merged[group.Id] = group;
                }
                mergedGroups = merged;
            }

            var count = mergedGroups.Count + (state.Implicit != null ? 1 : 0);
            if (count == 0)
            {
                return Array.Empty<EventGroupMarker>();
            }

            var markers = new EventGroupMarker[count];
            var index = 0;
            if (state.Implicit != null)
            {
                markers[index++] = state.Implicit.ToMarker();
            }
            foreach (var group in mergedGroups.Values)
            {
                markers[index++] = group.ToMarker();
            }
            return markers;
        }

        /// <summary>
        /// Restore a scope only when it is still the active one on this flow. An earlier scope is
        /// still referenced by the nested scope that replaced it, so restoring it would drop that
        /// nested scope.
        /// </summary>
        /// <param name="installed">State the disposing scope installed.</param>
        /// <param name="previous">State to restore.</param>
        internal static void Restore(State installed, State previous)
        {
            if (!ReferenceEquals(Current.Value, installed))
            {
                throw new InvalidOperationException(
                    "Event group scope is not the active scope. Dispose nested scopes first.");
            }
            Current.Value = (previous == State.Empty) ? null : previous;
        }

        /// <summary>
        /// Ambient Event Groups for one asynchronous flow.
        /// </summary>
        internal sealed class State
        {
            /// <summary>
            /// Shared empty state.
            /// </summary>
            public static readonly State Empty = new();

            /// <summary>
            /// Gets the implicit inbound signal or update group, if any.
            /// </summary>
            internal EventGroup? Implicit { get; init; }

            /// <summary>
            /// Gets user-created groups keyed by ID. Later scopes overwrite the same ID.
            /// </summary>
            internal IReadOnlyDictionary<string, EventGroup> Explicit { get; init; } =
                EmptyReadOnlyDictionary<string, EventGroup>.Value;
        }
    }
}
