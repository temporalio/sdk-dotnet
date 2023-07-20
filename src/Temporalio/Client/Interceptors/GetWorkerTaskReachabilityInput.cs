using System.Collections.Generic;
using Temporalio.Api.Enums.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.GetWorkerTaskReachabilityAsync" />.
    /// </summary>
    /// <param name="BuildIds">The Build IDs to query the reachability of. At least one must be specified.</param>
    /// <param name="TaskQueues">Task Queues to restrict the query to. If not specified, all Task Queues
    /// will be searched. When requesting a large number of task queues or all task queues
    /// associated with the given Build IDs in a namespace, all Task Queues will be listed
    /// in the response but some of them may not contain reachability information due to a
    /// server enforced limit. When reaching the limit, task queues that reachability
    /// information could not be retrieved for will be marked with a `NotFetched` entry in
    /// <see cref="BuildIdReachability.TaskQueueReachability"/>. The caller may issue another call
    /// to get the reachability for those task queues.</param>
    /// <param name="Reachability">The kind of reachability this request is concerned with.</param>
    /// <param name="RpcOptions">RPC options.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record GetWorkerTaskReachabilityInput(
        IReadOnlyCollection<string> BuildIds,
        IReadOnlyCollection<string> TaskQueues,
        TaskReachability? Reachability,
        RpcOptions? RpcOptions);
}