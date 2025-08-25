using System.Collections.Generic;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowOutboundInterceptor.StartNexusOperationAsync"/>.
    /// </summary>
    /// <param name="Service">Service name.</param>
    /// <param name="ClientOptions">Client options.</param>
    /// <param name="OperationName">Operation name.</param>
    /// <param name="Arg">Operation argument.</param>
    /// <param name="Options">Operation options.</param>
    /// <param name="Headers">Headers if any. These will be encoded using the codec before sent
    /// to the server.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public record StartNexusOperationInput(
        string Service,
        NexusClientOptions ClientOptions,
        string OperationName,
        object? Arg,
        NexusOperationOptions Options,
        IDictionary<string, string>? Headers);
}