#pragma warning disable SA1402 // Multiple types of the same name are fine in same file

namespace Temporalio.Common
{
    /// <summary>
    /// Base class for all metrics.
    /// </summary>
    public abstract class Metric
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Metric" /> class.
        /// </summary>
        /// <param name="details">Details.</param>
        internal Metric(MetricDetails details) => Details = details;

        /// <summary>
        /// Gets the name for the metric.
        /// </summary>
        public string Name => Details.Name;

        /// <summary>
        /// Gets the unit for the metric if any.
        /// </summary>
        public string? Unit => Details.Unit;

        /// <summary>
        /// Gets the description for the metric if any.
        /// </summary>
        public string? Description => Details.Description;

        /// <summary>
        /// Gets details of the metric.
        /// </summary>
        internal MetricDetails Details { get; private init; }

        /// <summary>
        /// Metric details for the metric.
        /// </summary>
        /// <param name="Name">Name.</param>
        /// <param name="Unit">Unit.</param>
        /// <param name="Description">Description.</param>
        internal record MetricDetails(string Name, string? Unit, string? Description);
    }

    /// <summary>
    /// Base class for all metrics.
    /// </summary>
    /// <typeparam name="T">Type of value for the metric.</typeparam>
    public abstract class Metric<T> : Metric
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Metric{T}" /> class.
        /// </summary>
        /// <param name="details">Details.</param>
        internal Metric(MetricDetails details)
            : base(details)
        {
        }
    }
}