using System.Collections.Generic;
using Temporalio.Api.History.V1;
using Temporalio.Converters;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Page of workflow history events.
    /// </summary>
    /// <param name="Events">Collection of events.</param>
    /// <param name="NextPageToken">Token for getting the next page if any.</param>
    public record WorkflowHistoryEventPage(
        IReadOnlyCollection<HistoryEvent> Events,
        byte[]? NextPageToken)
    {
        /// <summary>
        /// Gets the serialization context of the Nexus operation handler that started the
        /// workflow, if the server returned one.
        /// </summary>
        internal ISerializationContext.Nexus? NexusSerializationContext { get; init; }
    }
}