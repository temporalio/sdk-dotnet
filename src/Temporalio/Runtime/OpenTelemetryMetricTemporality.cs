namespace Temporalio.Runtime
{
    /// <summary>
    /// Temporality for OpenTelemetry metrics.
    /// </summary>
    public enum OpenTelemetryMetricTemporality
    {
        /// <summary>
        /// Cumulative temporality.
        /// </summary>
        Cumulative,

        /// <summary>
        /// Delta temporality.
        /// </summary>
        Delta,
    }
}
