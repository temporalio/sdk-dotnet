namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Represents the unique identifier for a hosted Temporal Worker service.
    /// </summary>
    public record TemporalWorkerServiceId(
        string TaskQueue,
        string? Version,
        bool IsBuildId)
    {
    }
}
