using System;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Prometheus metric export options. <see cref="BindAddress" /> is required.
    /// </summary>
    public class PrometheusOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusOptions"/> class.
        /// </summary>
        public PrometheusOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusOptions"/> class.
        /// </summary>
        /// <param name="bindAddress"><see cref="BindAddress" />.</param>
        public PrometheusOptions(string bindAddress)
        {
            BindAddress = bindAddress;
        }

        /// <summary>
        /// Gets or sets the address to expose Prometheus metrics on.
        /// </summary>
        public string? BindAddress { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone()
        {
            return MemberwiseClone();
        }
    }
}
