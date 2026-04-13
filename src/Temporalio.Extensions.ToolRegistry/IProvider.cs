using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Extensions.ToolRegistry
{
    /// <summary>
    /// LLM provider adapter. Each implementation handles one LLM API's wire format for tool
    /// calling.
    /// </summary>
    public interface IProvider
    {
        /// <summary>
        /// Executes one turn of the conversation.
        /// </summary>
        /// <param name="messages">Full conversation history so far.</param>
        /// <param name="tools">Available tool definitions.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>New messages to append and whether the loop is done.</returns>
        Task<ProviderTurnResult> RunTurnAsync(
            IList<Dictionary<string, object?>> messages,
            IReadOnlyList<ToolDef> tools,
            CancellationToken cancellationToken = default);
    }
}
