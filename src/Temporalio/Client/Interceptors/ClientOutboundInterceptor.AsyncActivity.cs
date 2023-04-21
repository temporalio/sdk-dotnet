using System.Threading.Tasks;

namespace Temporalio.Client.Interceptors
{
    public partial class ClientOutboundInterceptor
    {
        /// <summary>
        /// Intercept async activity heartbeat calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task completion.</returns>
        public virtual Task HeartbeatAsyncActivityAsync(HeartbeatAsyncActivityInput input) =>
            Next.HeartbeatAsyncActivityAsync(input);

        /// <summary>
        /// Intercept async activity complete calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task completion.</returns>
        public virtual Task CompleteAsyncActivityAsync(CompleteAsyncActivityInput input) =>
            Next.CompleteAsyncActivityAsync(input);

        /// <summary>
        /// Intercept async activity fail calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task completion.</returns>
        public virtual Task FailAsyncActivityAsync(FailAsyncActivityInput input) =>
            Next.FailAsyncActivityAsync(input);

        /// <summary>
        /// Intercept async activity report cancellation calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task completion.</returns>
        public virtual Task ReportCancellationAsyncActivityAsync(
            ReportCancellationAsyncActivityInput input) =>
            Next.ReportCancellationAsyncActivityAsync(input);
    }
}