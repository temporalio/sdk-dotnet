using System.Collections.Generic;
using NexusRpc;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for directly scheduling a Temporal System Nexus operation.
    /// </summary>
    /// <param name="Service">System Nexus service name.</param>
    /// <param name="Operation">System Nexus operation definition.</param>
    /// <param name="Arg">Generated request argument.</param>
    /// <param name="Headers">Nexus headers, if any.</param>
    /// <remarks>
    /// System Nexus operations do not expose normal Nexus endpoint, scheduling, cancellation, or
    /// transport-header options. Interceptors may modify the generated request argument.
    /// </remarks>
    internal record ScheduleSystemNexusOperationInput(
        string Service,
        OperationDefinition Operation,
        object? Arg,
        IDictionary<string, string>? Headers);
}
