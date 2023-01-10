using System;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception representing a server failure.
    /// </summary>
    public class ServerFailureException : FailureException
    {
        /// <inheritdoc />
        protected internal ServerFailureException(Failure failure, Exception? inner)
            : base(failure, inner)
        {
            if (failure.ServerFailureInfo == null)
            {
                throw new ArgumentException("Missing server failure info");
            }
        }

        /// <inheritdoc />
        public new Failure Failure => base.Failure!;

        /// <summary>
        /// Whether this exception is non-retryable.
        /// </summary>
        public bool NonRetryable => Failure!.ServerFailureInfo.NonRetryable;
    }
}
