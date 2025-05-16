using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NexusRpc.Handler;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;

namespace Temporalio.Nexus
{
    public class WorkflowRunOperationContext
    {
        internal WorkflowRunOperationContext(OperationStartContext handlerContext) =>
            HandlerContext = handlerContext;

        public OperationStartContext HandlerContext { get; private init; }

        public Task<NexusWorkflowRunHandle<TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return StartWorkflowAsync<TResult>(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        public async Task<NexusWorkflowRunHandle<TResult>> StartWorkflowAsync<TResult>(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options)
        {
            var temporalContext = NexusOperationExecutionContext.Current;
            var nexusContext = (OperationStartContext)temporalContext.HandlerContext;

            // Create the resulting handle ahead of time since we need the serialized form in a
            // header
            var handle = new NexusWorkflowRunHandle<TResult>(
                temporalContext.TemporalClient.Options.Namespace,
                // Missing ID will be caught later
                options.Id ?? string.Empty);

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
            if (nexusContext.InboundLinks.Count > 0)
            {
                options.Links = nexusContext.InboundLinks.Select(link =>
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
            if (nexusContext.CallbackUrl is { } callbackUrl)
            {
                var callback = new Callback() { Nexus = new() { Url = callbackUrl } };
                if (nexusContext.CallbackHeaders is { } callbackHeaders)
                {
                    foreach (var kv in callbackHeaders)
                    {
                        callback.Nexus.Header.Add(kv.Key, kv.Value);
                    }
                }
                // Set operation token
                if (nexusContext.CallbackHeaders?.ContainsKey("Nexus-Operation-Token") != true)
                {
                    callback.Nexus.Header["Nexus-Operation-Token"] = handle.ToToken();
                }
                if (options.Links is { } links)
                {
                    callback.Links.AddRange(links);
                }
                options.CompletionCallbacks = new[] { callback };
            }
            options.RequestId = nexusContext.RequestId;

            // Do the start call
            var wfHandle = await temporalContext.TemporalClient.StartWorkflowAsync(
                workflow, args, options).ConfigureAwait(false);

            // Add the outbound link
            nexusContext.OutboundLinks.Add(new Link.Types.WorkflowEvent
            {
                Namespace = handle.Namespace,
                WorkflowId = handle.WorkflowId,
                RunId = wfHandle.FirstExecutionRunId ??
                    throw new InvalidOperationException("Handle unexpectedly missing run ID"),
                EventRef = new() { EventId = 1, EventType = EventType.WorkflowExecutionStarted },
            }.ToNexusLink());
            return handle;
        }
    }
}