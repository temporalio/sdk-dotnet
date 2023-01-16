using System;

namespace Temporalio.Converters
{
    /// <summary>
    /// Options for a failure converter.
    /// </summary>
    public class DefaultFailureConverterOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets a value indicating whether message and stack trace to/from encoded
        /// attributes which are subject to codecs (e.g. for encryption).
        /// </summary>
        public bool EncodeCommonAttributes { get; set; }

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
