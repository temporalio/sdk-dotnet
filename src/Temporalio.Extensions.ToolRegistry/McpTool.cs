using System.Collections.Generic;

namespace Temporalio.Extensions.ToolRegistry
{
    /// <summary>
    /// MCP-compatible tool descriptor.
    /// </summary>
    /// <param name="Name">Tool name.</param>
    /// <param name="Description">Human-readable description (may be null).</param>
    /// <param name="InputSchema">JSON Schema for the tool's input object (may be null).</param>
    public sealed record McpTool(
        string Name,
        string? Description,
        IReadOnlyDictionary<string, object?>? InputSchema);
}
