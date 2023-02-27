using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for <see cref="ContinueAsNewException" />.
    /// </summary>
    public class ContinueAsNewOptions : ICloneable
    {
        // TODO(cretz): Options

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => (ContinueAsNewOptions)MemberwiseClone();
    }
}