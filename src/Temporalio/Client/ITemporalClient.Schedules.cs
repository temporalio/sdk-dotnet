using System.Threading.Tasks;
using Temporalio.Client.Schedules;

#if NETCOREAPP3_0_OR_GREATER
using System.Collections.Generic;
#endif

namespace Temporalio.Client
{
    public partial interface ITemporalClient
    {
        /// <summary>
        /// Create a schedule and return its handle.
        /// </summary>
        /// <param name="scheduleId">Unique ID for the schedule.</param>
        /// <param name="schedule">Schedule to create.</param>
        /// <param name="options">Options for creating the schedule.</param>
        /// <returns>Handle to the created schedule.</returns>
        /// <remarks>
        /// This can throw <see cref="Exceptions.ScheduleAlreadyRunningException" /> if the ID
        /// already exists.
        /// </remarks>
        Task<ScheduleHandle> CreateScheduleAsync(
            string scheduleId, Schedule schedule, ScheduleOptions? options = null);

        /// <summary>
        /// Gets the schedule handle for the given ID.
        /// </summary>
        /// <param name="scheduleId">Schedule ID to get the handle for.</param>
        /// <returns>Schedule handle.</returns>
        ScheduleHandle GetScheduleHandle(string scheduleId);

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// List schedules.
        /// </summary>
        /// <param name="options">Options for the list call.</param>
        /// <returns>Async enumerator for the schedules.</returns>
        public IAsyncEnumerable<ScheduleListDescription> ListSchedulesAsync(
            ScheduleListOptions? options = null);
#endif
    }
}