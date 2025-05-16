using NexusRpc.Handler;

namespace Temporalio.Worker.Interceptors
{
    public record ExecuteNexusOperationStartInput(OperationStartContext Context, object? Input);
}