using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using Temporalio.Activities;
using Temporalio.Api.Common.V1;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

namespace Temporalio.Extensions.OpenTelemetry
{
    /// <summary>
    /// Client and worker interceptor that will create and propagate diagnostic activities for
    /// clients, workflows, and activities. This can be instantiated and set as an interceptor on
    /// the client options and it will automatically apply to all uses including workers.
    /// </summary>
    /// <remarks>
    /// This uses OpenTelemetry context propagation and Temporal headers to serialize the diagnostic
    /// activities across workers. Normal <see cref="ActivitySource" /> methods can be used for
    /// client and activity code. Workflows however are interruptible/resumable and therefore cannot
    /// support .NET activities (i.e. OpenTelemetry spans) that remain open across workers.
    /// Therefore, all uses of diagnostic activities inside workflows should only use
    /// <see cref="ActivitySourceExtensions.TrackWorkflowDiagnosticActivity" />. See the project
    /// README for more information.
    /// </remarks>
    public class TracingInterceptor : IClientInterceptor, IWorkerInterceptor
    {
        /// <summary>
        /// Source used for all client outbound diagnostic activities.
        /// </summary>
        public static readonly ActivitySource ClientSource = new("Temporalio.Extensions.OpenTelemetry.Client");

        /// <summary>
        /// Source used for all workflow inbound/outbound diagnostic activities.
        /// </summary>
        public static readonly ActivitySource WorkflowsSource = new("Temporalio.Extensions.OpenTelemetry.Workflow");

        /// <summary>
        /// Source used for all activity inbound diagnostic activities.
        /// </summary>
        public static readonly ActivitySource ActivitiesSource = new("Temporalio.Extensions.OpenTelemetry.Activity");

        /// <summary>
        /// Initializes a new instance of the <see cref="TracingInterceptor"/> class.
        /// </summary>
        /// <param name="options">Optional options.</param>
        public TracingInterceptor(TracingInterceptorOptions? options = null) =>
            Options = options ?? new();

        /// <summary>
        /// Gets the options this interceptor was created with. This should never be mutated.
        /// </summary>
        public TracingInterceptorOptions Options { get; init; }

        /// <inheritdoc />
        public ClientOutboundInterceptor InterceptClient(
            ClientOutboundInterceptor nextInterceptor) =>
            new ClientOutbound(this, nextInterceptor);

        /// <inheritdoc />
        public WorkflowInboundInterceptor InterceptWorkflow(
            WorkflowInboundInterceptor nextInterceptor) =>
            new WorkflowInbound(this, nextInterceptor);

        /// <inheritdoc />
        public ActivityInboundInterceptor InterceptActivity(
            ActivityInboundInterceptor nextInterceptor) =>
            new ActivityInbound(this, nextInterceptor);

        /// <summary>
        /// Serialize an OTel context to Temporal headers.
        /// </summary>
        /// <param name="headers">Headers to mutate if present.</param>
        /// <param name="ctx">OTel context.</param>
        /// <returns>Created/updated headers.</returns>
        protected virtual IDictionary<string, Payload> HeadersFromContext(
            IDictionary<string, Payload>? headers, PropagationContext ctx)
        {
            var carrier = new Dictionary<string, string>();
            Options.Propagator.Inject(ctx, carrier, (d, k, v) => d[k] = v);
            headers ??= new Dictionary<string, Payload>();
            // Do not encode headers, that is done externally
            headers[Options.HeaderKey] = DataConverter.Default.PayloadConverter.ToPayload(carrier);
            return headers;
        }

        /// <summary>
        /// Deserialize Temporal headers to OTel context.
        /// </summary>
        /// <param name="headers">Headers to deserialize from.</param>
        /// <returns>OTel context if any on the headers.</returns>
        protected virtual PropagationContext? HeadersToContext(
            IReadOnlyDictionary<string, Payload>? headers)
        {
            if (headers == null || !headers.TryGetValue(Options.HeaderKey, out var tracerPayload))
            {
                return null;
            }
            var carrier = DataConverter.Default.PayloadConverter.ToValue<Dictionary<string, string>>(tracerPayload);
            return Options.Propagator.Extract(default, carrier, (d, k) =>
                d.TryGetValue(k, out var value) ? new[] { value } : Array.Empty<string>());
        }

