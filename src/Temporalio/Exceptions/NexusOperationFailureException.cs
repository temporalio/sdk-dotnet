using System;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    public class NexusOperationFailureException : FailureException
    {
        internal protected NexusOperationFailureException(Failure failure, Exception? inner)
            : base(failure, inner)
        {
            if (failure.NexusOperationExecutionFailureInfo == null)
            {
                throw new ArgumentException("Missing operation failure info");
            }
        }

        public new Failure Failure => base.Failure!;

        public string Endpoint => Failure.NexusOperationExecutionFailureInfo.Endpoint;

        public string Service => Failure.NexusOperationExecutionFailureInfo.Service;

        public string Operation => Failure.NexusOperationExecutionFailureInfo.Operation;

        public string OperationToken => Failure.NexusOperationExecutionFailureInfo.OperationToken;
    }
}