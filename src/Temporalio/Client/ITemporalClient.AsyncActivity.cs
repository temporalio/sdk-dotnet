namespace Temporalio.Client
{
    public partial interface ITemporalClient
    {
        /// <summary>
        /// Get a handle to complete an activity asynchronously using its task token.
        /// </summary>
        /// <param name="taskToken">Task token for the activity.</param>
        /// <returns>Async activity handle.</returns>
        public AsyncActivityHandle GetAsyncActivityHandle(byte[] taskToken);

        /// <summary>
        /// Get a handle to complete an activity asynchronously using its qualified identifiers.
        /// </summary>
        /// <param name="workflowId">ID for the activity's workflow.</param>
        /// <param name="runId">Run ID for the activity's workflow.</param>
        /// <param name="activityId">ID for the activity.</param>
        /// <returns>Async activity handle.</returns>
        public AsyncActivityHandle GetAsyncActivityHandle(
            string workflowId, string runId, string activityId);
    }
}