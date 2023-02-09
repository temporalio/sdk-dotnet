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
        /// <param name="workflowID">ID for the activity's workflow.</param>
        /// <param name="runID">Run ID for the activity's workflow.</param>
        /// <param name="activityID">ID for the activity.</param>
        /// <returns>Async activity handle.</returns>
        public AsyncActivityHandle GetAsyncActivityHandle(
            string workflowID, string runID, string activityID);
    }
}