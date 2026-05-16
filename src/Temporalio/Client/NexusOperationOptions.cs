using System;
using Temporalio.Api.Enums.V1;
using Temporalio.Common;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for starting a standalone Nexus operation. <see cref="Id" /> is required.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusOperationOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationOptions"/> class.
        /// </summary>
        public NexusOperationOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationOptions"/> class.
        /// </summary>
        /// <param name="id">Operation ID.</param>
        public NexusOperationOptions(string id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets or sets the unique operation identifier. This is required.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the schedule-to-close timeout for this operation.
        /// </summary>
        public TimeSpan? ScheduleToCloseTimeout { get; set; }

        /// <summary>
        /// Gets or sets a single-line fixed summary for this operation that may appear in
        /// UI/CLI. This can be in single-line Temporal markdown format.
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Gets or sets the search attributes for the operation.
        /// </summary>
        public SearchAttributeCollection? SearchAttributes { get; set; }

        /// <summary>
        /// Gets or sets whether to allow re-using an operation ID from a previously *closed*
        /// operation. Default is <see cref="NexusOperationIdReusePolicy.AllowDuplicate" />.
        /// </summary>
        public NexusOperationIdReusePolicy IdReusePolicy { get; set; } = NexusOperationIdReusePolicy.AllowDuplicate;

        /// <summary>
        /// Gets or sets how already-running operations of the same ID are treated. Default is
        /// <see cref="NexusOperationIdConflictPolicy.Fail" />.
        /// </summary>
        public NexusOperationIdConflictPolicy IdConflictPolicy { get; set; } = NexusOperationIdConflictPolicy.Fail;

        /// <summary>
        /// Gets or sets RPC options for starting the operation.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (NexusOperationOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
