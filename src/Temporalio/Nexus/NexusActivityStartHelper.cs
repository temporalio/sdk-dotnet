using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
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
        internal static async Task<string> StartActivityAsync(
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
            var callbackToken = NexusActivityExecutionToken.Create(namespace_, activityId, runId: null);

            var links = temporalContext.RequestLinks;
            if (links.Count > 0)
            {
                options.Links = options.Links is { } existingLinks ?
                    existingLinks.Concat(links).ToList() : links;
            }
            var callback = NexusOperationStartHelper.CreateCallback(
                nexusStartContext, callbackToken, options.Links);
            if (callback != null)
            {
                options.CompletionCallbacks = new[] { callback };
            }
            options.OnConflictOptions = CreateActivityOnConflictOptions(
                options.IdConflictPolicy,
                hasLinks: options.Links is { Count: > 0 },
                hasCompletionCallback: callback != null);
            options.RequestId = nexusStartContext.RequestId;

            // Do the start call
            var handle = await client.StartActivityAsync(
                activity, args, options).ConfigureAwait(false);

            // Return a token that includes the run ID from the start response.
            return NexusActivityExecutionToken.Create(namespace_, activityId, handle.RunId);
        }

        /// <summary>
        /// Build on-conflict options for an activity start under a <c>UseExisting</c> ID conflict
        /// policy, attaching only the artifacts the start actually carries. The server rejects
        /// attaching a request ID with no accompanying link or completion callback, so this
        /// returns <c>null</c> unless there is at least one to attach.
        /// </summary>
        /// <param name="idConflictPolicy">The activity ID conflict policy.</param>
        /// <param name="hasLinks">Whether the start carries links to attach.</param>
        /// <param name="hasCompletionCallback">Whether the start carries a completion callback to
        /// attach.</param>
        /// <returns>On-conflict options, or <c>null</c> if not applicable.</returns>
        internal static OnConflictOptions? CreateActivityOnConflictOptions(
            ActivityIdConflictPolicy idConflictPolicy,
            bool hasLinks,
            bool hasCompletionCallback)
        {
            if (idConflictPolicy != ActivityIdConflictPolicy.UseExisting ||
                (!hasLinks && !hasCompletionCallback))
            {
                return null;
            }
            return new()
            {
                AttachLinks = hasLinks,
                AttachCompletionCallbacks = hasCompletionCallback,
                AttachRequestId = true,
            };
        }
    }
}
