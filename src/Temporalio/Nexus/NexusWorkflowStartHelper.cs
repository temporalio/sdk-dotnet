using System.Collections.Generic;
using System.Threading.Tasks;
using NexusRpc.Handlers;
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
        /// <summary>
        /// Start a workflow and return the workflow-run handle. This handles all Nexus plumbing:
        /// cloning options, setting task queue, processing links, injecting callbacks, and
        /// adding outbound links.
        /// </summary>
        /// <param name="nexusStartContext">Nexus start context for callbacks and links.</param>
        /// <param name="temporalContext">Temporal operation context for client, info, and logging.</param>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Workflow arguments.</param>
        /// <param name="options">Workflow start options. ID is required; TaskQueue defaults to
        /// the operation's task queue when omitted.</param>
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
            if (NexusOperationStartCommon.BuildInboundLinks(
                    nexusStartContext, temporalContext) is { } links)
            {
                options.Links = links;
            }
            if (NexusOperationStartCommon.BuildCallback(
                    nexusStartContext, token, options.Links) is { } callback)
            {
                options.CompletionCallbacks = new[] { callback };
            }
            options.RequestId = nexusStartContext.RequestId;

            // Do the start call
            await client.StartWorkflowAsync(
                workflow, args, options).ConfigureAwait(false);

            return handle;
        }
    }
}
