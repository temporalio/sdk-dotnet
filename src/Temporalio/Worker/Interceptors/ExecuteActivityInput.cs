using System.Collections.Generic;
using Temporalio.Activities;
using Temporalio.Api.Common.V1;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="ActivityInboundInterceptor.ExecuteActivityAsync" />.
    /// </summary>
    /// <param name="Activity">Activity definition.</param>
    /// <param name="Args">Activity arguments.</param>
    /// <param name="Headers">Activity headers.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record ExecuteActivityInput(
        ActivityDefinition Activity,
        object?[] Args,
        IReadOnlyDictionary<string, Payload>? Headers);
}