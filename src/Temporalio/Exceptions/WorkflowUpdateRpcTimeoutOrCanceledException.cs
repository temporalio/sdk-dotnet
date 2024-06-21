using System;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception representing when an update RPC timeout or cancellation. This is not to be
    /// confused with an update itself timing out or being canceled, this is just the call.
    /// </summary>
    public class WorkflowUpdateRpcTimeoutOrCanceledException : RpcTimeoutOrCanceledException
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="WorkflowUpdateRpcTimeoutOrCanceledException"/> class.
        /// </summary>
        /// <param name="inner">Cause of the exception.</param>
        public WorkflowUpdateRpcTimeoutOrCanceledException(Exception? inner)
            : base("Timeout or cancellation waiting for update", inner)
        {
        }
    }
}