using System.Collections.Generic;

namespace Temporalio.Testing
{
    // Unstable
    public class TestServerOptions
    {
        public string? ExistingPath { get; set; }

        public string DownloadVersion { get; set; } = "default";

        public IEnumerable<string>? ExtraArgs { get; set; }
    }
}
