using System.Collections.Generic;

namespace Temporalio.Testing
{
    // Unstable
    public class TemporaliteOptions
    {
        public string? ExistingPath { get; set; }

        public string? DatabaseFilename { get; set; }

        public string LogFormat { get; set; } = "pretty";

        public string LogLevel { get; set; } = "warn";

        public string DownloadVersion { get; set; } = "default";

        public IEnumerable<string>? ExtraArgs { get; set; }
    }
}
