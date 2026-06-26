using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Plumbing shared by <see cref="NexusWorkflowStartHelper"/> and
    /// <see cref="NexusActivityStartHelper"/> for translating inbound Nexus links and building the
    /// completion callback.
    /// </summary>
    internal static class NexusOperationStartHelper
    {
        /// <summary>
        /// Header name used to carry the Nexus operation token on completion callbacks.
        /// </summary>
        internal const string NexusOperationTokenHeader = "Nexus-Operation-Token";

        /// <summary>
        /// Translate inbound Nexus links into Temporal proto links. Unrecognized link types are
        /// dropped silently; malformed links are logged and dropped.
        /// </summary>
        /// <param name="nexusStartContext">Nexus start context.</param>
        /// <param name="temporalContext">Temporal operation context (used for logging).</param>
        /// <returns>List of proto links, or <c>null</c> if there are no inbound links.</returns>
        internal static IReadOnlyCollection<Link>? CreateInboundLinks(
            OperationStartContext nexusStartContext,
            NexusOperationExecutionContext temporalContext)
        {
            if (nexusStartContext.InboundLinks.Count == 0)
            {
                return null;
            }
            return nexusStartContext.InboundLinks.Select(link =>
            {
                try
                {
                    return link.ToProtoLink();
                }
                catch (ArgumentException e)
                {
                    temporalContext.Logger.LogWarning(e, "Invalid Nexus link: {Url}", link.Uri);
                    return null;
                }
            }).OfType<Link>().ToList();
        }

        /// <summary>
        /// Build the completion callback from the start context, attaching the operation token
        /// header (unless the caller already supplied one) and any inbound links.
        /// </summary>
        /// <param name="nexusStartContext">Nexus start context.</param>
        /// <param name="token">Operation token to inject as a callback header.</param>
        /// <param name="links">Links to attach to the callback (typically from
        /// <see cref="CreateInboundLinks"/>).</param>
        /// <returns>Callback to attach to start options, or <c>null</c> if no callback URL is set.</returns>
        internal static Callback? CreateCallback(
            OperationStartContext nexusStartContext,
            string token,
            IReadOnlyCollection<Link>? links)
        {
            if (nexusStartContext.CallbackUrl is not { } callbackUrl)
            {
                return null;
            }
            var callback = new Callback() { Nexus = new() { Url = callbackUrl } };
            var callbackHeadersHasToken = false;
            if (nexusStartContext.CallbackHeaders is { } callbackHeaders)
            {
                foreach (var kv in callbackHeaders)
                {
                    callback.Nexus.Header.Add(kv.Key, kv.Value);
                    if (string.Equals(
                            kv.Key, NexusOperationTokenHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        callbackHeadersHasToken = true;
                    }
                }
            }
            // Set operation token if not already present (header is case-insensitive)
            if (!callbackHeadersHasToken)
            {
                callback.Nexus.Header[NexusOperationTokenHeader] = token;
            }
            if (links is { } notNullLinks)
            {
                callback.Links.AddRange(notNullLinks);
            }
            return callback;
        }
    }
}
