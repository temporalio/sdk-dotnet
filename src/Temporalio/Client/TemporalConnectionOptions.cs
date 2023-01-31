using System;
using System.Collections.Generic;
using Temporalio.Runtime;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="TemporalConnection.ConnectAsync" />.
    /// <see cref="TargetHost" /> is required.
    /// </summary>
    public class TemporalConnectionOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalConnectionOptions"/> class.
        /// Create default options.
        /// </summary>
        public TemporalConnectionOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalConnectionOptions"/> class.
        /// Create default options with a target host.
        /// </summary>
        /// <param name="targetHost">Target host to connect to.</param>
        /// <seealso cref="TargetHost" />
        public TemporalConnectionOptions(string targetHost)
        {
            TargetHost = targetHost;
        }

        /// <summary>
        /// Gets or sets the Temporal server <c>host:port</c> to connect to.
        /// </summary>
        /// <remarks>
        /// This is required for all connections.
        /// </remarks>
        public string? TargetHost { get; set; }

        /// <summary>
        /// Gets or sets the TLS options for connection.
        /// </summary>
        /// <remarks>
        /// This must be set, even to a default instance, to do any TLS connection.
        /// </remarks>
        public TlsOptions? Tls { get; set; }

        /// <summary>
        /// Gets or sets retry options for this connection.
        /// </summary>
        /// <remarks>
        /// This only applies if the call is being retried, which by default is usually all
        /// high-level client calls but no raw gRPC calls.
        /// </remarks>
        public RpcRetryOptions? RpcRetry { get; set; }

        /// <summary>
        /// Gets or sets the gRPC metadata for all calls (i.e. the headers).
        /// </summary>
        /// <seealso cref="RpcOptions.Metadata" />
        public IEnumerable<KeyValuePair<string, string>>? RpcMetadata { get; set; }

        /// <summary>
        /// Gets or sets the identity for this connection.
        /// </summary>
        /// <remarks>
        /// By default this is <c>pid@hostname</c>. This default may be set when the options object
        /// is first used.
        /// </remarks>
        public string? Identity { get; set; }

        /// <summary>
        /// Gets or sets runtime for this connection.
        /// </summary>
        /// <remarks>
        /// By default this uses <see cref="TemporalRuntime.Default" /> which is lazily created when
        /// first needed.
        /// </remarks>
        public TemporalRuntime? Runtime { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        /// <remarks>Does not create copies of RPC metadata or runtime.</remarks>
        public virtual object Clone()
        {
            var copy = (TemporalConnectionOptions)MemberwiseClone();
            if (Tls != null)
            {
                copy.Tls = (TlsOptions)Tls.Clone();
            }
            if (RpcRetry != null)
            {
                copy.RpcRetry = (RpcRetryOptions)RpcRetry.Clone();
            }
            return copy;
        }

        /// <summary>
        /// Parse the target host as IP and port.
        /// </summary>
        /// <param name="ip">Parsed IP.</param>
        /// <param name="port">Parsed port.</param>
        /// <exception cref="ArgumentException">If the format is invalid.</exception>
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
                    if (portStr != string.Empty && portStr != "0")
                    {
                        int portInt;
                        if (!int.TryParse(portStr, out portInt))
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
