using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="WorkflowHandle.DescribeAsync" />.
    /// </summary>
    public class WorkflowDescribeOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets RPC options for starting the workflow.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (WorkflowDescribeOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}