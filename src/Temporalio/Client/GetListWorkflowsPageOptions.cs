namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="ITemporalClient.GetListWorkflowsPageAsync"/>.
    /// </summary>
    /// <param name="PageSize">Number of results per page. Zero means server default.</param>
    /// <param name="Rpc">RPC options for listing workflows.</param>
    public record GetListWorkflowsPageOptions(int PageSize = 0, RpcOptions? Rpc = null);
}
