using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Google.Protobuf.Reflection;
using NexusRpc;
using Temporalio.Api.Enums.V1;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Helpers for Nexus links.
    /// </summary>
    internal static class ProtoLinkExtensions
    {
        // Need to map PascalCase enum names and original enum names to the event type
        private static readonly Dictionary<string, EventType> StringToEventType =
            Enum.GetValues(typeof(EventType)).
                Cast<EventType>().
                SelectMany(e =>
                    new[]
                    {
                        (e.ToString(), e),
                        (typeof(EventType).GetField(e.ToString())!.
                            GetCustomAttribute<OriginalNameAttribute>()!.Name, e),
                    }).
                ToDictionary(kv => kv.Item1, kv => kv.e);

        private static readonly EnumDescriptor EventTypeDescriptor =
            EventTypeReflection.Descriptor.FindTypeByName<EnumDescriptor>("EventType");

        private static readonly char[] QuerySeparator = new[] { '&' };
        private static readonly char[] QueryValueSeparator = new[] { '=' };

        /// <summary>
        /// Convert a workflow event to a Nexus link.
        /// </summary>
        /// <param name="evt">Event to convert.</param>
        /// <returns>Nexus link.</returns>
        public static NexusLink ToNexusLink(this Api.Common.V1.Link.Types.WorkflowEvent evt)
        {
            // Set some query params
            var queryParams = new Dictionary<string, string>();
            if (evt.EventRef is { } evtRef)
            {
                queryParams["referenceType"] = "EventReference";
                queryParams["eventType"] = EventTypeDescriptor.FindValueByNumber((int)evtRef.EventType).Name;
                if (evtRef.EventId > 0)
                {
                    queryParams["eventID"] = evtRef.EventId.ToString();
                }
            }
            else if (evt.RequestIdRef is { } reqIdRef)
            {
                queryParams["referenceType"] = "RequestIdReference";
                queryParams["eventType"] = EventTypeDescriptor.FindValueByNumber((int)reqIdRef.EventType).Name;
                queryParams["requestID"] = reqIdRef.RequestId;
            }

            // Build URI with empty authority so there is no host. UriBuilder cannot be used
            // here because even with Host explicitly set to "", it emits "temporal:/path"
            // (single slash) rather than the canonical "temporal:///path" form other SDKs use.
            var uriStr = "temporal:///namespaces/" + Uri.EscapeDataString(evt.Namespace) +
                "/workflows/" + Uri.EscapeDataString(evt.WorkflowId) + "/" +
                Uri.EscapeDataString(evt.RunId) + "/history";
            if (queryParams.Count > 0)
            {
                uriStr += "?" + string.Join("&", queryParams.Select(kvp =>
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            }
            return new(new Uri(uriStr), Api.Common.V1.Link.Types.WorkflowEvent.Descriptor.FullName);
        }

        /// <summary>
        /// Convert a Nexus operation link to a proto Link.
        /// </summary>
        /// <param name="nexusOp">Nexus operation to convert.</param>
        /// <returns>Nexus link.</returns>
        public static NexusLink ToNexusLink(this Api.Common.V1.Link.Types.NexusOperation nexusOp)
        {
            // Build URI with empty authority so there is no host. UriBuilder cannot be used
            // here because even with Host explicitly set to "", it emits "temporal:/path"
            // (single slash) rather than the canonical "temporal:///path" form other SDKs use.
            var uriStr = "temporal:///namespaces/" + Uri.EscapeDataString(nexusOp.Namespace) +
                "/nexus-operations/" + Uri.EscapeDataString(nexusOp.OperationId) +
                "/" + Uri.EscapeDataString(nexusOp.RunId) + "/details";
            return new(new Uri(uriStr), Api.Common.V1.Link.Types.NexusOperation.Descriptor.FullName);
        }

        /// <summary>
        /// Convert an activity link to a Nexus link.
        /// </summary>
        /// <param name="act">Activity link to convert.</param>
        /// <returns>Nexus link.</returns>
        public static NexusLink ToNexusLink(this Api.Common.V1.Link.Types.Activity act)
        {
            // Build URI with empty authority so there is no host. UriBuilder cannot be used
            // here because even with Host explicitly set to "", it emits "temporal:/path"
            // (single slash) rather than the canonical "temporal:///path" form other SDKs use.
            var uriStr = "temporal:///namespaces/" + Uri.EscapeDataString(act.Namespace) +
                "/activities/" + Uri.EscapeDataString(act.ActivityId) +
                "/" + Uri.EscapeDataString(act.RunId) + "/details";
            return new(new Uri(uriStr), Api.Common.V1.Link.Types.Activity.Descriptor.FullName);
        }

        /// <summary>
        /// Convert a proto Link to a Nexus link, dispatching on the populated oneof variant. Handles
        /// the workflow-event, nexus-operation, workflow, and activity variants. Returns <c>null</c>
        /// when no variant is set (e.g. a rejected update that has no history event to link to), so
        /// callers can skip it rather than dereferencing an unset variant. Throws for a
        /// set-but-unrecognized variant.
        /// </summary>
        /// <param name="link">Proto link.</param>
        /// <returns>Nexus link, or <c>null</c> if no variant is set.</returns>
        /// <exception cref="ArgumentException">If the link variant is set but unrecognized.</exception>
        public static NexusLink? ToNexusLink(this Api.Common.V1.Link link) => link.VariantCase switch
        {
            Api.Common.V1.Link.VariantOneofCase.WorkflowEvent => link.WorkflowEvent.ToNexusLink(),
            Api.Common.V1.Link.VariantOneofCase.NexusOperation => link.NexusOperation.ToNexusLink(),
            Api.Common.V1.Link.VariantOneofCase.Workflow => link.Workflow.ToNexusLink(),
            Api.Common.V1.Link.VariantOneofCase.Activity => link.Activity.ToNexusLink(),
            Api.Common.V1.Link.VariantOneofCase.None => null,
            _ => throw new ArgumentException($"Unknown link variant: {link.VariantCase}"),
        };

        /// <summary>
        /// Convert a Nexus link to a proto Link, dispatching on the link's type.
        /// </summary>
        /// <param name="link">Nexus link.</param>
        /// <returns>Proto link with the appropriate oneof variant populated.</returns>
        /// <exception cref="ArgumentException">If the link type is unknown or the link is invalid.</exception>
        public static Api.Common.V1.Link ToProtoLink(this NexusLink link) => link.Type switch
        {
            var t when t == Api.Common.V1.Link.Types.WorkflowEvent.Descriptor.FullName =>
                new Api.Common.V1.Link { WorkflowEvent = link.ToWorkflowEvent() },
            var t when t == Api.Common.V1.Link.Types.NexusOperation.Descriptor.FullName =>
                new Api.Common.V1.Link { NexusOperation = link.ToNexusOperation() },
            var t when t == Api.Common.V1.Link.Types.Activity.Descriptor.FullName =>
                new Api.Common.V1.Link { Activity = link.ToActivity() },
            var t when t == Api.Common.V1.Link.Types.Workflow.Descriptor.FullName =>
                new Api.Common.V1.Link { Workflow = link.ToWorkflow() },
            _ => throw new ArgumentException($"Unknown link type: {link.Type}"),
        };

        /// <summary>
        /// Convert a Nexus link to a nexus operation link.
        /// </summary>
        /// <param name="link">Nexus link.</param>
        /// <returns>Nexus operation link.</returns>
        /// <exception cref="ArgumentException">If the link is invalid.</exception>
        public static Api.Common.V1.Link.Types.NexusOperation ToNexusOperation(this NexusLink link)
        {
            var pathPieces = ParseTemporalLinkPath(link, "nexus-operations", "details");
            return new Api.Common.V1.Link.Types.NexusOperation
            {
                Namespace = Uri.UnescapeDataString(pathPieces[1]),
                OperationId = Uri.UnescapeDataString(pathPieces[3]),
                RunId = Uri.UnescapeDataString(pathPieces[4]),
            };
        }

        /// <summary>
        /// Convert a workflow link to a Nexus link. A workflow link addresses a workflow execution
        /// as a whole rather than one event within it, so the URL carries no event path suffix and
        /// no reference query params. It is used when there is no history event to point at, for
        /// example a query or a rejected update. The optional reason explaining why the link exists
        /// is carried as a query param.
        /// </summary>
        /// <param name="workflow">Workflow link to convert.</param>
        /// <returns>Nexus link.</returns>
        public static NexusLink ToNexusLink(this Api.Common.V1.Link.Types.Workflow workflow)
        {
            // Build URI with empty authority so there is no host. UriBuilder cannot be used
            // here because even with Host explicitly set to "", it emits "temporal:/path"
            // (single slash) rather than the canonical "temporal:///path" form other SDKs use.
            var uriStr = "temporal:///namespaces/" + Uri.EscapeDataString(workflow.Namespace) +
                "/workflows/" + Uri.EscapeDataString(workflow.WorkflowId) + "/" +
                Uri.EscapeDataString(workflow.RunId);
            if (workflow.Reason.Length > 0)
            {
                uriStr += "?reason=" + Uri.EscapeDataString(workflow.Reason);
            }
            return new(new Uri(uriStr), Api.Common.V1.Link.Types.Workflow.Descriptor.FullName);
        }

        /// <summary>
        /// Convert a Nexus link to a workflow link. The run ID ends a workflow link, so anything
        /// trailing is rejected. In particular this rejects the workflow-event form, which ends in
        /// "history" and is otherwise identical.
        /// </summary>
        /// <param name="link">Nexus link.</param>
        /// <returns>Workflow link.</returns>
        /// <exception cref="ArgumentException">If the link is invalid.</exception>
        public static Api.Common.V1.Link.Types.Workflow ToWorkflow(this NexusLink link)
        {
            var pathPieces = ParseTemporalLinkPath(link, "workflows", null);
            var workflow = new Api.Common.V1.Link.Types.Workflow
            {
                Namespace = Uri.UnescapeDataString(pathPieces[1]),
                WorkflowId = Uri.UnescapeDataString(pathPieces[3]),
                RunId = Uri.UnescapeDataString(pathPieces[4]),
            };
            if (ParseQueryParams(link.Uri).TryGetValue("reason", out var reason))
            {
                workflow.Reason = reason;
            }
            return workflow;
        }

        /// <summary>
        /// Convert a Nexus link to an activity link.
        /// </summary>
        /// <param name="link">Nexus link.</param>
        /// <returns>Activity link.</returns>
        /// <exception cref="ArgumentException">If the link is invalid.</exception>
        public static Api.Common.V1.Link.Types.Activity ToActivity(this NexusLink link)
        {
            var pathPieces = ParseTemporalLinkPath(link, "activities", "details");
            return new Api.Common.V1.Link.Types.Activity
            {
                Namespace = Uri.UnescapeDataString(pathPieces[1]),
                ActivityId = Uri.UnescapeDataString(pathPieces[3]),
                RunId = Uri.UnescapeDataString(pathPieces[4]),
            };
        }

        /// <summary>
        /// Convert a Nexus link to a workflow event.
        /// </summary>
        /// <param name="link">Nexus link.</param>
        /// <returns>Workflow event.</returns>
        /// <exception cref="ArgumentException">If the link is invalid.</exception>
        public static Api.Common.V1.Link.Types.WorkflowEvent ToWorkflowEvent(this NexusLink link)
        {
            var pathPieces = ParseTemporalLinkPath(link, "workflows", "history");
            var evt = new Api.Common.V1.Link.Types.WorkflowEvent
            {
                Namespace = Uri.UnescapeDataString(pathPieces[1]),
                WorkflowId = Uri.UnescapeDataString(pathPieces[3]),
                RunId = Uri.UnescapeDataString(pathPieces[4]),
            };

            var query = ParseQueryParams(link.Uri);

            if (!query.TryGetValue("referenceType", out var refType))
            {
                throw new ArgumentException("No reference type");
            }
            else if (refType == "EventReference")
            {
                evt.EventRef = new();
                if (query.TryGetValue("eventType", out var evtType))
                {
                    if (StringToEventType.TryGetValue(evtType, out var evtTypeEnum))
                    {
                        evt.EventRef.EventType = evtTypeEnum;
                    }
                    else
                    {
                        throw new ArgumentException($"Unknown event type: {evtType}");
                    }
                }
                if (query.TryGetValue("eventID", out var evtId))
                {
                    if (long.TryParse(evtId, out var evtIdLong))
                    {
                        evt.EventRef.EventId = evtIdLong;
                    }
                    else
                    {
                        throw new ArgumentException("Invalid event ID");
                    }
                }
            }
            else if (refType == "RequestIdReference")
            {
                evt.RequestIdRef = new();
                if (query.TryGetValue("eventType", out var evtType))
                {
                    if (StringToEventType.TryGetValue(evtType, out var evtTypeEnum))
                    {
                        evt.RequestIdRef.EventType = evtTypeEnum;
                    }
                    else
                    {
                        throw new ArgumentException($"Unknown event type: {evtType}");
                    }
                }
                if (query.TryGetValue("requestID", out var reqId))
                {
                    evt.RequestIdRef.RequestId = reqId;
                }
            }
            else
            {
                throw new ArgumentException("Unknown reference type");
            }

            return evt;
        }

        /// <summary>
        /// Parse a URI query string into its parameters. Simple hand-rolled parser because the .NET
        /// stdlib does not have one in all versions we target.
        /// </summary>
        /// <param name="uri">URI whose query string to parse.</param>
        /// <returns>
        /// The query parameters, with both keys and values percent-decoded. Values are additionally
        /// form-decoded, i.e. <c>+</c> becomes a space, since that is how a query value is encoded on
        /// the way out; the replacement is safe to do before percent-decoding because a literal
        /// <c>+</c> is encoded as <c>%2B</c>. A parameter present with no <c>=</c> maps to an empty
        /// string, so callers cannot distinguish <c>?reason</c> from <c>?reason=</c>. Keys are looked
        /// up with the dictionary's default ordinal comparer, so lookups are case-sensitive and
        /// <c>reason</c> and <c>Reason</c> are different parameters. A query repeating a key throws,
        /// which callers treat as an invalid link like any other malformed URI.
        /// </returns>
        private static Dictionary<string, string> ParseQueryParams(Uri uri) =>
            uri.Query.
                TrimStart('?').
                Split(QuerySeparator, StringSplitOptions.RemoveEmptyEntries).
                Select(v => v.Split(QueryValueSeparator, 2)).
                ToDictionary(
                    kv => Uri.UnescapeDataString(kv[0]),
                    kv => kv.Length > 1 ?
                        Uri.UnescapeDataString(kv[1].Replace("+", " ")) : string.Empty);

        // Validate a Temporal-shaped link URI and return its path segments. Expected path shape is
        // /namespaces/{namespace}/{kind}/{id}/{run}/{tail}, or /namespaces/{namespace}/{kind}/{id}/{run}
        // when expectedTail is null. The length is matched exactly, so a link with a trailing segment
        // is rejected when none is expected and vice versa.
        private static string[] ParseTemporalLinkPath(
            NexusLink link, string expectedKind, string? expectedTail)
        {
            if (link.Uri.Scheme != "temporal")
            {
                throw new ArgumentException("Invalid scheme");
            }
            if (link.Uri.Host.Length > 0)
            {
                throw new ArgumentException("Unexpected host");
            }
            var pathPieces = link.Uri.AbsolutePath.TrimStart('/').Split('/');
            if (pathPieces.Length != (expectedTail == null ? 5 : 6) ||
                pathPieces[0] != "namespaces" ||
                pathPieces[2] != expectedKind ||
                (expectedTail != null && pathPieces[5] != expectedTail))
            {
                throw new ArgumentException("Invalid path");
            }
            return pathPieces;
        }
    }
}
