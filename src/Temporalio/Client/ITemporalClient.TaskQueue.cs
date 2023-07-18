using System.Collections.Generic;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    public partial interface ITemporalClient
    {
        /// <summary>
        /// Use to add new Build IDs or otherwise update the relative compatibility of Build IDs as
        /// defined on a specific task queue for the Worker Versioning feature.
        /// For more on this feature, see https://docs.temporal.io/workers#worker-versioning .
        /// </summary>
        /// <param name="taskQueue">The Task Queue to target.</param>
        /// <param name="buildIdOp">The operation to perform.</param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Completion task.</returns>
        Task UpdateWorkerBuildIdCompatibilityAsync(
            string taskQueue,
            BuildIdOp buildIdOp,
            RpcOptions? rpcOptions = null);

        /// <summary>
        /// Use to retrieve the sets of compatible Build IDs for the targeted Task Queue.
        /// For more on this feature, see https://docs.temporal.io/workers#worker-versioning .
        /// </summary>
        /// <param name="taskQueue">The Task Queue to target.</param>
        /// <param name="maxSets">The maximum number of sets to return. If not specified, all sets will be
        ///     returned.</param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>The sets, if the Task Queue is versioned, otherwise null.</returns>
        Task<WorkerBuildIdVersionSets?> GetWorkerBuildIdCompatibilityAsync(
            string taskQueue,
            int maxSets = 0,
            RpcOptions? rpcOptions = null);

        /// <summary>
        /// Determine if some Build IDs for certain Task Queues could have tasks dispatched to them.
        /// For more on this feature, see https://docs.temporal.io/workers#worker-versioning .
        /// </summary>
        /// <param name="buildIds">The Build IDs to query the reachability of. At least one must be specified.</param>
        /// <param name="taskQueues">Task Queues to restrict the query to. If not specified, all Task Queues
        /// will be searched. When requesting a large number of task queues or all task queues
        /// associated with the given Build IDs in a namespace, all Task Queues will be listed
        /// in the response but some of them may not contain reachability information due to a
        /// server enforced limit. When reaching the limit, task queues that reachability
        /// information could not be retrieved for will be marked with a `NotFetched` entry in
        /// <see cref="BuildIdReachability.TaskQueueReachability"/>. The caller may issue another call
        /// to get the reachability for those task queues.</param>
        /// <param name="reachabilityType">The kind of reachability this request is concerned with.</param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>The reachability information.</returns>
        Task<WorkerTaskReachability> GetWorkerTaskReachabilityAsync(
            IReadOnlyCollection<string> buildIds,
            IReadOnlyCollection<string> taskQueues,
            TaskReachabilityType? reachabilityType = null,
            RpcOptions? rpcOptions = null);
    }
}