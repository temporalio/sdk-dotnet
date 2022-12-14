namespace Temporalio.Testing
{
    public class WorkflowEnvironmentStartLocalOptions : Client.TemporalClientConnectOptions
    {
        public string? DownloadDirectory { get; set; }

        public bool UI { get; set; } = false;

        // Unstable
        public TemporaliteOptions TemporaliteOptions { get; set; } = new();
    }
}
