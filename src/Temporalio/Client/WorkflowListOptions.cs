#if NETCOREAPP3_0_OR_GREATER
using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="ITemporalClient.ListWorkflows" />.
    /// </summary>
    public class WorkflowListOptions : ICloneable
    {
        // TODO(cretz): RPC options etc

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            return MemberwiseClone();
        }
    }
}
#endif