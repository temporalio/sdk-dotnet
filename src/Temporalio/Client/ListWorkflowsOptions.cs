using System;

namespace Temporalio.Client
{
    public class ListWorkflowsOptions : ICloneable
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