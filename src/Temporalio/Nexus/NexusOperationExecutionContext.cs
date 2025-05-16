using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using NexusRpc.Handler;
using Temporalio.Client;
using Temporalio.Common;

namespace Temporalio.Nexus
{
    public class NexusOperationExecutionContext
    {
        private readonly Lazy<MetricMeter> metricMeter;
        private readonly ITemporalClient? temporalClient;

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

        public static bool HasCurrent => AsyncLocalCurrent.Value != null;

        public static NexusOperationExecutionContext Current => AsyncLocalCurrent.Value ??
            throw new InvalidOperationException("No current Nexus operation context");

        public NexusOperationInfo Info { get; private init; }

        public ILogger Logger { get; private init; }

        public MetricMeter MetricMeter => metricMeter.Value;

        public ITemporalClient TemporalClient => temporalClient ??
            throw new InvalidOperationException("No Temporal client available. " +
                "This could either be a test environment without a client set, or the worker was " +
                "created in an advanced way without an ITemporalClient instance.");

        internal OperationContext HandlerContext { get; private init; }

        internal static AsyncLocal<NexusOperationExecutionContext?> AsyncLocalCurrent { get; } = new();
    }
}