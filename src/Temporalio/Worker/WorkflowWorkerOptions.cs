using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Temporalio.Worker
{
    internal record WorkflowWorkerOptions(
        Bridge.Worker BridgeWorker,
        string Namespace,
        string TaskQueue,
        IList<Type> Workflows,
        IList<Workflows.WorkflowDefinition> AdditionalWorkflowDefinitions,
        Converters.DataConverter DataConverter,
        IEnumerable<Type> WorkflowInboundInterceptorTypes,
        ILoggerFactory LoggerFactory,
        Func<WorkflowInstanceDetails, IWorkflowInstance>? WorkflowInstanceFactory,
        bool DebugMode,
        bool DisableWorkflowTracingEventListener,
        WorkflowStackTrace WorkflowStackTrace);
}