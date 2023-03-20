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
        internal ContinueAsNewException(CreateContinueAsNewExceptionInput input)
            : base("Continue as new") => Input = input;

        /// <summary>
        /// Gets the continue as new input.
        /// </summary>
        internal CreateContinueAsNewExceptionInput Input { get; private init; }
    }
}