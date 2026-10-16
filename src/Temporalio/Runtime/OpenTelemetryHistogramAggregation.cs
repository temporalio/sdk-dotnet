namespace Temporalio.Runtime
{
    /// <summary>
    /// How OpenTelemetry histogram metrics are aggregated.
    ///
    /// Use <see cref="ExplicitBuckets"/> for fixed bucket boundaries or
    /// <see cref="Base2Exponential"/> for exponential histograms.
    /// </summary>
    /// <seealso cref="OpenTelemetryOptions.HistogramAggregation" />
    public abstract record OpenTelemetryHistogramAggregation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTelemetryHistogramAggregation"/>
        /// class.
        /// </summary>
        internal OpenTelemetryHistogramAggregation()
        {
        }

        /// <summary>
        /// Fixed bucket boundaries, using defaults per metric or
        /// <see cref="OpenTelemetryOptions.HistogramBucketOverrides" /> if set.
        /// </summary>
        public sealed record ExplicitBuckets() : OpenTelemetryHistogramAggregation;

        /// <summary>
        /// OpenTelemetry base-2 exponential histograms, which Prometheus-compatible backends store
        /// as native histograms. Applies to every histogram, including custom ones created through
        /// <see cref="TemporalRuntime.MetricMeter" />. Cannot be combined with
        /// <see cref="OpenTelemetryOptions.HistogramBucketOverrides" />.
        /// </summary>
        public sealed record Base2Exponential() : OpenTelemetryHistogramAggregation
        {
            /// <summary>
            /// Gets the maximum number of buckets in each of the positive and negative ranges.
            /// Must be positive if set. Default is 160.
            /// </summary>
            public int? MaxSize { get; init; }

            /// <summary>
            /// Gets the maximum resolution scale, from -10 to 20. Default is 20. Prometheus
            /// reduces any scale above 8 on ingest.
            /// </summary>
            public int? MaxScale { get; init; }

            /// <summary>
            /// Gets the suffix appended to the name of every histogram, so the exponential series
            /// never shares a name with the fixed-bucket series it replaces. Must not be empty if
            /// set. Default is <c>_native</c>.
            /// </summary>
            public string? NameSuffix { get; init; }
        }
    }
}
