using System;
using Temporalio.Api.Enums.V1;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="WorkflowHandle.QueryAsync{TWorkflow, TQueryResult}(System.Linq.Expressions.Expression{Func{TWorkflow, TQueryResult}}, WorkflowQueryOptions?)" />
    /// and other overloads.
    /// </summary>
    public class WorkflowQueryOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the query reject condition. This overrides the client-level setting if set.
        /// </summary>
        public QueryRejectCondition? RejectCondition { get; set; }

        /// <summary>
        /// Gets or sets RPC options for querying the workflow.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (WorkflowQueryOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}