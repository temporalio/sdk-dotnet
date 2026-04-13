using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Extensions.ToolRegistry
{
    /// <summary>
    /// Maps tool names to definitions and handlers.
    /// </summary>
    /// <remarks>
    /// Tools are registered in Anthropic's tool_use format. The registry exports them for Anthropic
    /// or OpenAI and dispatches incoming tool calls to the appropriate handler.
    /// <para>
    /// A <see cref="ToolRegistry"/> is not safe for concurrent modification; build it before
    /// passing it to concurrent activities.
    /// </para>
    /// </remarks>
#pragma warning disable CA1724 // Type name matches namespace name by design
    public sealed class ToolRegistry
#pragma warning restore CA1724
    {
        private readonly List<ToolDef> defs = new();
        private readonly Dictionary<string, Func<IReadOnlyDictionary<string, object?>, string>> handlers = new();

        /// <summary>
        /// Runs a complete multi-turn LLM tool-calling loop.
        /// </summary>
        /// <remarks>
        /// This is the primary entry point for simple, non-resumable loops. For crash-safe sessions
        /// with heartbeat checkpointing, use
        /// <see cref="AgenticSession.RunWithSessionAsync(Func{AgenticSession, Task}, CancellationToken)"/>.
        /// </remarks>
        /// <param name="provider">LLM provider adapter.</param>
        /// <param name="registry">Tool registry.</param>
        /// <param name="system">
        /// System prompt. For API symmetry with other Temporal SDKs. The system prompt is
        /// captured by the provider at construction time; this parameter is not forwarded. Pass
        /// the same value you used when constructing the provider, or an empty string.
        /// </param>
        /// <param name="prompt">Initial user prompt.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Full message history on completion.</returns>
        public static async Task<IList<Dictionary<string, object?>>> RunToolLoopAsync(
            IProvider provider,
            ToolRegistry registry,
            string system,
            string prompt,
            CancellationToken cancellationToken = default)
        {
            var messages = new List<Dictionary<string, object?>>
            {
                new() { ["role"] = "user", ["content"] = prompt },
            };

            while (true)
            {
                var result = await provider.RunTurnAsync(
                    messages, registry.Definitions(), cancellationToken).ConfigureAwait(false);
                foreach (var msg in result.NewMessages)
                {
                    messages.Add(msg);
                }
                if (result.Done)
                {
                    return messages;
                }
            }
        }

        /// <summary>
        /// Creates a <see cref="ToolRegistry"/> from a list of MCP tool descriptors.
        /// </summary>
        /// <remarks>
        /// Each tool is registered with a no-op handler (returning an empty string).
        /// Override handlers by calling <see cref="Register"/> with the same name after
        /// construction.
        /// </remarks>
        /// <param name="tools">MCP tool descriptors.</param>
        /// <returns>A new registry populated from the MCP tool list.</returns>
        public static ToolRegistry FromMcpTools(IEnumerable<McpTool> tools)
        {
            IReadOnlyDictionary<string, object?> emptySchema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>(),
            };
            var registry = new ToolRegistry();
            foreach (var tool in tools)
            {
                registry.Register(
                    new ToolDef(
                        Name: tool.Name,
                        Description: tool.Description ?? string.Empty,
                        InputSchema: tool.InputSchema ?? emptySchema),
                    _ => string.Empty);
            }
            return registry;
        }

        /// <summary>
        /// Registers a tool definition and its handler.
        /// </summary>
        /// <param name="def">Tool definition.</param>
        /// <param name="handler">Function called when the LLM invokes the tool.</param>
        public void Register(ToolDef def, Func<IReadOnlyDictionary<string, object?>, string> handler)
        {
            defs.Add(def);
            handlers[def.Name] = handler;
        }

        /// <summary>
        /// Calls the handler registered for <paramref name="name"/> with the given input.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <param name="input">Tool input.</param>
        /// <returns>String result from the handler.</returns>
        /// <exception cref="KeyNotFoundException">
        /// If no handler is registered for <paramref name="name"/>.
        /// </exception>
        public string Dispatch(string name, IReadOnlyDictionary<string, object?> input)
        {
            if (!handlers.TryGetValue(name, out var handler))
            {
                throw new KeyNotFoundException($"Unknown tool: {name}");
            }
            return handler(input);
        }

        /// <summary>
        /// Returns a snapshot of registered tool definitions.
        /// </summary>
        /// <returns>Read-only list of registered tool definitions.</returns>
        public IReadOnlyList<ToolDef> Definitions() => defs.ToArray();

        /// <summary>
        /// Returns tool definitions in OpenAI function-calling format.
        /// </summary>
        /// <returns>Read-only list of tool definitions as OpenAI function objects.</returns>
        public IReadOnlyList<IReadOnlyDictionary<string, object?>> ToOpenAI()
        {
            var result = new IReadOnlyDictionary<string, object?>[defs.Count];
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                result[i] = new Dictionary<string, object?>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = def.Name,
                        ["description"] = def.Description,
                        ["parameters"] = def.InputSchema,
                    },
                };
            }
            return result;
        }
    }
}
