#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;

namespace Temporalio.Extensions.ToolRegistry.Providers
{
    /// <summary>
    /// <see cref="IProvider"/> implementation for the Anthropic Messages API.
    /// </summary>
    /// <remarks>
    /// Messages are stored as <c>List&lt;Dictionary&lt;string, object?&gt;&gt;</c>
    /// (checkpoint-safe) and converted to Anthropic SDK types via JSON round-trip before each
    /// API call.
    /// <para>
    /// Example:
    /// <code>
    /// var cfg = new AnthropicConfig { ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") };
    /// IProvider provider = new AnthropicProvider(cfg, registry, "You are a helpful assistant.");
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class AnthropicProvider : IProvider, IDisposable
    {
        private const string DefaultModel = "claude-sonnet-4-6";

        private readonly AnthropicClient client;
        private readonly bool ownsClient;
        private readonly string model;
        private readonly string system;
        private readonly ToolRegistry registry;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnthropicProvider"/> class.
        /// </summary>
        /// <param name="config">Provider configuration.</param>
        /// <param name="registry">Tool registry used to dispatch tool calls.</param>
        /// <param name="system">System prompt.</param>
        public AnthropicProvider(AnthropicConfig config, ToolRegistry registry, string system)
        {
            this.system = system;
            this.registry = registry;
            model = config.Model ?? DefaultModel;
            if (config.Client != null)
            {
                client = config.Client;
                ownsClient = false;
            }
            else
            {
                var opts = new ClientOptions { ApiKey = config.ApiKey };
                if (config.BaseUrl != null)
                {
                    opts.BaseUrl = config.BaseUrl.AbsoluteUri;
                }
                client = new AnthropicClient(opts);
                ownsClient = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (ownsClient)
            {
                client.Dispose();
            }
        }

        /// <inheritdoc/>
        public async Task<ProviderTurnResult> RunTurnAsync(
            IList<Dictionary<string, object?>> messages,
            IReadOnlyList<ToolDef> tools,
            CancellationToken cancellationToken = default)
        {
            var msgParams = BuildMessageParams(messages);
            var toolUnions = BuildToolUnions(tools);

            var response = await client.Messages.Create(
                new MessageCreateParams
                {
                    Model = model,
                    MaxTokens = 4096,
                    System = system,
                    Messages = msgParams,
                    Tools = toolUnions,
                },
                cancellationToken).ConfigureAwait(false);

            // Convert response content blocks to checkpoint-safe maps.
            var contentMaps = new List<Dictionary<string, object?>>();
            var toolCalls = new List<Dictionary<string, object?>>();

            foreach (var block in response.Content)
            {
                if (block.TryPickText(out var textBlock))
                {
                    contentMaps.Add(new()
                    {
                        ["type"] = "text",
                        ["text"] = textBlock.Text,
                    });
                }
                else if (block.TryPickToolUse(out var toolUseBlock))
                {
                    var input = new Dictionary<string, object?>();
                    foreach (var kvp in toolUseBlock.Input)
                    {
                        input[kvp.Key] = JsonElementConverter.ConvertElement(kvp.Value);
                    }
                    var toolMap = new Dictionary<string, object?>
                    {
                        ["type"] = "tool_use",
                        ["id"] = toolUseBlock.ID,
                        ["name"] = toolUseBlock.Name,
                        ["input"] = input,
                    };
                    contentMaps.Add(toolMap);
                    toolCalls.Add(toolMap);
                }
                // Ignore other block types (thinking, server tool use, etc.)
            }

            var newMessages = new List<Dictionary<string, object?>>
            {
                new() { ["role"] = "assistant", ["content"] = contentMaps },
            };

            if (toolCalls.Count == 0)
            {
                return new(newMessages, Done: true);
            }

            // Dispatch each tool call and collect results.
            var toolResults = new List<Dictionary<string, object?>>();
            foreach (var call in toolCalls)
            {
                var name = (string)call["name"]!;
                var id = (string)call["id"]!;
                var input = (Dictionary<string, object?>)call["input"]!;
                string result;
                try
                {
                    result = registry.Dispatch(name, input);
                }
#pragma warning disable CA1031
                catch (Exception e)
                {
                    result = $"error: {e.Message}";
                }
#pragma warning restore CA1031

                toolResults.Add(new()
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = id,
                    ["content"] = result,
                });
            }
            newMessages.Add(new() { ["role"] = "user", ["content"] = toolResults });
            return new(newMessages, Done: false);
        }

        private static MessageParam[] BuildMessageParams(
            IList<Dictionary<string, object?>> messages)
        {
            var result = new MessageParam[messages.Count];
            for (int i = 0; i < messages.Count; i++)
            {
                var json = JsonSerializer.Serialize(messages[i]);
                var rawData = JsonSerializer.Deserialize<IReadOnlyDictionary<string, JsonElement>>(json)!;
                result[i] = MessageParam.FromRawUnchecked(rawData);
            }
            return result;
        }

        private static ToolUnion[] BuildToolUnions(IReadOnlyList<ToolDef> tools)
        {
            var result = new ToolUnion[tools.Count];
            for (int i = 0; i < tools.Count; i++)
            {
                var def = tools[i];
                var toolDict = new Dictionary<string, object?>
                {
                    ["name"] = def.Name,
                    ["description"] = def.Description,
                    ["input_schema"] = def.InputSchema,
                };
                var json = JsonSerializer.Serialize(toolDict);
                var rawData = JsonSerializer.Deserialize<IReadOnlyDictionary<string, JsonElement>>(json)!;
                result[i] = Tool.FromRawUnchecked(rawData);
            }
            return result;
        }
    }
}
#endif
