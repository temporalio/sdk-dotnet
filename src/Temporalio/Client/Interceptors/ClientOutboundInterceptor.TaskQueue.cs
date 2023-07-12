using System.Threading.Tasks;

namespace Temporalio.Client.Interceptors
{
    public partial class ClientOutboundInterceptor
    {
        /// <summary>
        /// Intercept update worker build id compatability calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Completion task.</returns>
        public virtual Task UpdateWorkerBuildIdCompatibilityAsync(
            UpdateWorkerBuildIdCompatibilityInput input) =>
            Next.UpdateWorkerBuildIdCompatibilityAsync(input);

        /// <summary>
        /// Intercept get worker build id compatability calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Completion task.</returns>
        public virtual Task<WorkerBuildIdVersionSets> GetWorkerBuildIdCompatibilityAsync(
            GetWorkerBuildIdCompatibilityInput input) =>
            Next.GetWorkerBuildIdCompatibilityAsync(input);

        /// <summary>
        /// Intercept get worker build id compatability calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Completion task.</returns>
        public virtual Task<WorkerTaskReachability> GetWorkerTaskReachabilityAsync(
            GetWorkerTaskReachabilityInput input) =>
            Next.GetWorkerTaskReachabilityAsync(input);
    }
}