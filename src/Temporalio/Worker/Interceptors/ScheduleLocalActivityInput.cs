using System.Collections.Generic;
using Temporalio.Api.Common.V1;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowOutboundInterceptor.ScheduleLocalActivityAsync{TResult}" />.
    /// </summary>
    /// <param name="Activity">Activity type name.</param>
    /// <param name="Args">Activity args.</param>
    /// <param name="Options">Activity options.</param>
    /// <param name="Headers">Headers.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record ScheduleLocalActivityInput(
        string Activity,
        IReadOnlyCollection<object?> Args,
        LocalActivityOptions Options,
        IDictionary<string, Payload>? Headers);
}