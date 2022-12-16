using System;
using System.Collections.Generic;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="TemporalConnection.ConnectAsync" />.
    /// <see cref="TemporalConnectionOptions.TargetHost" /> is required.
    /// </summary>
    public class TemporalConnectionOptions : ICloneable
    {
        /// <summary>
        /// Create default options.
        /// </summary>
        public TemporalConnectionOptions() { }

        /// <summary>
        /// Create default options with a target host.
        /// </summary>
        /// <param name="targetHost">Target host to connect to.</param>
        /// <seealso cref="TemporalConnectionOptions.TargetHost" />
        public TemporalConnectionOptions(string targetHost) : this()
        {
            this.TargetHost = targetHost;
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
        public TlsOptions? TlsOptions { get; set; }

        /// <summary>
        /// Gets or sets retry options for this connection.
        /// </summary>
        /// <remarks>
        /// This only applies if the call is being retried, which by default is usually all
        /// high-level client calls but no raw gRPC calls.
        /// </remarks>
        public RpcRetryOptions? RpcRetryOptions { get; set; }

        /// <summary>
        /// Gets or sets the gRPC metadata for all calls (i.e. the headers).
        /// </summary>
        /// <seealso cref="RpcOptions.Metadata" />
        public IEnumerable<KeyValuePair<string, string>>? RpcMetadata { get; set; }

        /// <summary>
        /// Gets or sets the identity for this connection.
        /// </summary>
        /// <remarks>
        /// By default this is <c>pid@hostname</c>.
        /// </remarks>
        public string? Identity { get; set; }

        /// <summary>
        /// Runtime for this connection.
        /// </summary>
        /// <remarks>
        /// By default this uses <see cref="Runtime.Default" /> which is lazily created when first
        /// needed.
        /// </remarks>
        public Runtime? Runtime { get; set; }

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

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        /// <remarks>Does not create copies of RPC metadata or runtime.</remarks>
        public virtual object Clone()
        {
            var copy = (TemporalConnectionOptions)this.MemberwiseClone();
            if (TlsOptions != null)
            {
                copy.TlsOptions = (TlsOptions)TlsOptions.Clone();
            }
            if (RpcRetryOptions != null)
            {
                copy.RpcRetryOptions = (RpcRetryOptions)RpcRetryOptions.Clone();
            }
            return copy;
        }
    }
}
