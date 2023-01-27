#if NETCOREAPP3_0_OR_GREATER
using System;
using Temporalio.Api.Enums.V1;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="WorkflowHandle.FetchHistoryEventsAsync" />.
    /// </summary>
    public class WorkflowHistoryEventFetchOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets a value indicating whether the iterator should wait on events.
        /// </summary>
        public bool WaitNewEvent { get; set; }

        /// <summary>
        /// Gets or sets which history events to fetch.
        /// </summary>
        public HistoryEventFilterType EventFilterType { get; set; } = HistoryEventFilterType.AllEvent;

        /// <summary>
        /// Gets or sets a value indicating whether the to skip archival.
        /// </summary>
        public bool SkipArchival { get; set; }

        /// <summary>
        /// Gets or sets RPC options for starting the workflow.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (WorkflowHistoryEventFetchOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
#endif