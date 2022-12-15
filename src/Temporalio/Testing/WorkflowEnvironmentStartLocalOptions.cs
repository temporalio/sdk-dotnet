namespace Temporalio.Testing
{
    /// <summary>
    /// Options for <see cref="WorkflowEnvironment.StartLocalAsync" />.
    /// </summary>
    public class WorkflowEnvironmentStartLocalOptions : Client.TemporalClientConnectOptions
    {
        /// <summary>
        /// Gets or sets the download directory if the server must be downloaded.
        /// </summary>
        /// <remarks>
        /// Default is OS temporary directory.
        /// </remarks>
        public string? DownloadDirectory { get; set; }

        /// <summary>
        /// Gets or sets whether the UI will be started with the server.
        /// </summary>
        public bool UI { get; set; } = false;

        /// <summary>
        /// Gets or sets <b>unstable</b> Temporalite options.
        /// </summary>
        /// <remarks>
        /// <b>WARNING: This API is subject to change/removal</b>
        /// </remarks>
        public TemporaliteOptions TemporaliteOptions { get; set; } = new();
    }
}
