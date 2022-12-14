namespace Temporalio.Testing
{
    public class WorkflowEnvironmentStartTimeSkippingOptions : Client.TemporalClientConnectOptions
    {
        public string? DownloadDirectory { get; set; }

        // Unstable
        public TestServerOptions TestServerOptions { get; set; } = new();
    }
}
