#if NETCOREAPP3_0_OR_GREATER
namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.ListWorkflowsAsync" />.
    /// </summary>
    /// <param name="Query">List query.</param>
    /// <param name="Options">Options passed in to list.</param>
    public record ListWorkflowsInput(
        string Query,
        WorkflowListOptions? Options);
}
#endif