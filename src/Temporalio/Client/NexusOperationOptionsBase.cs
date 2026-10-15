using System;
using Temporalio.Api.Enums.V1;
using Temporalio.Common;

namespace Temporalio.Client
{
    /// <summary>
    /// Common settings for starting a standalone Nexus operation.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public abstract class NexusOperationOptionsBase : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationOptionsBase"/> class.
        /// </summary>
        protected NexusOperationOptionsBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationOptionsBase"/> class
        /// with the common settings from another options instance.
        /// </summary>
        /// <param name="options">The settings to copy.</param>
        protected NexusOperationOptionsBase(NexusOperationOptionsBase options)
        {
            Id = options.Id;
            ScheduleToCloseTimeout = options.ScheduleToCloseTimeout;
            Summary = options.Summary;
            SearchAttributes = options.SearchAttributes;
            IdReusePolicy = options.IdReusePolicy;
            IdConflictPolicy = options.IdConflictPolicy;
            Rpc = (RpcOptions?)options.Rpc?.Clone();
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
        /// Creates a shallow copy of these options and copies the RPC options.
        /// </summary>
        /// <returns>The copied options.</returns>
        public virtual object Clone()
        {
            var copy = (NexusOperationOptionsBase)MemberwiseClone();
            copy.Rpc = (RpcOptions?)Rpc?.Clone();
            return copy;
        }
    }
}
