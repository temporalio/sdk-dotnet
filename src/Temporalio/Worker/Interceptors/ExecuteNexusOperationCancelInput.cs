using NexusRpc.Handlers;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for
    /// <see cref="NexusOperationInboundInterceptor.ExecuteNexusOperationCancelAsync"/>.
    /// </summary>
    /// <param name="Context">Nexus context.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public record ExecuteNexusOperationCancelInput(OperationCancelContext Context);
}