        /// <summary>
        /// Create tag collection for the given workflow ID.
        /// </summary>
        /// <param name="workflowId">Workflow ID.</param>
        /// <returns>Tags.</returns>
        protected virtual IEnumerable<KeyValuePair<string, object?>> CreateWorkflowTags(
            string workflowId)
        {
            if (Options.TagNameWorkflowId is string name)
            {
                return new KeyValuePair<string, object?>[] { new(name, workflowId) };
            }
            return Enumerable.Empty<KeyValuePair<string, object?>>();
        }

        /// <summary>
        /// Create tag collection from the current workflow environment. Must be called within a
        /// workflow.
        /// </summary>
        /// <returns>Tags.</returns>
        protected virtual IEnumerable<KeyValuePair<string, object?>> CreateInWorkflowTags()
        {
            var info = Workflow.Info;
            // Can't just append to other enumerable, Append is >= 4.7.1 only
            var ret = new List<KeyValuePair<string, object?>>(2);
            if (Options.TagNameWorkflowId is string wfName)
            {
                ret.Add(new(wfName, info.WorkflowId));
            }
            if (Options.TagNameRunId is string runName)
            {
                ret.Add(new(runName, info.RunId));
            }
            return ret;
        }

        /// <summary>
        /// Create tag collection from the current activity environment. Must be called within an
        /// activity.
        /// </summary>
        /// <returns>Tags.</returns>
        protected virtual IEnumerable<KeyValuePair<string, object?>> CreateInActivityTags()
        {
            var info = ActivityExecutionContext.Current.Info;
            var ret = new List<KeyValuePair<string, object?>>(3);
            if (Options.TagNameWorkflowId is string wfName)
            {
                ret.Add(new(wfName, info.WorkflowId));
            }
            if (Options.TagNameRunId is string runName)
            {
                ret.Add(new(runName, info.WorkflowRunId));
            }
            if (Options.TagNameActivityId is string actName)
            {
                ret.Add(new(actName, info.ActivityId));
            }
            return ret;
        }

        private static void RecordExceptionWithStatus(Activity? activity, Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.RecordException(exception);
        }

        private sealed class ClientOutbound : ClientOutboundInterceptor
        {
            private readonly TracingInterceptor root;

            internal ClientOutbound(TracingInterceptor root, ClientOutboundInterceptor next)
                : base(next) => this.root = root;

            public override async Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
                StartWorkflowInput input)
            {
                var namePrefix = input.Options.StartSignal == null ? "StartWorkflow" : "SignalWithStartWorkflow";
                using (var activity = ClientSource.StartActivity(
                    $"{namePrefix}:{input.Workflow}",
                    kind: ActivityKind.Client,
                    parentContext: default,
                    tags: root.CreateWorkflowTags(input.Options.Id!)))
                {
                    if (HeadersFromContext(input.Headers) is Dictionary<string, Payload> headers)
                    {
                        input = input with { Headers = headers };
                    }
                    return await base.StartWorkflowAsync<TWorkflow, TResult>(input).ConfigureAwait(false);
                }
            }

            public override async Task SignalWorkflowAsync(SignalWorkflowInput input)
            {
                using (var activity = ClientSource.StartActivity(
                    $"SignalWorkflow:{input.Signal}",
                    kind: ActivityKind.Client,
                    parentContext: default,
                    tags: root.CreateWorkflowTags(input.Id)))
                {
                    if (HeadersFromContext(input.Headers) is Dictionary<string, Payload> headers)
                    {
                        input = input with { Headers = headers };
                    }
                    await base.SignalWorkflowAsync(input).ConfigureAwait(false);
                }
            }

            public override async Task<TResult> QueryWorkflowAsync<TResult>(QueryWorkflowInput input)
            {
                using (var activity = ClientSource.StartActivity(
                    $"QueryWorkflow:{input.Query}",
                    kind: ActivityKind.Client,
                    parentContext: default,
                    tags: root.CreateWorkflowTags(input.Id)))
                {
                    if (HeadersFromContext(input.Headers) is Dictionary<string, Payload> headers)
                    {
                        input = input with { Headers = headers };
                    }
                    return await base.QueryWorkflowAsync<TResult>(input).ConfigureAwait(false);
                }
            }

