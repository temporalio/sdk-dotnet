using System;
using System.Collections.Generic;
using System.Threading;

namespace Temporalio.Client
{
    /// <summary>
    /// RPC options that can be provided on client calls.
    /// </summary>
    public class RpcOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets whether the call will retry.
        /// </summary>
        /// <remarks>
        /// High-level client calls retry by default, low-level calls do not.
        /// </remarks>
        public bool? Retry { get; set; }

        /// <summary>
        /// Gets or sets the gRPC metadata for the call (i.e. the headers).
        /// </summary>
        /// <remarks>
        /// Newlines are not allowed in keys or values. Keys here will override any connection-level
        /// metadata values for the same keys.
        /// </remarks>
        public IEnumerable<KeyValuePair<string, string>>? Metadata { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the call. Default is no timeout.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets the cancellation token for the call.
        /// </summary>
        public CancellationToken? CancellationToken { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        /// <remarks>Does not create copies of metadata or cancellation token.</remarks>
        public virtual object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
