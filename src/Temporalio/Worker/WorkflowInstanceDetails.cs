using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Temporalio.Bridge.Api.WorkflowActivation;
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
    /// <param name="Start">Start attributes for the workflow.</param>
    /// <param name="Interceptors">Interceptors.</param>
    /// <param name="PayloadConverter">Payload converter.</param>
    /// <param name="FailureConverter">Failure converter.</param>
    /// <param name="LoggerFactory">Logger factory.</param>
    /// <param name="DisableTracingEvents">Whether tracing events are disabled.</param>
    /// <param name="WorkflowStackTrace">Option for workflow stack trace.</param>
    /// <remarks>
    /// This is built to be easily serializable in case we do want a sandbox one day.
    /// </remarks>
    internal record WorkflowInstanceDetails(
        string Namespace,
        string TaskQueue,
        WorkflowDefinition Definition,
        WorkflowActivation InitialActivation,
        StartWorkflow Start,
        IReadOnlyCollection<Interceptors.IWorkerInterceptor> Interceptors,
        IPayloadConverter PayloadConverter,
        IFailureConverter FailureConverter,
        ILoggerFactory LoggerFactory,
        bool DisableTracingEvents,
        WorkflowStackTrace WorkflowStackTrace);
}