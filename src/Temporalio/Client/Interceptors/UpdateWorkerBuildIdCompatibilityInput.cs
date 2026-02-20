using System;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.UpdateWorkerBuildIdCompatibilityAsync" />.
    /// </summary>
    /// <param name="TaskQueue">The Task Queue to target.</param>
    /// <param name="BuildIdOp">The operation to perform.</param>
    /// <param name="RpcOptions">RPC options.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    [Obsolete("Use the Worker Deployment API instead. See https://docs.temporal.io/worker-deployments")]
    public record UpdateWorkerBuildIdCompatibilityInput(
        string TaskQueue,
        BuildIdOp BuildIdOp,
        RpcOptions? RpcOptions);
}
