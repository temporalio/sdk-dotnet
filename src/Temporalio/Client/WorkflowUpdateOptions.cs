using System;
using Temporalio.Api.Enums.V1;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for starting an update on a <see cref="WorkflowHandle" />.
    /// </summary>
    /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
    public class WorkflowUpdateOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the unique identifier for the update. This is optional and is defaulted to
        /// a GUID if not set. This must be unique within the scope of a workflow execution (i.e.
        /// namespace + workflow ID + run ID).
        /// </summary>
        public string? UpdateID { get; set; }

        /// <summary>
        /// Gets or sets RPC options for starting the workflow.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Gets or sets the stage to wait for on start. Internal only.
        /// </summary>
        internal UpdateWorkflowExecutionLifecycleStage WaitForStage { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (WorkflowUpdateOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}