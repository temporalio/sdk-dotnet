using System;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception that is thrown by the server to workflows when an operation fails.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class NexusOperationFailureException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationFailureException"/> class.
        /// </summary>
        /// <param name="failure">Underlying proto failure.</param>
        /// <param name="inner">Inner exception if any.</param>
        internal protected NexusOperationFailureException(Failure failure, Exception? inner)
            : base(failure, inner)
        {
            if (failure.NexusOperationExecutionFailureInfo == null)
            {
                throw new ArgumentException("Missing operation failure info");
            }
        }

        /// <summary>
        /// Gets the underlying protobuf failure object.
        /// </summary>
        public new Failure Failure => base.Failure!;

        /// <summary>
        /// Gets the endpoint name.
        /// </summary>
        public string Endpoint => Failure.NexusOperationExecutionFailureInfo.Endpoint;

        /// <summary>
        /// Gets the service name.
        /// </summary>
        public string Service => Failure.NexusOperationExecutionFailureInfo.Service;

        /// <summary>
        /// Gets the operation name.
        /// </summary>
        public string Operation => Failure.NexusOperationExecutionFailureInfo.Operation;

        /// <summary>
        /// Gets the operation token. May be empty if the operation failed synchronously.
        /// </summary>
        public string OperationToken => Failure.NexusOperationExecutionFailureInfo.OperationToken;
    }
}