using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for executing an update on a <see cref="WorkflowHandle" />.
    /// </summary>
    /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
    public class WorkflowUpdateOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateOptions"/> class.
        /// </summary>
        public WorkflowUpdateOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateOptions"/> class.
        /// </summary>
        /// <param name="id">Update ID.</param>
        public WorkflowUpdateOptions(string id) => Id = id;

        /// <summary>
        /// Gets or sets the unique workflow identifier. If not set, this is defaulted to a GUID.
        /// This must be unique within the scope of a workflow execution (i.e. namespace +
        /// workflow ID + run ID).
        /// </summary>
        public string? Id { get; set; }

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
            var copy = (WorkflowUpdateOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}