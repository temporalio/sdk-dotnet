using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Google.Protobuf.Reflection;
using NexusRpc;
using Temporalio.Api.Enums.V1;

namespace Temporalio.Nexus
{
    internal static class ProtoLinkExtensions
    {
        // Need to map PascalCase enum names and original enum names to the event type
        private static readonly Dictionary<string, EventType> stringToEventType =
            Enum.GetValues(typeof(EventType)).
                Cast<EventType>().
                SelectMany(e =>
                    new[]
                    {
                        (e.ToString(), e),
                        (typeof(EventType).GetField(e.ToString()).
                            GetCustomAttribute<OriginalNameAttribute>().Name, e),
                    }).
                ToDictionary(kv => kv.Item1, kv => kv.e);

        private static readonly EnumDescriptor eventTypeDescriptor =
            EventTypeReflection.Descriptor.FindTypeByName<EnumDescriptor>("EventType");

        private static readonly char[] querySeparator = new[] { '&' };
        private static readonly char[] queryValueSeparator = new[] { '=' };

        public static NexusLink ToNexusLink(this Api.Common.V1.Link.Types.WorkflowEvent evt)
        {
            // Set some query params
            var queryParams = new Dictionary<string, string>();
            if (evt.EventRef is { } evtRef)
            {
                queryParams["referenceType"] = "EventReference";
                queryParams["eventType"] = eventTypeDescriptor.FindValueByNumber((int)evtRef.EventType).Name;
                if (evtRef.EventId > 0)
                {
                    queryParams["eventID"] = evtRef.EventId.ToString();
                }
            }
            else if (evt.RequestIdRef is { } reqIdRef)
            {
                queryParams["referenceType"] = "RequestIdReference";
                queryParams["eventType"] = eventTypeDescriptor.FindValueByNumber((int)reqIdRef.EventType).Name;
                queryParams["requestID"] = reqIdRef.RequestId;
            }

            // Build URI
            var builder = new UriBuilder
            {
                Scheme = "temporal",
                Path = "/namespaces/" + Uri.EscapeDataString(evt.Namespace) + "/workflows/" +
                    Uri.EscapeDataString(evt.WorkflowId) + "/" + Uri.EscapeDataString(evt.RunId) +
                    "/history",
                Query = string.Join("&", queryParams.Select(kvp =>
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}")),
            };
            return new(builder.Uri, Api.Common.V1.Link.Types.WorkflowEvent.Descriptor.FullName);
        }

        public static Api.Common.V1.Link.Types.WorkflowEvent ToWorkflowEvent(this NexusLink link)
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
            if (pathPieces.Length != 6 ||
                pathPieces[0] != "namespaces" ||
                pathPieces[2] != "workflows" ||
                pathPieces[5] != "history")
            {
                throw new ArgumentException("Invalid path");
            }
            var evt = new Api.Common.V1.Link.Types.WorkflowEvent
            {
                Namespace = Uri.UnescapeDataString(pathPieces[1]),
                WorkflowId = Uri.UnescapeDataString(pathPieces[3]),
                RunId = Uri.UnescapeDataString(pathPieces[4]),
            };

            // Simple query param parser because .NET stdlib doesn't have one in all versions
            var query = link.Uri.Query.
                TrimStart('?').
                Split(querySeparator, StringSplitOptions.RemoveEmptyEntries).
                Select(v => v.Split(queryValueSeparator, 2)).
                ToDictionary(
                    kv => Uri.UnescapeDataString(kv[0]),
                    kv => kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty);

            if (!query.TryGetValue("referenceType", out var refType))
            {
                throw new ArgumentException("No reference type");
            }
            else if (refType == "EventReference")
            {
                evt.EventRef = new();
                if (query.TryGetValue("eventType", out var evtType))
                {
                    if (stringToEventType.TryGetValue(evtType, out var evtTypeEnum))
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
                    if (stringToEventType.TryGetValue(evtType, out var evtTypeEnum))
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
    }
}