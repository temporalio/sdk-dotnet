namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.GetWorkerBuildIdCompatibilityAsync" />.
    /// </summary>
    /// <param name="TaskQueue">The Task Queue to target.</param>
    /// <param name="MaxSets">The maximum number of sets to return. If not specified, all sets will be
    /// returned.</param>
    /// <param name="RpcOptions">RPC options.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record GetWorkerBuildIdCompatibilityInput(
        string TaskQueue,
        int MaxSets,
        RpcOptions? RpcOptions);
}