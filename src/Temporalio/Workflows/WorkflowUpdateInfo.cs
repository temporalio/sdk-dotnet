namespace Temporalio.Workflows
{
    /// <summary>
    /// Information about the current update. This set via a
    /// <see cref="System.Threading.AsyncLocal{T}" /> and therefore only visible inside the handler
    /// and tasks it creates.
    /// </summary>
    /// <param name="Id">Current update ID.</param>
    /// <param name="Name">Current update name.</param>
    /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
    public record WorkflowUpdateInfo(
        string Id,
        string Name)
    {
    }
}