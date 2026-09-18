using System.Collections.Generic;
using Temporalio.Api.Sdk.V1;
using Temporalio.Exceptions;
using Temporalio.Worker.Interceptors;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Exception thrown by a workflow to continue as new. Use
    /// <c>Workflow.CreateContinueAsNewException</c> to create.
    /// </summary>
    public class ContinueAsNewException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContinueAsNewException"/> class.
        /// </summary>
        /// <param name="input">Continue as new input.</param>
        /// <param name="eventGroupMarkers">Markers captured at request time.</param>
        internal ContinueAsNewException(
            CreateContinueAsNewExceptionInput input,
            IReadOnlyList<EventGroupMarker> eventGroupMarkers)
            : base("Continue as new")
        {
            Input = input;
            EventGroupMarkers = eventGroupMarkers;
        }

        /// <summary>
        /// Gets the continue as new input.
        /// </summary>
        internal CreateContinueAsNewExceptionInput Input { get; private init; }

        /// <summary>
        /// Gets Event Group markers captured when the continue-as-new was requested.
        /// </summary>
        internal IReadOnlyList<EventGroupMarker> EventGroupMarkers { get; private init; }
    }
}