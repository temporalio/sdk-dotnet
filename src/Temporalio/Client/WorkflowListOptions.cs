#if NETCOREAPP3_0_OR_GREATER
using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="ITemporalClient.ListWorkflowsAsync" />.
    /// </summary>
    public class WorkflowListOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets RPC options for listing workflows.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of workflows to return. A zero value means no limit.
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (WorkflowListOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
#endif
