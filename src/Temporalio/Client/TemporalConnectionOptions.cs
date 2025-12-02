using System;
using System.Collections.Generic;
using Temporalio.Runtime;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="TemporalConnection.ConnectAsync(TemporalConnectionOptions)" />.
    /// <see cref="TargetHost" /> is required.
    /// </summary>
    public class TemporalConnectionOptions : ICloneable
    {
        private TlsOptions? tls;

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
        public TemporalConnectionOptions(string targetHost) => TargetHost = targetHost;

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
        /// This must be set, even to a default instance, to do any TLS connection. If not set and
        /// <see cref="ApiKey"/> is provided, TLS will be automatically enabled with default options.
        /// </remarks>
        public TlsOptions? Tls
        {
            get => tls;
            set
            {
                tls = value;
                TlsExplicitlySet = true;
            }
        }

        /// <summary>
        /// Gets or sets retry options for this connection.
        /// </summary>
        /// <remarks>
        /// This only applies if the call is being retried, which by default is usually all
        /// high-level client calls but no raw gRPC calls.
        /// </remarks>
        public RpcRetryOptions? RpcRetry { get; set; }

        /// <summary>
        /// Gets or sets keep alive options for this connection.
        /// </summary>
        /// <remarks>
        /// Default enabled, set to null to disable.
        /// </remarks>
        public KeepAliveOptions? KeepAlive { get; set; } = new();

        /// <summary>
        /// Gets or sets HTTP connect proxy options for this connection.
        /// </summary>
        public HttpConnectProxyOptions? HttpConnectProxy { get; set; }

        /// <summary>
        /// Gets or sets the gRPC metadata for all calls (i.e. the headers).
        /// </summary>
        /// <remarks>
        /// Note, this is only the initial value, updates will not be applied. Use
        /// <see cref="ITemporalConnection.RpcMetadata" /> property setter to update.
        /// </remarks>
        /// <seealso cref="RpcOptions.Metadata" />
        public IReadOnlyCollection<KeyValuePair<string, string>>? RpcMetadata { get; set; }

        /// <summary>
        /// Gets or sets the API key for all calls.
        /// </summary>
        /// <remarks>
        /// This is the "Authorization" HTTP header for every call, with "Bearer " prepended. This
        /// is only set if the RPC metadata doesn't already have an "Authorization" key. Note, this
        /// is only the initial value, updates will not be applied. Use
        /// <see cref="ITemporalConnection.ApiKey" /> property setter to update.
        /// </remarks>
        public string? ApiKey { get; set; }

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
        /// Gets a value indicating whether TLS was explicitly set (even to null).
        /// </summary>
        internal bool TlsExplicitlySet { get; private set; }

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
                copy.tls = (TlsOptions)Tls.Clone();
            }
            if (RpcRetry != null)
            {
                copy.RpcRetry = (RpcRetryOptions)RpcRetry.Clone();
            }
            if (KeepAlive != null)
            {
                copy.KeepAlive = (KeepAliveOptions)KeepAlive.Clone();
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
                    if (!string.IsNullOrEmpty(portStr) && portStr != "0")
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
