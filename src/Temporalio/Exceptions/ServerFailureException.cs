using System;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception representing a server failure.
    /// </summary>
    public class ServerFailureException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerFailureException"/> class.
        /// </summary>
        /// <param name="failure">Underlying proto failure.</param>
        /// <param name="inner">Inner exception if any.</param>
        internal protected ServerFailureException(Failure failure, Exception? inner)
            : base(failure, inner)
        {
            if (failure.ServerFailureInfo == null)
            {
                throw new ArgumentException("Missing server failure info");
            }
        }

        /// <summary>
        /// Gets the underlying protobuf failure object.
        /// </summary>
        public new Failure Failure => base.Failure!;

        /// <summary>
        /// Gets a value indicating whether this exception is non-retryable.
        /// </summary>
        public bool NonRetryable => Failure!.ServerFailureInfo.NonRetryable;
    }
}
