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
    public record ExecuteActivityInput(
        ActivityDefinition Activity,
        object?[] Args,
        IDictionary<string, Payload>? Headers);
}