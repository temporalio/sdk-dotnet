using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Payload size-limit options for a connection.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    public class PayloadLimitsOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the warning threshold, in bytes, for the size of an outbound payload-bearing
        /// field. Over-threshold fields are logged but still sent to the server. Defaults to 512KiB;
        /// set to <c>0</c> to disable.
        /// </summary>
        public int PayloadsWarnSize { get; set; } = 512 * 1024;

        /// <summary>
        /// Gets or sets the warning threshold, in bytes, for outbound memo size. Over-threshold
        /// memos are logged but still sent to the server. Defaults to 2KiB; set to <c>0</c> to
        /// disable.
        /// </summary>
        public int MemoWarnSize { get; set; } = 2 * 1024;

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}
