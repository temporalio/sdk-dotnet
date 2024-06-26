using System.Collections.Generic;
using Temporalio.Common;

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
        /// Gets or sets a value indicating whether the UI will be started with the server.
        /// </summary>
        public bool UI { get; set; }

        /// <summary>
        /// Gets or sets search attributes registered on the dev server on start.
        /// </summary>
        public IReadOnlyCollection<SearchAttributeKey>? SearchAttributes { get; set; }

        /// <summary>
        /// Gets or sets <b>unstable</b> dev server options.
        /// </summary>
        /// <remarks>
        /// <b>WARNING: This API is subject to change/removal</b>
        /// </remarks>
        public DevServerOptions DevServerOptions { get; set; } = new();

        /// <inheritdoc />
        public override object Clone()
        {
            var copy = (WorkflowEnvironmentStartLocalOptions)base.Clone();
            copy.DevServerOptions = (DevServerOptions)DevServerOptions.Clone();
            return copy;
        }
    }
}
