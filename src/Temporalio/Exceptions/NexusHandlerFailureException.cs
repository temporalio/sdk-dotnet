using System;
using NexusRpc.Handlers;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Failure from a Nexus handler.
    /// </summary>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public class NexusHandlerFailureException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusHandlerFailureException"/> class.
        /// </summary>
        /// <param name="failure">Proto failure.</param>
        /// <param name="inner">Inner exception.</param>
        internal protected NexusHandlerFailureException(Failure failure, Exception? inner)
            : base(failure, inner)
        {
            if (failure.NexusHandlerFailureInfo == null)
            {
                throw new ArgumentException("Missing handler failure info");
            }
            if (HandlerException.TryParseErrorType(
                failure.NexusHandlerFailureInfo.Type, out var errorType))
            {
                ErrorType = errorType;
            }
        }

        /// <summary>
        /// Gets the underlying protobuf failure object.
        /// </summary>
        public new Failure Failure => base.Failure!;

        /// <summary>
        /// Gets the error type.
        /// </summary>
        public HandlerErrorType ErrorType { get; private init; } = HandlerErrorType.Unknown;

        /// <summary>
        /// Gets the raw error type as a string. Most users should use <see cref="ErrorType"/>.
        /// </summary>
        public string RawErrorType => Failure.NexusHandlerFailureInfo.Type;

        /// <summary>
        /// Gets the error retry behavior.
        /// </summary>
        public NexusHandlerErrorRetryBehavior ErrorRetryBehavior => Failure.NexusHandlerFailureInfo.RetryBehavior;
    }
}