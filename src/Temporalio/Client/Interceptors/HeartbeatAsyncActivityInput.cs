namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.HeartbeatAsyncActivityAsync" />.
    /// </summary>
    /// <param name="Activity">Activity to heartbeat.</param>
    /// <param name="Options">Options passed in to heartbeat.</param>
    public record HeartbeatAsyncActivityInput(
        AsyncActivityHandle.Reference Activity,
        AsyncActivityHeartbeatOptions? Options);
}