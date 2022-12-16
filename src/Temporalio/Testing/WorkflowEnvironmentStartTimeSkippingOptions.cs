namespace Temporalio.Testing
{
    /// <summary>
    /// Options for <see cref="WorkflowEnvironment.StartTimeSkippingAsync" />.
    /// </summary>
    public class WorkflowEnvironmentStartTimeSkippingOptions : Client.TemporalClientConnectOptions
    {
        /// <summary>
        /// Gets or sets the download directory if the server must be downloaded.
        /// </summary>
        /// <remarks>
        /// Default is OS temporary directory.
        /// </remarks>
        public string? DownloadDirectory { get; set; }

        /// <summary>
        /// Gets or sets <b>unstable</b> test server options.
        /// </summary>
        /// <remarks>
        /// <b>WARNING: This API is subject to change/removal</b>
        /// </remarks>
        public TestServerOptions TestServerOptions { get; set; } = new();

        /// <inheritdoc />
        public override object Clone()
        {
            var copy = (WorkflowEnvironmentStartTimeSkippingOptions)base.Clone();
            copy.TestServerOptions = (TestServerOptions)TestServerOptions.Clone();
            return copy;
        }
    }
}
