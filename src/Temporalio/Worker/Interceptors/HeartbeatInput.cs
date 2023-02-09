namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="ActivityOutboundInterceptor.Heartbeat" />.
    /// </summary>
    /// <param name="Details">Heartbeat details.</param>
    public record HeartbeatInput(object?[] Details);
}