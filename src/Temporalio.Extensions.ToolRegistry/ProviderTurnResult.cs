using System.Collections.Generic;

namespace Temporalio.Extensions.ToolRegistry
{
    /// <summary>
    /// Result of a single LLM provider turn.
    /// </summary>
    /// <param name="NewMessages">New messages to append to the conversation history.</param>
    /// <param name="Done">Whether the conversation loop is complete.</param>
    public sealed record ProviderTurnResult(
        IList<Dictionary<string, object?>> NewMessages,
        bool Done);
}
