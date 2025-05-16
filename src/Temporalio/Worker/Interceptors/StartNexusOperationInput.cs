using System.Collections.Generic;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    public record StartNexusOperationInput(
        string Service,
        NexusClientOptions ClientOptions,
        string OperationName,
        object? Arg,
        NexusOperationOptions Options,
        IDictionary<string, string>? Headers);
}