namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.HeartbeatAsyncActivityAsync" />.
    /// </summary>
    /// <param name="Activity">Activity to heartbeat.</param>
    /// <param name="Options">Options passed in to heartbeat.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record HeartbeatAsyncActivityInput(
        AsyncActivityHandle.Reference Activity,
        AsyncActivityHeartbeatOptions? Options);
}