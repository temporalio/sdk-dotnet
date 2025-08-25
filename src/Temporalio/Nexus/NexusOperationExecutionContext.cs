using System;
using System.Collections.Generic;
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
                    { "task_queue", info.TaskQueue },
                    { "service", handlerContext.Service },
                    { "operation", handlerContext.Operation },
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
    }
}