            public override async Task<WorkflowUpdateHandle<TResult>> StartWorkflowUpdateAsync<TResult>(
                StartWorkflowUpdateInput input)
            {
                using (var activity = ClientSource.StartActivity(
                    $"UpdateWorkflow:{input.Update}",
                    kind: ActivityKind.Client,
                    parentContext: default,
                    tags: root.CreateWorkflowTags(input.Id)))
                {
                    if (HeadersFromContext(input.Headers) is Dictionary<string, Payload> headers)
                    {
                        input = input with { Headers = headers };
                    }
                    return await base.StartWorkflowUpdateAsync<TResult>(input).ConfigureAwait(false);
                }
            }

            /// <summary>
            /// Serialize current context to headers if one exists.
            /// </summary>
            /// <param name="headers">Headers to mutate.</param>
            /// <returns>Created/updated headers if any changes were made. Returns null if no
            /// context present regardless of given parameter.</returns>
            private IDictionary<string, Payload>? HeadersFromContext(
                IDictionary<string, Payload>? headers)
            {
                if (Activity.Current?.Context is ActivityContext ctx)
                {
                    return root.HeadersFromContext(headers, new(ctx, Baggage.Current));
                }
                return null;
            }
        }

        private sealed class WorkflowInbound : WorkflowInboundInterceptor
        {
            private readonly TracingInterceptor root;

            internal WorkflowInbound(TracingInterceptor root, WorkflowInboundInterceptor next)
                : base(next) => this.root = root;

            public override void Init(WorkflowOutboundInterceptor outbound) =>
                base.Init(new WorkflowOutbound(root, outbound));

            public override async Task<object?> ExecuteWorkflowAsync(ExecuteWorkflowInput input)
            {
                var prevBaggage = Baggage.Current;
                WorkflowDiagnosticActivity? remoteActivity = null;
                if (root.HeadersToContext(Workflow.Info.Headers) is PropagationContext ctx)
                {
                    Baggage.Current = ctx.Baggage;
                    remoteActivity = WorkflowDiagnosticActivity.AttachFromContext(ctx.ActivityContext);
                }
                try
                {
                    using (WorkflowsSource.TrackWorkflowDiagnosticActivity(
                        name: $"RunWorkflow:{Workflow.Info.WorkflowType}",
                        kind: ActivityKind.Server,
                        tags: root.CreateInWorkflowTags(),
                        inheritParentTags: false))
                    {
                        try
                        {
                            var res = await base.ExecuteWorkflowAsync(input).ConfigureAwait(true);
                            WorkflowsSource.TrackWorkflowDiagnosticActivity(
                                name: $"CompleteWorkflow:{Workflow.Info.WorkflowType}").
                                Dispose();
                            return res;
                        }
                        catch (Exception e)
                        {
                            ApplyWorkflowException(e);
                            throw;
                        }
                    }
                }
                finally
                {
                    remoteActivity?.Dispose();
                    Baggage.Current = prevBaggage;
                }
            }

            public override async Task HandleSignalAsync(HandleSignalInput input)
            {
                var prevBaggage = Baggage.Current;
                WorkflowDiagnosticActivity? remoteActivity = null;
                if (root.HeadersToContext(Workflow.Info.Headers) is PropagationContext ctx)
                {
                    Baggage.Current = ctx.Baggage;
                    remoteActivity = WorkflowDiagnosticActivity.AttachFromContext(ctx.ActivityContext);
                }
                try
                {
                    using (WorkflowsSource.TrackWorkflowDiagnosticActivity(
                        name: $"HandleSignal:{input.Signal}",
                        kind: ActivityKind.Server,
                        tags: root.CreateInWorkflowTags(),
                        links: LinksFromHeaders(input.Headers),
                        inheritParentTags: false))
                    {
                        try
                        {
                            await base.HandleSignalAsync(input).ConfigureAwait(true);
                        }
                        catch (Exception e)
                        {
                            ApplyWorkflowException(e);
                            throw;
                        }
                    }
                }
                finally
                {
                    remoteActivity?.Dispose();
                    Baggage.Current = prevBaggage;
                }
            }

