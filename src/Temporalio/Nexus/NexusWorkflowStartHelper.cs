using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

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
                        return link.ToProtoLink();
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

        /// <summary>
        /// Start a workflow update from a Nexus operation and return the operation result. This
        /// handles all Nexus plumbing: validating the wait stage, defaulting the update ID to the
        /// Nexus request ID, generating the operation token, injecting the request ID, callback, and
        /// links onto the update request, and adding the outbound link.
        /// </summary>
        /// <typeparam name="TResult">Operation result type.</typeparam>
        /// <param name="nexusStartContext">Nexus start context for callbacks and links.</param>
        /// <param name="temporalContext">Temporal operation context for client, info, and logging.</param>
        /// <param name="workflowId">Target workflow ID.</param>
        /// <param name="update">Update name.</param>
        /// <param name="args">Update arguments.</param>
        /// <param name="options">Update start options. <c>WaitForStage</c> must be
        /// <see cref="WorkflowUpdateStage.Accepted"/>.</param>
        /// <param name="runId">Target workflow run ID, or null for the latest run.</param>
        /// <returns>An async result carrying the update-workflow token, or a sync result if the
        /// update already completed.</returns>
        internal static async Task<TemporalOperationResult<TResult>> StartWorkflowUpdateAsync<TResult>(
            OperationStartContext nexusStartContext,
            NexusOperationExecutionContext temporalContext,
            string workflowId,
            string update,
            IReadOnlyCollection<object?> args,
            WorkflowUpdateStartOptions options,
            string? runId = null)
        {
            // Only WorkflowUpdateStage.Accepted is supported. A Nexus handler has a short deadline,
            // so waiting for full update completion is unsupported; reject any other wait stage as a
            // failed operation.
            if (options.WaitForStage != WorkflowUpdateStage.Accepted)
            {
                throw OperationException.CreateFailed(
                    "nexus operation workflow updates only support WorkflowUpdateStage.Accepted");
            }

            // Shallow clone the options so we can mutate them without leaking the internal Nexus
            // state (ID, links, callbacks, request ID) back to a caller that reuses the instance.
            options = (WorkflowUpdateStartOptions)options.Clone();

            // Resolve the update ID as a non-nullable local. When the caller doesn't supply one,
            // fall back to the Nexus request ID: the server assigns it and keeps it stable across
            // task redeliveries, so the update stays deduplicated if the start task is redelivered.
            // (The "is { } id" pattern narrows Id to non-null on all targets; IsNullOrWhiteSpace
            // isn't annotated as a null guard on the netstandard2.0/net462 targets so
            // caused a compiler error without that.)
            string updateId;
            if (options.Id is { } id && !string.IsNullOrWhiteSpace(id))
            {
                updateId = id;
            }
            else if (!string.IsNullOrWhiteSpace(nexusStartContext.RequestId))
            {
                updateId = nexusStartContext.RequestId;
            }
            else
            {
                throw new HandlerException(
                    HandlerErrorType.Internal,
                    "no update ID supplied and the Nexus request ID is empty");
            }

            options.Id = updateId;

            if (nexusStartContext.CallbackUrl is not { } callbackUrl)
            {
                throw new HandlerException(
                    HandlerErrorType.BadRequest,
                    "callback URL required for async UpdateWorkflow operation invocations");
            }

            var client = temporalContext.TemporalClient;

            // Generate the operation token before starting the update; it is needed for the callback
            // header.
            var handle = new NexusWorkflowUpdateHandle(
                client.Options.Namespace,
                workflowId,
                runId: runId ?? string.Empty,
                updateId: updateId,
                version: 0);
            var token = handle.ToToken();

            // Convert inbound links to backward links carried on the update request.
            var links = nexusStartContext.InboundLinks.Select(link =>
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
            // Set operation token if not already present (header is case-insensitive).
            if (!callbackHeadersHasToken)
            {
                callback.Nexus.Header[NexusOperationTokenHeader] = token;
            }
            if (links.Count > 0)
            {
                callback.Links.AddRange(links);
                options.Links = links;
            }
            options.CompletionCallbacks = new[] { callback };
            options.RequestId = nexusStartContext.RequestId;

            // Cancel the (potentially long-polling) update-start RPC if the Nexus operation is
            // cancelled — e.g. worker shutdown or operation timeout. StartUpdateAsync with
            // WaitForStage.Accepted blocks until the update is accepted; without forwarding the
            // operation's cancellation token, a target task queue with no worker would leave this
            // handler task blocked and wedge worker drain.
            options.Rpc = options.Rpc is { } existingRpc
                ? (RpcOptions)existingRpc.Clone()
                : new RpcOptions();
            using var linkedStartCts = options.Rpc.CancellationToken is { } userStartToken
                ? CancellationTokenSource.CreateLinkedTokenSource(
                    userStartToken, nexusStartContext.CancellationToken)
                : null;
            options.Rpc.CancellationToken =
                linkedStartCts?.Token ?? nexusStartContext.CancellationToken;

            var updateHandle = await client.GetWorkflowHandle(workflowId, runId)
                .StartUpdateAsync<TResult>(update, args, options).ConfigureAwait(false);

            // Capture the link from the response and add it as an outbound handler link when one is
            // present. A rejected or failed update legitimately may have no link, so in that case we
            // skip attaching the outbound link and continue, surfacing the outcome (sync result or
            // failed operation) normally. On validation failure there may be no history event, so a
            // present link can be a plain workflow link rather than a workflow-event link.
            if (updateHandle.Link is { } responseLink &&
                responseLink.ToNexusLink() is { } outboundLink)
            {
                nexusStartContext.OutboundLinks.Add(outboundLink);
            }

            // If the update already completed (e.g. a retried request with the same update ID, or an
            // immediately-completing update returned as completed), return a synchronous result. A
            // completed-with-error update (such as a validation rejection) is non-retryable and
            // surfaces as a failed operation.
            if (updateHandle.KnownOutcome != null)
            {
                try
                {
                    if (typeof(TResult) == typeof(NoValue))
                    {
                        await updateHandle.GetResultAsync().ConfigureAwait(false);
                        return TemporalOperationResult<TResult>.SyncResult(default);
                    }
                    var value = await updateHandle.GetResultAsync<TResult>().ConfigureAwait(false);
                    return TemporalOperationResult<TResult>.SyncResult(value);
                }
                catch (WorkflowUpdateFailedException e)
                {
                    throw OperationException.CreateFailed(e.Message, e);
                }
            }

            return TemporalOperationResult<TResult>.AsyncResult(token);
        }
    }
}
