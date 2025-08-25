using NexusRpc.Handlers;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for
    /// <see cref="NexusOperationInboundInterceptor.ExecuteNexusOperationStartAsync"/>.
    /// </summary>
    /// <param name="Context">Nexus context.</param>
    /// <param name="Input">Operation input.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public record ExecuteNexusOperationStartInput(OperationStartContext Context, object? Input);
}