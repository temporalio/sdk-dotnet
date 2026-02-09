using System;
using System.Threading.Tasks;

namespace Temporalio.Client.Interceptors
{
    public partial class ClientOutboundInterceptor
    {
#pragma warning disable CS0618 // Using obsolete types internally
        /// <summary>
        /// Intercept update worker build id compatability calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Completion task.</returns>
        [Obsolete("Use the Worker Deployment API instead. See https://docs.temporal.io/worker-deployments")]
        public virtual Task UpdateWorkerBuildIdCompatibilityAsync(
            UpdateWorkerBuildIdCompatibilityInput input) =>
            Next.UpdateWorkerBuildIdCompatibilityAsync(input);

        /// <summary>
        /// Intercept get worker build id compatability calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>The sets, if the Task Queue is versioned, otherwise null.</returns>
        [Obsolete("Use the Worker Deployment API instead. See https://docs.temporal.io/worker-deployments")]
        public virtual Task<WorkerBuildIdVersionSets?> GetWorkerBuildIdCompatibilityAsync(
            GetWorkerBuildIdCompatibilityInput input) =>
            Next.GetWorkerBuildIdCompatibilityAsync(input);

        /// <summary>
        /// Intercept get worker build id compatability calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>The reachability information.</returns>
        [Obsolete("Use the Worker Deployment API instead. See https://docs.temporal.io/worker-deployments")]
        public virtual Task<WorkerTaskReachability> GetWorkerTaskReachabilityAsync(
            GetWorkerTaskReachabilityInput input) =>
            Next.GetWorkerTaskReachabilityAsync(input);
#pragma warning restore CS0618
    }
}