            public override object? HandleQuery(HandleQueryInput input)
            {
                var prevBaggage = Baggage.Current;
                WorkflowDiagnosticActivity? remoteActivity = null;
                if (root.HeadersToContext(Workflow.Info.Headers) is PropagationContext ctx)
                {
                    Baggage.Current = ctx.Baggage;
                    remoteActivity = WorkflowDiagnosticActivity.AttachFromContext(ctx.ActivityContext);
                }
                try
                {
                    using (var activity = WorkflowsSource.TrackWorkflowDiagnosticActivity(
                        name: $"HandleQuery:{input.Query}",
                        kind: ActivityKind.Server,
                        tags: root.CreateInWorkflowTags(),
                        links: LinksFromHeaders(input.Headers),
                        evenOnReplay: true,
                        inheritParentTags: false))
                    {
                        try
                        {
                            return base.HandleQuery(input);
                        }
                        catch (Exception e)
                        {
                            RecordExceptionWithStatus(activity.Activity, e);
                            throw;
                        }
                    }
                }
                finally
                {
                    remoteActivity?.Dispose();
                    Baggage.Current = prevBaggage;
                }
            }

            public override void ValidateUpdate(HandleUpdateInput input)
            {
                var prevBaggage = Baggage.Current;
                WorkflowDiagnosticActivity? remoteActivity = null;
                if (root.HeadersToContext(Workflow.Info.Headers) is PropagationContext ctx)
                {
                    Baggage.Current = ctx.Baggage;
                    remoteActivity = WorkflowDiagnosticActivity.AttachFromContext(ctx.ActivityContext);
                }
                try
                {
                    using (var activity = WorkflowsSource.TrackWorkflowDiagnosticActivity(
                        name: $"ValidateUpdate:{input.Update}",
                        kind: ActivityKind.Server,
                        tags: root.CreateInWorkflowTags(),
                        links: LinksFromHeaders(input.Headers),
                        inheritParentTags: false))
                    {
                        try
                        {
                            base.ValidateUpdate(input);
                        }
                        catch (Exception e)
                        {
                            RecordExceptionWithStatus(activity.Activity, e);
                            throw;
                        }
                    }
                }
                finally
                {
                    remoteActivity?.Dispose();
                    Baggage.Current = prevBaggage;
                }
            }

            public override async Task<object?> HandleUpdateAsync(HandleUpdateInput input)
            {
                var prevBaggage = Baggage.Current;
                WorkflowDiagnosticActivity? remoteActivity = null;
                if (root.HeadersToContext(Workflow.Info.Headers) is PropagationContext ctx)
                {
                    Baggage.Current = ctx.Baggage;
                    remoteActivity = WorkflowDiagnosticActivity.AttachFromContext(ctx.ActivityContext);
                }
                try
                {
                    using (WorkflowsSource.TrackWorkflowDiagnosticActivity(
                        name: $"HandleUpdate:{input.Update}",
                        kind: ActivityKind.Server,
                        tags: root.CreateInWorkflowTags(),
                        links: LinksFromHeaders(input.Headers),
                        inheritParentTags: false))
                    {
                        try
                        {
                            return await base.HandleUpdateAsync(input).ConfigureAwait(true);
                        }
                        catch (Exception e)
                        {
                            // We make a new span for failure same as signal/workflow handlers do
                            var namePrefix = e is FailureException || e is OperationCanceledException ?
                                "CompleteUpdate" : "WorkflowTaskFailure";
                            WorkflowsSource.TrackWorkflowDiagnosticActivity(
                                name: $"{namePrefix}:{input.Update}",
                                updateActivity: act => RecordExceptionWithStatus(act, e)).
                                Dispose();
                            throw;
                        }
                    }
                }
                finally
                {
                    remoteActivity?.Dispose();
                    Baggage.Current = prevBaggage;
                }
            }

            private static void ApplyWorkflowException(Exception e)
            {
                // Continue as new is not an exception worth of recording
                if (e is ContinueAsNewException)
                {
                    return;
                }
                // Activity name depends on whether it is completing the workflow with failure or is
                // a task failure. We choose to make a new span instead of putting the task failure
                // on the existing activity because there may not be an existing activity.
                var namePrefix = e is FailureException || e is OperationCanceledException ?
                    "CompleteWorkflow" : "WorkflowTaskFailure";
                WorkflowsSource.TrackWorkflowDiagnosticActivity(
                    name: $"{namePrefix}:{Workflow.Info.WorkflowType}",
                    updateActivity: act => RecordExceptionWithStatus(act, e)).
                    Dispose();
            }

