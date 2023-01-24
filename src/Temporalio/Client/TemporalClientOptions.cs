using System;
using System.Collections.Generic;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for a <see cref="TemporalClient" />.
    /// </summary>
    public class TemporalClientOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the client namespace. Default is "default".
        /// </summary>
        public string Namespace { get; set; } = "default";

        /// <summary>
        /// Gets or sets the data converter.
        /// </summary>
        public Converters.DataConverter DataConverter { get; set; } =
            Converters.DataConverter.Default;

        /// <summary>
        /// Gets or sets the interceptors to intercept client calls.
        /// </summary>
        /// <remarks>
        /// Earlier interceptors in the list wrap later ones. If the interceptor in the list also
        /// implements <see cref="Worker.Interceptors.IWorkerInterceptor" />, it will automatically
        /// be used when the worker is created.
        /// </remarks>
        public IEnumerable<Interceptors.IClientInterceptor>? Interceptors { get; set; }

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
