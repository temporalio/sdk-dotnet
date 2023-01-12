using System;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception representing a terminated workflow.
    /// </summary>
    public class TerminatedFailureException : FailureException
    {
        /// <inheritdoc />
        protected internal TerminatedFailureException(Failure failure, Exception? inner)
            : base(failure, inner)
        {
            if (failure.TerminatedFailureInfo == null)
            {
                throw new ArgumentException("Missing terminated failure info");
            }
        }

        /// <inheritdoc />
        public new Failure Failure => base.Failure!;
    }
}
