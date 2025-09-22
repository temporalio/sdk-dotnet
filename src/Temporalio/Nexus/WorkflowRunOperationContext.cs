using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Context used to create workflow run handles. This is passed to functions passed to
    /// <c>FromHandleFactory</c> on <see cref="WorkflowRunOperationHandler"/>.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class WorkflowRunOperationContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowRunOperationContext"/> class.
        /// </summary>
        /// <param name="handlerContext">Nexus context.</param>
        internal WorkflowRunOperationContext(OperationStartContext handlerContext) =>
            HandlerContext = handlerContext;

        /// <summary>
        /// Gets the Nexus handler context for the start call.
        /// </summary>
        public OperationStartContext HandlerContext { get; private init; }

        /// <summary>
        /// Start a workflow via a lambda invoking the run method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method with no result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Nexus workflow run handle to return in handle factory.</returns>
        public Task<NexusWorkflowRunHandle> StartWorkflowAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return StartWorkflowAsync(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        /// <summary>
        /// Start a workflow via a lambda invoking the run method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TResult">Workflow result type.</typeparam>
        /// <param name="workflowRunCall">Invocation of workflow run method with a result.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Nexus workflow run handle to return in handle factory.</returns>
        public Task<NexusWorkflowRunHandle<TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return StartWorkflowAsync<TResult>(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        /// <summary>
        /// Start a workflow by name with no result type.
        /// </summary>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Nexus workflow run handle to return in handle factory.</returns>
#pragma warning disable CA1822 // We don't want this static
        public async Task<NexusWorkflowRunHandle> StartWorkflowAsync(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options)
        {
#pragma warning restore CA1822
            var temporalContext = NexusOperationExecutionContext.Current;
            var handle = new NexusWorkflowRunHandle(
                temporalContext.TemporalClient.Options.Namespace,
                // Missing ID will be caught later
                options.Id ?? string.Empty,
                version: 0);
            await StartWorkflowInternalAsync(handle, workflow, args, options).ConfigureAwait(false);
            return handle;
        }

        /// <summary>
        /// Start a workflow by name with a result type.
        /// </summary>
        /// <typeparam name="TResult">Result type.</typeparam>
        /// <param name="workflow">Workflow type name.</param>
        /// <param name="args">Arguments for the workflow.</param>
        /// <param name="options">Start workflow options. ID and TaskQueue are required.</param>
        /// <returns>Nexus workflow run handle to return in handle factory.</returns>
#pragma warning disable CA1822 // We don't want this static
        public async Task<NexusWorkflowRunHandle<TResult>> StartWorkflowAsync<TResult>(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options)
        {
#pragma warning restore CA1822
            var temporalContext = NexusOperationExecutionContext.Current;
            var handle = new NexusWorkflowRunHandle<TResult>(
                temporalContext.TemporalClient.Options.Namespace,
                // Missing ID will be caught later
                options.Id ?? string.Empty,
                version: 0);
            await StartWorkflowInternalAsync(handle, workflow, args, options).ConfigureAwait(false);
            return handle;
        }

        private static async Task StartWorkflowInternalAsync(
            NexusWorkflowRunHandle handle,
            string workflow,
            IReadOnlyCollection<object?> args,
            WorkflowOptions options)
        {
            var temporalContext = NexusOperationExecutionContext.Current;
            var nexusContext = (OperationStartContext)temporalContext.HandlerContext;

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
        }
    }
}