using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Workflows;

namespace Temporalio.Worker
{
    /// <summary>
    /// Immutable details for a <see cref="WorkflowInstance" />.
    /// </summary>
    /// <param name="Namespace">Workflow namespace.</param>
    /// <param name="TaskQueue">Workflow task queue.</param>
    /// <param name="Definition">Workflow definition.</param>
    /// <param name="InitialActivation">Initial activation for the workflow.</param>
    /// <param name="Init">Start attributes for the workflow.</param>
    /// <param name="Interceptors">Interceptors.</param>
    /// <param name="PayloadConverterNoContext">Payload converter with no context.</param>
    /// <param name="PayloadConverterWorkflowContext">Payload converter with workflow context.</param>
    /// <param name="FailureConverterNoContext">Failure converter with no context.</param>
    /// <param name="FailureConverterWorkflowContext">Failure converter with workflow context.</param>
    /// <param name="LoggerFactory">Logger factory.</param>
    /// <param name="DisableTracingEvents">Whether tracing events are disabled.</param>
    /// <param name="WorkflowStackTrace">Option for workflow stack trace.</param>
    /// <param name="OnTaskStarting">Callback for every instance task start.</param>
    /// <param name="OnTaskCompleted">Callback for every instance task complete.</param>
    /// <param name="RuntimeMetricMeter">Lazy runtime-level metric meter.</param>
    /// <param name="WorkerLevelFailureExceptionTypes">Failure exception types at worker level.</param>
    /// <param name="DisableEagerActivityExecution">Whether to disable eager at the worker level.</param>
    /// <param name="AssertValidLocalActivity">Checks the validity of the local activity and throws if it is invalid.</param>
    internal record WorkflowInstanceDetails(
        string Namespace,
        string TaskQueue,
        WorkflowDefinition Definition,
        WorkflowActivation InitialActivation,
        InitializeWorkflow Init,
        IReadOnlyCollection<Interceptors.IWorkerInterceptor> Interceptors,
        IPayloadConverter PayloadConverterNoContext,
        IPayloadConverter PayloadConverterWorkflowContext,
        IFailureConverter FailureConverterNoContext,
        IFailureConverter FailureConverterWorkflowContext,
        ILoggerFactory LoggerFactory,
        bool DisableTracingEvents,
        WorkflowStackTrace WorkflowStackTrace,
        Action<WorkflowInstance> OnTaskStarting,
        Action<WorkflowInstance, Exception?> OnTaskCompleted,
        Lazy<MetricMeter> RuntimeMetricMeter,
        IReadOnlyCollection<Type>? WorkerLevelFailureExceptionTypes,
        bool DisableEagerActivityExecution,
        Action<string> AssertValidLocalActivity);
}
