using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Internal helper for starting workflows from Nexus operations and managing operation tokens.
    /// Shared by both <see cref="WorkflowRunOperationContext"/> and <see cref="TemporalNexusClient"/>.
    /// </summary>
    internal static class NexusWorkflowStartHelper
    {
        private const string NexusOperationTokenHeader = "Nexus-Operation-Token";

        /// <summary>
        /// Start a workflow and return the workflow-run handle. This handles all Nexus plumbing:
        /// cloning options, setting task queue, processing links, injecting callbacks, and
        /// adding outbound links.
        /// </summary>
        /// <param name="nexusStartContext">Nexus start context for callbacks and links.</param>
        /// <param name="temporalContext">Temporal operation context for client, info, and logging.</param>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Workflow arguments.</param>
        /// <param name="options">Workflow start options. ID and TaskQueue are required.</param>
        /// <returns>Workflow-run handle for the started workflow.</returns>
        internal static async Task<NexusWorkflowRunHandle> StartWorkflowAsync(
            OperationStartContext nexusStartContext,
            NexusOperationExecutionContext temporalContext,
            string workflow,
            IReadOnlyCollection<object?> args,
            WorkflowOptions options)
        {
            var client = temporalContext.TemporalClient;
            var namespace_ = client.Options.Namespace;
            var workflowId = options.Id ?? string.Empty;

            // Generate the handle and token before starting the workflow (token is needed for the
            // callback header).
            var handle = new NexusWorkflowRunHandle(namespace_, workflowId, 0);
            var token = handle.ToToken();

            // Shallow clone the options so we can mutate them. We just overwrite any of these
            // internal options since they cannot be user set at this time.
            options = (WorkflowOptions)options.Clone();
            options.TaskQueue ??= temporalContext.Info.TaskQueue;
            if (options.IdConflictPolicy == WorkflowIdConflictPolicy.UseExisting)
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
                // Set operation token if not already present (header is case-insensitive)
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
            var wfHandle = await client.StartWorkflowAsync(
                workflow, args, options).ConfigureAwait(false);

            // Add the outbound link
            nexusStartContext.OutboundLinks.Add(new Link.Types.WorkflowEvent
            {
                Namespace = namespace_,
                WorkflowId = workflowId,
                RunId = wfHandle.FirstExecutionRunId ??
                    throw new InvalidOperationException("Handle unexpectedly missing run ID"),
                EventRef = new() { EventId = 1, EventType = EventType.WorkflowExecutionStarted },
            }.ToNexusLink());

            return handle;
        }
    }
}
