using System.Collections.Generic;

namespace Temporalio.Extensions.ToolRegistry
{
    /// <summary>
    /// Defines an LLM tool in Anthropic's tool_use JSON format.
    /// The same definition is used for both Anthropic and OpenAI; <see cref="ToolRegistry"/>
    /// converts the schema for each provider.
    /// </summary>
    /// <param name="Name">Tool name used in function calls.</param>
    /// <param name="Description">Human-readable description of what the tool does.</param>
    /// <param name="InputSchema">JSON schema for the tool's input parameters.</param>
    public sealed record ToolDef(
        string Name,
        string Description,
        IReadOnlyDictionary<string, object?> InputSchema);
}
