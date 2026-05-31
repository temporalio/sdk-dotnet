using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Client;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Internal helper for starting standalone activities from Nexus operations and managing
    /// activity-execution operation tokens.
    /// </summary>
    internal static class NexusActivityStartHelper
    {
        private const string NexusOperationTokenHeader = "Nexus-Operation-Token";

        /// <summary>
        /// Start a standalone activity and return the activity-execution operation token. This
        /// handles all Nexus plumbing: cloning options, defaulting task queue / ID, processing
        /// links, injecting callbacks, and adding outbound links.
        /// </summary>
        /// <param name="client">Temporal client.</param>
        /// <param name="nexusStartContext">Nexus start context for callbacks and links.</param>
        /// <param name="temporalContext">Temporal operation context for info and logging.</param>
        /// <param name="activity">Activity type name.</param>
        /// <param name="args">Activity arguments.</param>
        /// <param name="options">Activity start options. Either ScheduleToCloseTimeout or
        /// StartToCloseTimeout must be set; TaskQueue defaults to the operation's task queue and
        /// ID defaults to a new GUID.</param>
        /// <returns>Base64url-encoded operation token.</returns>
        internal static async Task<string> StartActivityAndGetTokenAsync(
            ITemporalClient client,
            OperationStartContext nexusStartContext,
            NexusOperationExecutionContext temporalContext,
            string activity,
            IReadOnlyCollection<object?> args,
            StartActivityOptions options)
        {
            // Shallow clone so we can mutate
            options = (StartActivityOptions)options.Clone();
            options.TaskQueue ??= temporalContext.Info.TaskQueue;
            if (string.IsNullOrEmpty(options.Id))
            {
                var message = "Activity ID is required. Derive it deterministically from the " +
                    "operation input so retries of this Nexus start request are idempotent.";
                throw new HandlerException(HandlerErrorType.BadRequest, message);
            }
            if (options.ScheduleToCloseTimeout == null && options.StartToCloseTimeout == null)
            {
                throw new HandlerException(
                    HandlerErrorType.BadRequest,
                    "At least one of ScheduleToCloseTimeout or StartToCloseTimeout is required");
            }

            var namespace_ = client.Options.Namespace;
            var activityId = options.Id!;

            // Generate the token before starting the activity (needed for callback header)
            var token = new NexusActivityExecutionHandle(namespace_, activityId, 0).ToToken();

            if (options.IdConflictPolicy == Api.Enums.V1.ActivityIdConflictPolicy.UseExisting)
            {
                options.OnConflictOptions = new()
                {
                    AttachLinks = true,
                    AttachCompletionCallbacks = true,
                    AttachRequestId = true,
                };
            }
            if (nexusStartContext.InboundLinks.Count > 0)
            {
                options.Links = nexusStartContext.InboundLinks.Select(link =>
                {
                    try
                    {
                        return new Link { WorkflowEvent = link.ToWorkflowEvent() };
                    }
                    catch (ArgumentException e)
                    {
                        temporalContext.Logger.LogWarning(e, "Invalid Nexus link: {Url}", link.Uri);
                        return null;
                    }
                }).OfType<Link>().ToList();
            }
            if (nexusStartContext.CallbackUrl is { } callbackUrl)
            {
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
                if (!callbackHeadersHasToken)
                {
                    callback.Nexus.Header[NexusOperationTokenHeader] = token;
                }
                if (options.Links is { } links)
                {
                    callback.Links.AddRange(links);
                }
                options.CompletionCallbacks = new[] { callback };
            }
            options.RequestId = nexusStartContext.RequestId;

            // Do the start call
            var handle = await client.StartActivityAsync(
                activity, args, options).ConfigureAwait(false);

            // Add the outbound link
            nexusStartContext.OutboundLinks.Add(new Link.Types.Activity
            {
                Namespace = namespace_,
                ActivityId = activityId,
                RunId = handle.RunId ??
                    throw new InvalidOperationException("Handle unexpectedly missing run ID"),
            }.ToNexusLink());

            return token;
        }
    }
}
