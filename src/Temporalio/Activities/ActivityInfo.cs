using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Activities
{
    /// <summary>
    /// Information about an activity.
    /// </summary>
    /// <param name="ActivityId">ID for the activity.</param>
    /// <param name="ActivityType">Type name for the activity.</param>
    /// <param name="Attempt">Attempt the activity is on.</param>
    /// <param name="CurrentAttemptScheduledTime">When the current attempt was scheduled.</param>
    /// <param name="DataConverter">Data converter used for heartbeat details.</param>
    /// <param name="HeartbeatDetails">Details from the last heartbeat of the last attempt.</param>
    /// <param name="HeartbeatTimeout">Heartbeat timeout set by the caller.</param>
    /// <param name="IsLocal">Whether the activity is a local activity or not.</param>
    /// <param name="ScheduleToCloseTimeout">Schedule to close timeout set by the caller.</param>
    /// <param name="ScheduledTime">When the activity was scheduled.</param>
    /// <param name="StartToCloseTimeout">Start to close timeout set by the caller.</param>
    /// <param name="StartedTime">When the activity started.</param>
    /// <param name="TaskQueue">Task queue this activity is on.</param>
    /// <param name="TaskToken">Task token uniquely identifying this activity.</param>
    /// <param name="WorkflowId">Workflow ID that started this activity.</param>
    /// <param name="WorkflowNamespace">Namespace this activity is on.</param>
    /// <param name="WorkflowRunId">Workflow run ID that started this activity.</param>
    /// <param name="WorkflowType">Workflow type name that started this activity.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record ActivityInfo(
        string ActivityId,
        string ActivityType,
        int Attempt,
        DateTime CurrentAttemptScheduledTime,
        [property: JsonIgnore] DataConverter DataConverter,
        IReadOnlyCollection<Payload> HeartbeatDetails,
        TimeSpan? HeartbeatTimeout,
        bool IsLocal,
        TimeSpan? ScheduleToCloseTimeout,
        DateTime ScheduledTime,
        TimeSpan? StartToCloseTimeout,
        DateTime StartedTime,
        string TaskQueue,
        byte[] TaskToken,
        string WorkflowId,
        string WorkflowNamespace,
        string WorkflowRunId,
        string WorkflowType)
    {
        /// <summary>
        /// Gets the value that is set on
        /// <see cref="Microsoft.Extensions.Logging.ILogger.BeginScope{TState}" /> before this activity is
        /// started.
        /// </summary>
        internal Dictionary<string, object> LoggerScope { get; } = new()
        {
            ["ActivityId"] = ActivityId,
            ["ActivityType"] = ActivityType,
            ["Attempt"] = Attempt,
            ["WorkflowNamespace"] = WorkflowNamespace,
            ["WorkflowId"] = WorkflowId,
            ["WorkflowRunId"] = WorkflowRunId,
            ["WorkflowType"] = WorkflowType,
        };

        /// <summary>
        /// Convert a heartbeat detail at the given index.
        /// </summary>
        /// <typeparam name="T">Type of the value.</typeparam>
        /// <param name="index">Index of the value.</param>
        /// <returns>Converted value.</returns>
        public Task<T> HeartbeatDetailAtAsync<T>(int index) =>
            DataConverter.ToValueAsync<T>(HeartbeatDetails.ElementAt(index));
    }
}