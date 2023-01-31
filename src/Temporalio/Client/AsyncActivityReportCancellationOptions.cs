using System;
using System.Collections.Generic;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="AsyncActivityHandle.ReportCancellationAsync" />.
    /// </summary>
    public class AsyncActivityReportCancellationOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the details for the cancellation.
        /// </summary>
        public IReadOnlyCollection<object?>? Details { get; set; }

        /// <summary>
        /// Gets or sets RPC options.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (AsyncActivityReportCancellationOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}