using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Temporalio.Client
{
    /// <summary>
    /// Contains information about the reachability of some Build IDs.
    /// </summary>
    /// <param name="BuildIdReachability">Maps Build IDs to information about their reachability.</param>
    public record WorkerTaskReachability(IReadOnlyDictionary<string, BuildIdReachability> BuildIdReachability)
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static WorkerTaskReachability FromProto(Api.WorkflowService.V1.GetWorkerTaskReachabilityResponse proto)
        {
            var idMap = proto.BuildIdReachability.ToDictionary(
                bid => bid.BuildId,
                bid =>
                {
                    var unretrieved = new List<string>();
                    var tqrDict = bid.TaskQueueReachability
                        .SkipWhile(tqr =>
                        {
                            if (tqr.Reachability.Count == 1 &&
                                tqr.Reachability[0] == Api.Enums.V1.TaskReachability.Unspecified)
                            {
                                unretrieved.Add(tqr.TaskQueue);
                                return true;
                            }

                            return false;
                        })
                        .ToDictionary(
                            tqr => tqr.TaskQueue,
                            tqr =>
                                tqr.Reachability.Select(r => TaskReachabilityType.FromProto(r)).ToList().AsReadOnly());
                    return new BuildIdReachability(tqrDict, unretrieved.AsReadOnly());
                });
            return new(idMap);
        }
    }

    /// <summary>
    /// Contains information about the reachability of a specific Build ID.
    /// </summary>
    /// <param name="TaskQueueReachability">Maps Task Queue names to the reachability status of the Build ID on that
    /// queue. If the value is an empty list, the Build ID is not reachable on that queue.</param>
    /// <param name="UnretrievedTaskQueues">If any Task Queues could not be retrieved because the server limits the
    /// number that can be queried at once, they will be listed here.</param>
    public record BuildIdReachability(
        IReadOnlyDictionary<string, ReadOnlyCollection<TaskReachabilityType>> TaskQueueReachability,
        IReadOnlyCollection<string> UnretrievedTaskQueues);

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