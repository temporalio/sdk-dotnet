using System;
using System.Collections.Generic;
using System.Linq;

namespace Temporalio.Client
{
    /// <summary>
    /// A supertype for different ways a task might reach Workflows.
    /// </summary>
    public abstract record TaskReachabilityType
    {
        private TaskReachabilityType()
        {
        }

        /// <summary>
        /// Convert this reachability type to its protobuf equivalent.
        /// </summary>
        /// <returns>Protobuf.</returns>
        internal abstract Api.Enums.V1.TaskReachability ToProto();

        /// <summary>
        /// Instantiate this reachability type from its protobuf equivalent.
        /// </summary>
        /// <param name="proto">The proto.</param>
        /// <returns>This.</returns>
        internal static TaskReachabilityType FromProto(Api.Enums.V1.TaskReachability proto)
        {
            switch (proto)
            {
                case Api.Enums.V1.TaskReachability.NewWorkflows:
                    return new NewWorkflows();
                case Api.Enums.V1.TaskReachability.ExistingWorkflows:
                    return new ExistingWorkflows();
                case Api.Enums.V1.TaskReachability.OpenWorkflows:
                    return new OpenWorkflows();
                case Api.Enums.V1.TaskReachability.ClosedWorkflows:
                    return new ClosedWorkflows();
                default:
                    throw new ArgumentOutOfRangeException(nameof(proto), proto, null);
            }
        }

        /// <summary>
        /// A task can reach new Workflows.
        /// </summary>
        public record NewWorkflows : TaskReachabilityType
        {
            /// <inheritdoc/>
            internal override Api.Enums.V1.TaskReachability ToProto() => Api.Enums.V1.TaskReachability.NewWorkflows;
        }

        /// <summary>
        /// A task can reach open and/or closed Workflows.
        /// </summary>
        public record ExistingWorkflows : TaskReachabilityType
        {
            /// <inheritdoc/>
            internal override Api.Enums.V1.TaskReachability ToProto() =>
                Api.Enums.V1.TaskReachability.ExistingWorkflows;
        }

        /// <summary>
        /// A task can reach open Workflows.
        /// </summary>
        public record OpenWorkflows : TaskReachabilityType
        {
            /// <inheritdoc/>
            internal override Api.Enums.V1.TaskReachability ToProto() => Api.Enums.V1.TaskReachability.OpenWorkflows;
        }

        /// <summary>
        /// A task can reach closed Workflows.
        /// </summary>
        public record ClosedWorkflows : TaskReachabilityType
        {
            /// <inheritdoc/>
            internal override Api.Enums.V1.TaskReachability ToProto() => Api.Enums.V1.TaskReachability.ClosedWorkflows;
        }
    }
}