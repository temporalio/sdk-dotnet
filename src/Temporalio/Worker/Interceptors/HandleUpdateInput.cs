using System.Collections.Generic;
using Temporalio.Api.Common.V1;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowInboundInterceptor.ValidateUpdate(HandleUpdateInput)" /> and
    /// <see cref="WorkflowInboundInterceptor.HandleUpdateAsync(HandleUpdateInput)" /> .
    /// </summary>
    /// <param name="Id">Update ID.</param>
    /// <param name="Update">Update name.</param>
    /// <param name="Definition">Update definition.</param>
    /// <param name="Args">Update arguments.</param>
    /// <param name="Headers">Update headers.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record HandleUpdateInput(
        string Id,
        string Update,
        WorkflowUpdateDefinition Definition,
        object?[] Args,
        IReadOnlyDictionary<string, Payload>? Headers);
}