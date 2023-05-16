using System;
using System.Collections.Generic;
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
    /// <param name="PayloadConverterType">Payload converter type to instantiate.</param>
    /// <param name="FailureConverterType">Failure converter type to instantiate.</param>
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
        IEnumerable<Interceptors.IWorkerInterceptor> Interceptors,
        Type PayloadConverterType,
        Type FailureConverterType,
        bool DisableTracingEvents,
        WorkflowStackTrace WorkflowStackTrace)
    {
        /// <summary>
        /// Gets a created payload converter.
        /// </summary>
        /// <remarks>
        /// Internal because it's not serializable.
        /// </remarks>
        internal IPayloadConverter PayloadConverter { get; init; } =
            (IPayloadConverter)Activator.CreateInstance(PayloadConverterType)!;

        /// <summary>
        /// Gets a created failure converter.
        /// </summary>
        /// <remarks>
        /// Internal because it's not serializable.
        /// </remarks>
        internal IFailureConverter FailureConverter { get; init; } =
            (IFailureConverter)Activator.CreateInstance(FailureConverterType)!;
    }
}