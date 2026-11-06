using System;
using System.Collections.Generic;
using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;

namespace Temporalio.Converters
{
    /// <summary>
    /// Gathers what external storage moved during one store or retrieve pass into the metrics core
    /// reports on the activation completion. A single builder collects a whole pass, including its
    /// concurrent per-driver operations, so callers share one instance and read it back once at the
    /// end.
    /// </summary>
    internal sealed class StorageOperationMetricsBuilder
    {
        private readonly object gate = new();
        private readonly SortedSet<string> driverNames = new(StringComparer.Ordinal);
        private long payloadCount;
        private long totalSizeBytes;
        private long startTimestamp;
        private long endTimestamp;
        private bool any;

        /// <summary>
        /// Record one driver operation that moved payloads, bounded by timestamps taken around the
        /// driver call. Safe to call from the concurrent operations of a single pass.
        /// </summary>
        /// <param name="driverName">Driver that stored or retrieved the payloads.</param>
        /// <param name="payloadCount">Number of payloads moved.</param>
        /// <param name="totalSizeBytes">Total size of the payloads moved.</param>
        /// <param name="startTimestamp">
        /// <see cref="Stopwatch.GetTimestamp"/> taken before the driver call.
        /// </param>
        /// <param name="endTimestamp">
        /// <see cref="Stopwatch.GetTimestamp"/> taken after the driver call.
        /// </param>
        public void Record(
            string driverName,
            long payloadCount,
            long totalSizeBytes,
            long startTimestamp,
            long endTimestamp)
        {
            lock (gate)
            {
                driverNames.Add(driverName);
                this.payloadCount += payloadCount;
                this.totalSizeBytes += totalSizeBytes;
                // Operations run concurrently, so the pass spans the earliest start to the latest end.
                if (!any || startTimestamp < this.startTimestamp)
                {
                    this.startTimestamp = startTimestamp;
                }
                if (!any || endTimestamp > this.endTimestamp)
                {
                    this.endTimestamp = endTimestamp;
                }
                any = true;
            }
        }

        /// <summary>
        /// Build the completion metrics for this pass, or null when it moved no payloads so the
        /// field is left unset.
        /// </summary>
        /// <returns>The metrics, or null when nothing was stored or retrieved.</returns>
        public Bridge.Api.Common.ExternalStorageMetrics? ToProto()
        {
            lock (gate)
            {
                if (!any)
                {
                    return null;
                }
                var seconds = (endTimestamp - startTimestamp) / (double)Stopwatch.Frequency;
                var metrics = new Bridge.Api.Common.ExternalStorageMetrics
                {
                    PayloadCount = (ulong)payloadCount,
                    TotalSizeBytes = (ulong)totalSizeBytes,
                    TotalDuration = Duration.FromTimeSpan(TimeSpan.FromSeconds(seconds)),
                };
                metrics.DriverNames.Add(driverNames);
                return metrics;
            }
        }
    }
}
