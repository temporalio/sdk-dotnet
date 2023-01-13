using System;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception representing a terminated workflow.
    /// </summary>
    public class TerminatedFailureException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TerminatedFailureException"/> class.
        /// </summary>
        /// <param name="failure">Underlying proto failure.</param>
        /// <param name="inner">Inner exception if any.</param>
        internal protected TerminatedFailureException(Failure failure, Exception? inner)
            : base(failure, inner)
        {
            if (failure.TerminatedFailureInfo == null)
            {
                throw new ArgumentException("Missing terminated failure info");
            }
        }

        /// <summary>
        /// Gets the underlying protobuf failure object.
        /// </summary>
        public new Failure Failure => base.Failure!;
    }
}
