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
        /// <param name="details">Inbound failure details.</param>
        internal protected TerminatedFailureException(
            Failure failure, Exception? inner, InboundFailureDetails? details)
            : base(failure, inner)
        {
            if (failure.TerminatedFailureInfo == null)
            {
                throw new ArgumentException("Missing terminated failure info");
            }
            Details = details;
        }

        /// <summary>
        /// Gets the underlying protobuf failure object.
        /// </summary>
        public new Failure Failure => base.Failure!;

        /// <summary>
        /// Gets the details of the termination if present and they came from the client.
        /// </summary>
        /// <remarks>
        /// If present, this will be an instance of <see cref="InboundFailureDetails" /> currently.
        /// </remarks>
        public IFailureDetails? Details { get; protected init; }
    }
}
