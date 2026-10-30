using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NexusRpc.Handlers;
using Temporalio.Client;
using Temporalio.Client.Interceptors;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Nexus-aware client wrapping the Temporal client. Provides methods for starting workflows
    /// from within Nexus operation handlers, handling all Nexus plumbing (links, callbacks, token
    /// generation) internally.
    /// </summary>
    /// <remarks>
    /// WARNING: Temporal operation handlers are experimental.
    /// This client is created by <see cref="TemporalOperationHandler"/> and passed to the
    /// user's start function. It should not be instantiated directly.
    /// </remarks>
    internal class TemporalNexusClient : ITemporalNexusClient
    {
        private readonly OperationStartContext nexusStartContext;
        private readonly NexusOperationExecutionContext temporalContext;
        private readonly Lazy<ITemporalClient> temporalClient;
        private bool asyncStarted;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalNexusClient"/> class.
        /// </summary>
        /// <param name="nexusStartContext">Nexus start context for callbacks and links.</param>
        /// <param name="temporalContext">Temporal operation context. </param>
        internal TemporalNexusClient(
            OperationStartContext nexusStartContext,
            NexusOperationExecutionContext temporalContext)
        {
            this.nexusStartContext = nexusStartContext;
            this.temporalContext = temporalContext;
            // A custom ITemporalClient cannot be rebuilt with an extra interceptor, so its signals are
            // not deduplicated or limited.
            temporalClient = new(() => temporalContext.TemporalClient is TemporalClient client ?
                new TemporalClient(
                    client, next => new SignalOnceInterceptor(next, nexusStartContext.RequestId)) :
                temporalContext.TemporalClient);
        }

        /// <inheritdoc/>
        public ITemporalClient TemporalClient => temporalClient.Value;

        /// <inheritdoc/>
        public Task<TemporalOperationResult<TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return StartWorkflowAsync<TResult>(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        /// <inheritdoc/>
        public Task<TemporalOperationResult<NoValue>> StartWorkflowAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return StartWorkflowAsync<NoValue>(
                Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                args,
                options);
        }

        /// <inheritdoc/>
        public async Task<TemporalOperationResult<TResult>> StartWorkflowAsync<TResult>(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options)
        {
            // Reserve the single async-operation slot for this operation invocation. Starting a
            // workflow always produces an async result, so a successful start consumes the slot and
            // a failure releases it. A Nexus operation Start handler runs single-threaded per
            // invocation, so no synchronization is needed.
            if (asyncStarted)
            {
                throw new HandlerException(
                    HandlerErrorType.BadRequest,
                    "only one async operation can be started per operation invocation");
            }
            asyncStarted = true;
            var keepSlot = false;
            try
            {
                var handle = await NexusWorkflowStartHelper.StartWorkflowAsync(
                    nexusStartContext,
                    temporalContext,
                    workflow,
                    args,
                    options).ConfigureAwait(false);
                keepSlot = true;
                return TemporalOperationResult<TResult>.AsyncResult(handle.ToToken());
            }
            finally
            {
                if (!keepSlot)
                {
                    asyncStarted = false;
                }
            }
        }

        /// <inheritdoc/>
        public Task<TemporalOperationResult<TResult>> StartWorkflowUpdateAsync<TWorkflow, TResult>(
            string workflowId,
            Expression<Func<TWorkflow, Task<TResult>>> updateCall,
            WorkflowUpdateStartOptions options,
            string? runId = null)
        {
            var (method, args) = Common.ExpressionUtil.ExtractCall(updateCall);
            return StartWorkflowUpdateAsync<TResult>(
                workflowId,
                Workflows.WorkflowUpdateDefinition.NameFromMethodForCall(method),
                args,
                options,
                runId);
        }

        /// <inheritdoc/>
        public Task<TemporalOperationResult<NoValue>> StartWorkflowUpdateAsync<TWorkflow>(
            string workflowId,
            Expression<Func<TWorkflow, Task>> updateCall,
            WorkflowUpdateStartOptions options,
            string? runId = null)
        {
            var (method, args) = Common.ExpressionUtil.ExtractCall(updateCall);
            return StartWorkflowUpdateAsync<NoValue>(
                workflowId,
                Workflows.WorkflowUpdateDefinition.NameFromMethodForCall(method),
                args,
                options,
                runId);
        }

        /// <inheritdoc/>
        public async Task<TemporalOperationResult<TResult>> StartWorkflowUpdateAsync<TResult>(
            string workflowId,
            string update,
            IReadOnlyCollection<object?> args,
            WorkflowUpdateStartOptions options,
            string? runId = null)
        {
            // Reserve the single async-operation slot for this operation invocation. Only a genuine
            // async result consumes it; a sync result or a failure releases it. A Nexus operation
            // Start handler runs single-threaded per invocation, so no synchronization is needed.
            if (asyncStarted)
            {
                throw new HandlerException(
                    HandlerErrorType.BadRequest,
                    "only one async operation can be started per operation invocation");
            }
            asyncStarted = true;
            var keepSlot = false;
            try
            {
                var result = await NexusWorkflowStartHelper.StartWorkflowUpdateAsync<TResult>(
                    nexusStartContext,
                    temporalContext,
                    workflowId,
                    update,
                    args,
                    options,
                    runId).ConfigureAwait(false);
                keepSlot = !result.IsSyncResult;
                return result;
            }
            finally
            {
                if (!keepSlot)
                {
                    asyncStarted = false;
                }
            }
        }

        /// <inheritdoc/>
        public Task<TemporalOperationResult<TResult>> StartActivityAsync<TResult>(
            Expression<Func<Task<TResult>>> activityCall, StartActivityOptions options)
        {
            var (method, args) = Common.ExpressionUtil.ExtractCall(activityCall);
            return StartActivityAsync<TResult>(
                Activities.ActivityDefinition.NameFromMethodForCall(method),
                args,
                options);
        }

        /// <inheritdoc/>
        public Task<TemporalOperationResult<NoValue>> StartActivityAsync(
            Expression<Func<Task>> activityCall, StartActivityOptions options)
        {
            var (method, args) = Common.ExpressionUtil.ExtractCall(activityCall);
            return StartActivityAsync<NoValue>(
                Activities.ActivityDefinition.NameFromMethodForCall(method),
                args,
                options);
        }

        /// <inheritdoc/>
        public async Task<TemporalOperationResult<TResult>> StartActivityAsync<TResult>(
            string activity, IReadOnlyCollection<object?> args, StartActivityOptions options)
        {
            // Reserve the single async-operation slot for this operation invocation. Starting an
            // activity always produces an async result, so a successful start consumes the slot and
            // a failure releases it. A Nexus operation Start handler runs single-threaded per
            // invocation, so no synchronization is needed.
            if (asyncStarted)
            {
                throw new HandlerException(
                    HandlerErrorType.BadRequest,
                    "only one async operation can be started per operation invocation");
            }
            asyncStarted = true;
            var keepSlot = false;
            try
            {
                var token = await NexusActivityStartHelper.StartActivityAsync(
                    TemporalClient,
                    nexusStartContext,
                    temporalContext,
                    activity,
                    args,
                    options).ConfigureAwait(false);
                keepSlot = true;
                return TemporalOperationResult<TResult>.AsyncResult(token);
            }
            finally
            {
                if (!keepSlot)
                {
                    asyncStarted = false;
                }
            }
        }

        // Signals share the operation's request ID, so the server would silently drop a second
        // one; reject it instead.
        private sealed class SignalOnceInterceptor : ClientOutboundInterceptor
        {
            private readonly string requestId;
            private int signaled;

            internal SignalOnceInterceptor(ClientOutboundInterceptor next, string requestId)
                : base(next) => this.requestId = requestId;

            public override Task SignalWorkflowAsync(SignalWorkflowInput input)
            {
                var options = (WorkflowSignalOptions?)input.Options?.Clone() ?? new();
                options.RequestId = requestId;
                return SendOnceAsync(async () =>
                {
                    await base.SignalWorkflowAsync(input with { Options = options }).ConfigureAwait(false);
                    return true;
                });
            }

            public override Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
                StartWorkflowInput input)
            {
                if (input.Options.StartSignal == null)
                {
                    return base.StartWorkflowAsync<TWorkflow, TResult>(input);
                }
                var options = (WorkflowOptions)input.Options.Clone();
                options.RequestId = requestId;
                return SendOnceAsync(() =>
                    base.StartWorkflowAsync<TWorkflow, TResult>(input with { Options = options }));
            }

            // Signals may be sent concurrently from one handler.
            private async Task<T> SendOnceAsync<T>(Func<Task<T>> send)
            {
                if (Interlocked.CompareExchange(ref signaled, 1, 0) != 0)
                {
                    throw new HandlerException(
                        HandlerErrorType.BadRequest,
                        "only one signal can be sent per operation invocation");
                }
                try
                {
                    return await send().ConfigureAwait(false);
                }
                catch
                {
                    Volatile.Write(ref signaled, 0);
                    throw;
                }
            }
        }
    }
}
