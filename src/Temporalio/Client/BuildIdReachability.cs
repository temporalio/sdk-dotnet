using System.Collections.Generic;
using System.Linq;
using Temporalio.Api.Enums.V1;

namespace Temporalio.Client
{
    /// <summary>
    /// Contains information about the reachability of a specific Build ID.
    /// </summary>
    /// <param name="TaskQueueReachability">Maps Task Queue names to the reachability status of the Build ID on that
    /// queue. If the value is an empty list, the Build ID is not reachable on that queue.</param>
    /// <param name="UnretrievedTaskQueues">If any Task Queues could not be retrieved because the server limits the
    /// number that can be queried at once, they will be listed here.</param>
    public record BuildIdReachability(
        IReadOnlyDictionary<string, IReadOnlyCollection<TaskReachability>> TaskQueueReachability,
        IReadOnlyCollection<string> UnretrievedTaskQueues)
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static BuildIdReachability FromProto(Api.TaskQueue.V1.BuildIdReachability proto)
        {
            var unretrieved = new List<string>();
            var tqrDict = proto.TaskQueueReachability
                .SkipWhile(tqr =>
                {
                    if (tqr.Reachability.Count == 1 &&
                        tqr.Reachability[0] == TaskReachability.Unspecified)
                    {
                        unretrieved.Add(tqr.TaskQueue);
                        return true;
                    }

                    return false;
                })
                .ToDictionary(
                    tqr => tqr.TaskQueue,
                    tqr => (IReadOnlyCollection<TaskReachability>)tqr.Reachability);
            return new BuildIdReachability(tqrDict, unretrieved.AsReadOnly());
        }
    }
}