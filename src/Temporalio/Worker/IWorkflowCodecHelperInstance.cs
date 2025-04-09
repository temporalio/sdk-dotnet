using Temporalio.Converters;
using Temporalio.Workflows;

namespace Temporalio.Worker
{
    /// <summary>
    /// Implemented by instances that can provide workflow codec helper contextual information.
    /// </summary>
    internal interface IWorkflowCodecHelperInstance
    {
        /// <summary>
        /// Gets the workflow info.
        /// </summary>
        WorkflowInfo Info { get; }

        /// <summary>
        /// Gets the pending activity serialization context for the given sequence.
        /// </summary>
        /// <param name="seq">Sequence.</param>
        /// <returns>Context.</returns>
        ISerializationContext.Activity? GetPendingActivitySerializationContext(uint seq);

        /// <summary>
        /// Gets the pending child serialization context for the given sequence.
        /// </summary>
        /// <param name="seq">Sequence.</param>
        /// <returns>Context.</returns>
        ISerializationContext.Workflow? GetPendingChildSerializationContext(uint seq);

        /// <summary>
        /// Gets the pending external cancel serialization context for the given sequence.
        /// </summary>
        /// <param name="seq">Sequence.</param>
        /// <returns>Context.</returns>
        ISerializationContext.Workflow? GetPendingExternalCancelSerializationContext(uint seq);

        /// <summary>
        /// Gets the pending external signal serialization context for the given sequence.
        /// </summary>
        /// <param name="seq">Sequence.</param>
        /// <returns>Context.</returns>
        ISerializationContext.Workflow? GetPendingExternalSignalSerializationContext(uint seq);
    }
}