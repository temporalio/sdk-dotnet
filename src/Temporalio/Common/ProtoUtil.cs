using System;
using Google.Protobuf.WellKnownTypes;

namespace Temporalio.Common
{
    /// <summary>
    /// Utilities for working with Protobuf.
    /// </summary>
    internal static class ProtoUtil
    {
        /// <summary>
        /// Converts Proto Duration to TimeSpan if non-zero, or null if duration is zero.
        /// </summary>
        /// <param name="duration">Proto Duration.</param>
        /// <returns>Non-zero TimeSpan or null.</returns>
        public static TimeSpan? ToNonZeroTimeSpan(this Duration duration)
        {
            var timeSpan = duration.ToTimeSpan();
            if (timeSpan == TimeSpan.Zero)
            {
                return null;
            }
            return timeSpan;
        }
    }
}
