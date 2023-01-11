using System;

namespace Temporalio.Converters
{
    /// <summary>
    /// Options for a failure converter.
    /// </summary>
    public class FailureConverterOptions : ICloneable
    {
        /// <summary>
        /// If true, will move message and stack trace to/from encoded attributes which are subject
        /// to codecs (e.g. for encryption).
        /// </summary>
        public bool EncodeCommonAttributes { get; set; }

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
