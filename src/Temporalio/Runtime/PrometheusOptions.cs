using System;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Prometheus metric export options. <see cref="PrometheusOptions.BindAddress" /> is required.
    /// </summary>
    public class PrometheusOptions : ICloneable
    {
        /// <summary>
        /// Address to expose Prometheus metrics on.
        /// </summary>
        public string? BindAddress { get; set; }

        /// <summary>
        /// Create unset Prometheus options.
        /// </summary>
        public PrometheusOptions() { }

        /// <summary>
        /// Create PrometheusOptions with the given bind address.
        /// </summary>
        /// <param name="bindAddress"><see cref="PrometheusOptions.BindAddress" /></param>
        public PrometheusOptions(string bindAddress)
        {
            BindAddress = bindAddress;
        }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
