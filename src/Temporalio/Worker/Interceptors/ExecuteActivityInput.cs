using System;
using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="ActivityInboundInterceptor.ExecuteActivityAsync" />.
    /// </summary>
    /// <param name="Delegate">Activity delegate.</param>
    /// <param name="Args">Activity arguments.</param>
    /// <param name="Headers">Activity headers.</param>
    public record ExecuteActivityInput(
        Delegate Delegate,
        object?[] Args,
        IDictionary<string, Payload>? Headers);
}