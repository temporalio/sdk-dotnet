using System;
using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="ActivityInboundInterceptor.ExecuteActivityAsync" />.
    /// </summary>
    /// <param name="Delegate">Activity delegate.</param>
    /// <param name="Parameters">Activity parameters.</param>
    /// <param name="Headers">Activity headers.</param>
    public record ExecuteActivityInput(
        Delegate Delegate,
        object?[] Parameters,
        IDictionary<string, Payload>? Headers);
}