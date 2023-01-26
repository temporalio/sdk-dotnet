using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="WorkflowHandle.CancelAsync" />
    /// and other overloads.
    /// </summary>
    public class WorkflowCancelOptions : ICloneable
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
            var copy = (WorkflowSignalOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}