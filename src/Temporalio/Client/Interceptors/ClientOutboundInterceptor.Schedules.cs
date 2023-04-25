using System.Threading.Tasks;
using Temporalio.Client.Schedules;

#if NETCOREAPP3_0_OR_GREATER
using System.Collections.Generic;
#endif

namespace Temporalio.Client.Interceptors
{
    public partial class ClientOutboundInterceptor
    {
        /// <summary>
        /// Intercept create schedule calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Schedule handle.</returns>
        public virtual Task<ScheduleHandle> CreateScheduleAsync(
            CreateScheduleInput input) => Next.CreateScheduleAsync(input);

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Intercept list schedules calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Schedule enumerable.</returns>
        public virtual IAsyncEnumerable<ScheduleListDescription> ListSchedulesAsync(
            ListSchedulesInput input) => Next.ListSchedulesAsync(input);
#endif

        /// <summary>
        /// Intercept backfill schedule calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for completion.</returns>
        public virtual Task BackfillScheduleAsync(
            BackfillScheduleInput input) => Next.BackfillScheduleAsync(input);

        /// <summary>
        /// Intercept delete schedule calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for completion.</returns>
        public virtual Task DeleteScheduleAsync(
            DeleteScheduleInput input) => Next.DeleteScheduleAsync(input);

        /// <summary>
        /// Intercept describe schedule calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Schedule description.</returns>
        public virtual Task<ScheduleDescription> DescribeScheduleAsync(
            DescribeScheduleInput input) => Next.DescribeScheduleAsync(input);

        /// <summary>
        /// Intercept pause schedule calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for completion.</returns>
        public virtual Task PauseScheduleAsync(
            PauseScheduleInput input) => Next.PauseScheduleAsync(input);

        /// <summary>
        /// Intercept trigger schedule calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for completion.</returns>
        public virtual Task TriggerScheduleAsync(
            TriggerScheduleInput input) => Next.TriggerScheduleAsync(input);

        /// <summary>
        /// Intercept unpause schedule calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for completion.</returns>
        public virtual Task UnpauseScheduleAsync(
            UnpauseScheduleInput input) => Next.UnpauseScheduleAsync(input);

        /// <summary>
        /// Intercept update schedule calls.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for completion.</returns>
        public virtual Task UpdateScheduleAsync(
            UpdateScheduleInput input) => Next.UpdateScheduleAsync(input);
    }
}