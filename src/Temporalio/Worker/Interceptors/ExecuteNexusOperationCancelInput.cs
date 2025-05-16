using NexusRpc.Handler;

namespace Temporalio.Worker.Interceptors
{
    public record ExecuteNexusOperationCancelInput(OperationCancelContext Context);
}