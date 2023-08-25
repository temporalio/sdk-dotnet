using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Workflows;

namespace Temporalio.Worker
{
    internal record WorkflowWorkerOptions(
        Bridge.Worker BridgeWorker,
        string Namespace,
        string TaskQueue,
        IList<WorkflowDefinition> Workflows,
        Converters.DataConverter DataConverter,
        IReadOnlyCollection<Interceptors.IWorkerInterceptor> Interceptors,
        ILoggerFactory LoggerFactory,
        Func<WorkflowInstanceDetails, IWorkflowInstance> WorkflowInstanceFactory,
        bool DebugMode,
        bool DisableWorkflowTracingEventListener,
        WorkflowStackTrace WorkflowStackTrace,
        Action<WorkflowInstance> OnTaskStarting,
        Action<WorkflowInstance, Exception?> OnTaskCompleted,
        Lazy<IMetricMeter> RuntimeMetricMeter);
}