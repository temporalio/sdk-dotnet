using System;
using System.Collections.Generic;

namespace Temporalio.Client
{
    public class TemporalConnectionOptions
    {
        public TemporalConnectionOptions() { }

        public TemporalConnectionOptions(string targetHost) : this()
        {
            this.TargetHost = targetHost;
        }

        public TemporalConnectionOptions(TemporalConnectionOptions other)
        {
            TargetHost = other.TargetHost;
            Runtime = other.Runtime;
            Lazy = other.Lazy;
            Metadata = other.Metadata;
        }

        public string? TargetHost { get; set; }

        public TlsOptions? TlsOptions { get; set; }

        public RpcRetryOptions? RpcRetryOptions { get; set; }

        public IEnumerable<KeyValuePair<string, string>>? RpcMetadata { get; set; }

        public string? Identity { get; set; }

        public Runtime? Runtime { get; set; }

        public bool Lazy { get; set; } = false;

        public IEnumerable<KeyValuePair<string, string>>? Metadata { get; set; } = null;

        internal void ParseTargetHost(out string? ip, out int? port)
        {
            ip = null;
            port = null;
            if (TargetHost != null)
            {
                var colonIndex = TargetHost.LastIndexOf(':');
                if (colonIndex == -1)
                {
                    ip = TargetHost;
                }
                else
                {
                    ip = TargetHost.Substring(0, colonIndex);
                    var portStr = TargetHost.Substring(colonIndex + 1);
                    if (portStr != "" && portStr != "0")
                    {
                        int portInt;
                        if (!Int32.TryParse(portStr, out portInt))
                        {
                            throw new ArgumentException("TargetHost does not have valid port");
                        }
                        port = portInt;
                    }
                }
            }
        }
    }
}
