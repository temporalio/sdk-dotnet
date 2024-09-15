using System.Collections.Generic;
using Temporalio.Api.Common.V1;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowOutboundInterceptor.ScheduleActivityAsync{TResult}(ScheduleActivityInput)" />.
    /// </summary>
    /// <param name="Activity">Activity type name.</param>
    /// <param name="Args">Activity args.</param>
    /// <param name="Options">Activity options.</param>
    /// <param name="Headers">Headers if any. These will be encoded using the codec before sent
    /// to the server.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record ScheduleActivityInput(
        string Activity,
        IReadOnlyCollection<object?> Args,
        ActivityOptions Options,
        IDictionary<string, Payload>? Headers);
}