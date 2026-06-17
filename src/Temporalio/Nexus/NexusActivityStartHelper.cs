using System.Collections.Generic;
using System.Threading.Tasks;
using NexusRpc.Handlers;
using Temporalio.Client;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Internal helper for starting standalone activities from Nexus operations and managing
    /// activity-execution operation tokens.
    /// </summary>
    internal static class NexusActivityStartHelper
    {
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
        /// StartToCloseTimeout must be set; TaskQueue defaults to the operation's task queue.</param>
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

            var namespace_ = client.Options.Namespace;
            var activityId = options.Id!;

            // Build the callback-header token without a run ID (we don't have it yet).
            var callbackToken = NexusActivityExecutionToken.Build(namespace_, activityId, runId: null);

            if (options.IdConflictPolicy == Api.Enums.V1.ActivityIdConflictPolicy.UseExisting)
            {
                options.OnConflictOptions = new()
                {
                    AttachLinks = true,
                    AttachCompletionCallbacks = true,
                    AttachRequestId = true,
                };
            }
            if (NexusOperationStartCommon.BuildInboundLinks(
                    nexusStartContext, temporalContext) is { } links)
            {
                options.Links = links;
            }
            if (NexusOperationStartCommon.BuildCallback(
                    nexusStartContext, callbackToken, options.Links) is { } callback)
            {
                options.CompletionCallbacks = new[] { callback };
            }
            options.RequestId = nexusStartContext.RequestId;

            // Do the start call
            var handle = await client.StartActivityAsync(
                activity, args, options).ConfigureAwait(false);

            // Return a token that includes the run ID from the start response.
            return NexusActivityExecutionToken.Build(namespace_, activityId, handle.RunId);
        }
    }
}
