using System;
using System.Collections.Generic;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="WorkflowHandle.TerminateAsync" />.
    /// </summary>
    public class WorkflowTerminateOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the termination details.
        /// </summary>
        public IReadOnlyCollection<object?>? Details { get; set; }

        /// <summary>
        /// Gets or sets RPC options for terminating the workflow.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (WorkflowTerminateOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}