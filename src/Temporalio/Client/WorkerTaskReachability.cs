using System;
using System.Collections.Generic;
using System.Linq;

namespace Temporalio.Client
{
    /// <summary>
    /// Contains information about the reachability of some Build IDs.
    /// </summary>
    /// <param name="BuildIdReachability">Maps Build IDs to information about their reachability.</param>
    [Obsolete("Use the Worker Deployment API instead. See https://docs.temporal.io/worker-deployments")]
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
                bid => Client.BuildIdReachability.FromProto(bid));
            return new(idMap);
        }
    }
}