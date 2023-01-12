﻿using System.Threading.Tasks;

namespace Temporalio.Client
{
    /// <summary>
    /// Client for a Temporal namespace.
    /// </summary>
    public class TemporalClient : ITemporalClient
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalClient"/> class from an existing
        /// connection.
        /// </summary>
        /// <param name="connection">Connection for this client.</param>
        /// <param name="options">Options for this client.</param>
        public TemporalClient(ITemporalConnection connection, TemporalClientOptions options)
        {
            Connection = connection;
            Options = options;
        }

        /// <inheritdoc />
        public string Namespace => Options.Namespace;

        /// <inheritdoc />
        public Converters.DataConverter DataConverter => Options.DataConverter;

        /// <inheritdoc />
        public IBridgeClientProvider BridgeClientProvider => Connection;

        /// <inheritdoc />
        public ITemporalConnection Connection { get; private init; }

        /// <inheritdoc />
        public TemporalClientOptions Options { get; private init; }

        /// <summary>
        /// Connect to a Temporal namespace.
        /// </summary>
        /// <param name="options">Options for connecting.</param>
        /// <returns>The connected client.</returns>
        public static async Task<TemporalClient> ConnectAsync(TemporalClientConnectOptions options)
        {
            return new TemporalClient(
                await TemporalConnection.ConnectAsync(options),
                options.ToClientOptions());
        }

        // TODO(cretz): High-level client methods
    }
}
