namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="ActivityOutboundInterceptor.Heartbeat" />.
    /// </summary>
    /// <param name="Details">Heartbeat details.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record HeartbeatInput(object?[] Details);
}