            private ActivityLink[]? LinksFromHeaders(IReadOnlyDictionary<string, Payload>? headers)
            {
                if (root.HeadersToContext(headers) is PropagationContext ctx)
                {
                    return new[] { new ActivityLink(ctx.ActivityContext) };
                }
                return null;
            }
        }

        private sealed class WorkflowOutbound : WorkflowOutboundInterceptor
        {
            private readonly TracingInterceptor root;

            internal WorkflowOutbound(TracingInterceptor root, WorkflowOutboundInterceptor next)
                : base(next) => this.root = root;

            public override ContinueAsNewException CreateContinueAsNewException(CreateContinueAsNewExceptionInput input)
            {
                // Put current context onto headers
                var headers = root.HeadersFromContext(
                    input.Headers,
                    new(WorkflowDiagnosticActivity.Current?.Context ?? default, Baggage.Current));
                input = input with { Headers = headers };
                return base.CreateContinueAsNewException(input);
            }

            public override Task<TResult> ScheduleActivityAsync<TResult>(
                ScheduleActivityInput input)
            {
                var headers = StartWorkflowActivityOnHeaders(
                    input.Headers, $"StartActivity:{input.Activity}");
                input = input with { Headers = headers };
                return base.ScheduleActivityAsync<TResult>(input);
            }

            public override Task<TResult> ScheduleLocalActivityAsync<TResult>(
                ScheduleLocalActivityInput input)
            {
                var headers = StartWorkflowActivityOnHeaders(
                    input.Headers, $"StartActivity:{input.Activity}");
                input = input with { Headers = headers };
                return base.ScheduleLocalActivityAsync<TResult>(input);
            }

            public override Task SignalChildWorkflowAsync(SignalChildWorkflowInput input)
            {
                var headers = StartWorkflowActivityOnHeaders(
                    input.Headers, $"SignalChildWorkflow:{input.Signal}");
                input = input with { Headers = headers };
                return base.SignalChildWorkflowAsync(input);
            }

            public override Task SignalExternalWorkflowAsync(SignalExternalWorkflowInput input)
            {
                var headers = StartWorkflowActivityOnHeaders(
                    input.Headers, $"SignalExternalWorkflow:{input.Signal}");
                input = input with { Headers = headers };
                return base.SignalExternalWorkflowAsync(input);
            }

            public override Task<ChildWorkflowHandle<TWorkflow, TResult>> StartChildWorkflowAsync<TWorkflow, TResult>(
                StartChildWorkflowInput input)
            {
                var headers = StartWorkflowActivityOnHeaders(
                    input.Headers, $"StartChildWorkflow:{input.Workflow}");
                input = input with { Headers = headers };
                return base.StartChildWorkflowAsync<TWorkflow, TResult>(input);
            }

            // TODO(cretz): Document this only returns non-null headers if changed
            private IDictionary<string, Payload> StartWorkflowActivityOnHeaders(
                IDictionary<string, Payload>? headers, string name)
            {
                using (WorkflowsSource.TrackWorkflowDiagnosticActivity(
                    name: name,
                    kind: ActivityKind.Client))
                {
                    return root.HeadersFromContext(
                        headers,
                        new(WorkflowDiagnosticActivity.Current?.Context ?? default, Baggage.Current));
                }
            }
        }

        private sealed class ActivityInbound : ActivityInboundInterceptor
        {
            private readonly TracingInterceptor root;

            internal ActivityInbound(TracingInterceptor root, ActivityInboundInterceptor next)
                : base(next) => this.root = root;

            public override async Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
            {
                var prevBaggage = Baggage.Current;
                ActivityContext parentContext = default;
                if (root.HeadersToContext(input.Headers) is PropagationContext ctx)
                {
                    Baggage.Current = ctx.Baggage;
                    parentContext = ctx.ActivityContext;
                }
                try
                {
                    using (var activity = ActivitiesSource.StartActivity(
                        $"RunActivity:{input.Activity.Name}",
                        kind: ActivityKind.Server,
                        parentContext: parentContext,
                        tags: root.CreateInActivityTags()))
                    {
                        try
                        {
                            return await base.ExecuteActivityAsync(input).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            RecordExceptionWithStatus(activity, e);
                            throw;
                        }
                    }
                }
                finally
                {
                    Baggage.Current = prevBaggage;
                }
            }
        }
    }
}