using System.Collections.Generic;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    public partial class TemporalClient
    {
        /// <inheritdoc />
        public Task UpdateWorkerBuildIdCompatibilityAsync(
            string taskQueue,
            BuildIdOp buildIdOp,
            RpcOptions? rpcOptions) =>
            OutboundInterceptor.UpdateWorkerBuildIdCompatibilityAsync(new(
                TaskQueue: taskQueue,
                BuildIdOp: buildIdOp,
                RpcOptions: rpcOptions));

        /// <inheritdoc />
        public Task<WorkerBuildIdVersionSets> GetWorkerBuildIdCompatibilityAsync(
            string taskQueue,
            int maxSets,
            RpcOptions? rpcOptions) =>
            OutboundInterceptor.GetWorkerBuildIdCompatibilityAsync(new(
                TaskQueue: taskQueue,
                MaxSets: maxSets,
                RpcOptions: rpcOptions));

        /// <inheritdoc />
        public Task<WorkerTaskReachability> GetWorkerTaskReachabilityAsync(
            IReadOnlyCollection<string> buildIds,
            IReadOnlyCollection<string> taskQueues,
            TaskReachabilityType? reachabilityType,
            RpcOptions? rpcOptions) =>
            OutboundInterceptor.GetWorkerTaskReachabilityAsync(new(
                BuildIds: buildIds,
                TaskQueues: taskQueues,
                ReachabilityType: reachabilityType,
                RpcOptions: rpcOptions));
    }
}