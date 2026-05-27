using System.Collections.Generic;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.StartNexusOperationAsync{TResult}" />.
    /// </summary>
    /// <param name="Service">Nexus service name.</param>
    /// <param name="Endpoint">Endpoint name.</param>
    /// <param name="Operation">Operation name to start.</param>
    /// <param name="Arg">Argument for the operation.</param>
    /// <param name="Options">Options passed in to start.</param>
    /// <param name="Headers">Headers if any. Nexus headers are string key-value pairs, not
    /// Payloads.</param>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record StartNexusOperationInput(
        string Service,
        string Endpoint,
        string Operation,
        object? Arg,
        NexusOperationOptions Options,
        IDictionary<string, string>? Headers);
}
