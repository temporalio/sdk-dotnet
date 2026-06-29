using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using NexusRpc.Handlers;
using Temporalio.Client;
using Temporalio.Common;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Nexus operation context available in Temporal-powered Nexus operations via async local
    /// <see cref="Current"/>.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class NexusOperationExecutionContext
    {
        private readonly Lazy<MetricMeter> metricMeter;
        private readonly ITemporalClient? temporalClient;

        // Response links returned by outbound RPCs the operation handler issues (such as
        // SignalWorkflowExecutionResponse.Link or SignalWithStartWorkflowExecutionResponse.SignalLink).
        // One entry per outbound RPC that returned a link. Drained by the task handler when building
        // the StartOperationResponse so each RPC the handler issued gets a corresponding link on the
        // caller workflow's history event. A ConcurrentQueue handles enqueues from outbound RPCs the
        // handler may issue in parallel; the ResponseLinks getter snapshots it.
        private readonly ConcurrentQueue<Api.Common.V1.Link> responseLinks = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationExecutionContext"/> class.
        /// </summary>
        /// <param name="handlerContext">Nexus Handler context.</param>
        /// <param name="info">Operation info.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="runtimeMetricMeter">Metric meter.</param>
        /// <param name="temporalClient">Temporal client.</param>
        internal NexusOperationExecutionContext(
            OperationContext handlerContext,
            NexusOperationInfo info,
            ILogger logger,
            Lazy<MetricMeter> runtimeMetricMeter,
            ITemporalClient? temporalClient)
        {
            HandlerContext = handlerContext;
            Info = info;
            Logger = logger;
            metricMeter = new(() =>
            {
                return runtimeMetricMeter.Value.WithTags(new Dictionary<string, object>()
                {
                    { "namespace", info.Namespace },
                    { "nexus_service", handlerContext.Service },
                    { "nexus_operation", handlerContext.Operation },
                    { "task_queue", info.TaskQueue },
                });
            });
            this.temporalClient = temporalClient;
        }

        /// <summary>
        /// Gets a value indicating whether there is a current Nexus operation context.
        /// </summary>
        public static bool HasCurrent => AsyncLocalCurrent.Value != null;

        /// <summary>
        /// Gets the current Nexus operation context.
        /// </summary>
        /// <exception cref="InvalidOperationException">If no context is available.</exception>
        public static NexusOperationExecutionContext Current => AsyncLocalCurrent.Value ??
            throw new InvalidOperationException("No current Nexus operation context");

        /// <summary>
        /// Gets the Temporal-specific info for this operation.
        /// </summary>
        public NexusOperationInfo Info { get; private init; }

        /// <summary>
        /// Gets the logger scoped to this operation.
        /// </summary>
        public ILogger Logger { get; private init; }

        /// <summary>
        /// Gets the metric meter for this operation with operation-specific tags. Note, this is
        /// lazily created for each operation execution.
        /// </summary>
        public MetricMeter MetricMeter => metricMeter.Value;

        /// <summary>
        /// Gets the Temporal client for use within the operation.
        /// </summary>
        /// <exception cref="InvalidOperationException">If the client the worker was created with is
        /// not an <c>ITemporalClient</c>.</exception>
        public ITemporalClient TemporalClient => temporalClient ??
            throw new InvalidOperationException("No Temporal client available. " +
                "This could either be a test environment without a client set, or the worker was " +
                "created in an advanced way without an ITemporalClient instance.");

        /// <summary>
        /// Gets the async local for this context.
        /// </summary>
        internal static AsyncLocal<NexusOperationExecutionContext?> AsyncLocalCurrent { get; } = new();

        /// <summary>
        /// Gets the Nexus context.
        /// </summary>
        internal OperationContext HandlerContext { get; private init; }

        /// <summary>
        /// Gets or sets the <c>common.v1.Link</c>s extracted from the inbound Nexus task so they can
        /// be attached to RPCs issued by the operation handler. Empty if none. Links whose variant
        /// cannot be converted by <see cref="ProtoLinkExtensions"/> are dropped during inbound
        /// conversion.
        /// </summary>
        internal IReadOnlyCollection<Api.Common.V1.Link> RequestLinks { get; set; } =
            Array.Empty<Api.Common.V1.Link>();

        /// <summary>
        /// Gets the response links accumulated from every outbound RPC the handler issued. Entries
        /// are accumulated while the operation handler runs and are drained afterward by the task
        /// handler when building the StartOperationResponse.
        /// </summary>
        internal IReadOnlyList<Api.Common.V1.Link> ResponseLinks => responseLinks.ToList();

        /// <summary>
        /// Append a link returned by an outbound RPC the operation handler issued (e.g. signal,
        /// signalWithStart, start). The task handler drains the accumulated links when building the
        /// operation's StartOperationResponse, dropping any whose variant it cannot convert. Null
        /// links are ignored.
        /// </summary>
        /// <param name="link">Response link to add.</param>
        /// <returns><c>true</c> if the link was added; <c>false</c> if it was null.</returns>
        internal bool TryAddResponseLink(Api.Common.V1.Link? link)
        {
            if (link == null)
            {
                return false;
            }
            responseLinks.Enqueue(link);
            return true;
        }
    }
}
