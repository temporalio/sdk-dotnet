using System;
using NexusRpc.Handler;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    public class NexusHandlerFailureException : FailureException
    {
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

        public new Failure Failure => base.Failure!;

        public HandlerErrorType ErrorType { get; private init; } = HandlerErrorType.Unknown;

        public string RawErrorType => Failure.NexusHandlerFailureInfo.Type;

        public NexusHandlerErrorRetryBehavior RetryBehavior => Failure.NexusHandlerFailureInfo.RetryBehavior;
    }
}