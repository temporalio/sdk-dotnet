using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace Temporalio.Worker
{
    internal record WorkflowWorkerOptions(
        Bridge.Worker BridgeWorker,
        string Namespace,
        string TaskQueue,
        IList<WorkflowDefinition> Workflows,
        Converters.DataConverter DataConverter,
        IEnumerable<Type> WorkflowInboundInterceptorTypes,
        ILoggerFactory LoggerFactory,
        Func<WorkflowInstanceDetails, IWorkflowInstance>? WorkflowInstanceFactory,
        bool DebugMode,
        bool DisableWorkflowTracingEventListener,
        WorkflowStackTrace WorkflowStackTrace);
}