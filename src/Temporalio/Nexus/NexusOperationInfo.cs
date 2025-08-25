namespace Temporalio.Nexus
{
    /// <summary>
    /// Temporal-specific Nexus operation information.
    /// </summary>
    /// <param name="Namespace">Current namespace.</param>
    /// <param name="TaskQueue">Current task queue.</param>
    /// <remarks>WARNING: Nexus support is experimental.</remarks>
    public record NexusOperationInfo(
        string Namespace,
        string TaskQueue);
}