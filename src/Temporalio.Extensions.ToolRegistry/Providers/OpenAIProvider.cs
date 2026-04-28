#if NET8_0_OR_GREATER
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Chat;

namespace Temporalio.Extensions.ToolRegistry.Providers
{
    /// <summary>
    /// <see cref="IProvider"/> implementation for the OpenAI Chat Completions API.
    /// </summary>
    /// <remarks>
    /// Messages are stored as <c>List&lt;Dictionary&lt;string, object?&gt;&gt;</c>
    /// (checkpoint-safe) and converted to OpenAI SDK types at call time.
    /// <para>
    /// Example:
    /// <code>
    /// var cfg = new OpenAIConfig { ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") };
    /// IProvider provider = new OpenAIProvider(cfg, registry, "You are a helpful assistant.");
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class OpenAIProvider : IProvider
    {
        private const string DefaultModel = "gpt-4o";

        private readonly ChatClient client;
        private readonly string system;
        private readonly ToolRegistry registry;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAIProvider"/> class.
        /// </summary>
        /// <param name="config">Provider configuration.</param>
        /// <param name="registry">Tool registry used to dispatch tool calls.</param>
        /// <param name="system">System prompt.</param>
        public OpenAIProvider(OpenAIConfig config, ToolRegistry registry, string system)
        {
            this.system = system;
            this.registry = registry;
            if (config.Client != null)
            {
                client = config.Client;
            }
            else
            {
                var model = config.Model ?? DefaultModel;
                if (config.BaseUrl != null)
                {
                    var openAIClient = new OpenAIClient(
                        new ApiKeyCredential(config.ApiKey ?? string.Empty),
                        new OpenAIClientOptions { Endpoint = config.BaseUrl });
                    client = openAIClient.GetChatClient(model);
                }
                else
                {
                    client = new ChatClient(model, config.ApiKey);
                }
            }
        }

        /// <inheritdoc/>
        public async Task<ProviderTurnResult> RunTurnAsync(
            IList<Dictionary<string, object?>> messages,
            IReadOnlyList<ToolDef> tools,
            CancellationToken cancellationToken = default)
        {
            var chatMessages = BuildChatMessages(messages);
            var chatTools = BuildChatTools(tools);

            var options = new ChatCompletionOptions();
            foreach (var tool in chatTools)
            {
                options.Tools.Add(tool);
            }

            ChatCompletion completion = await client.CompleteChatAsync(
                chatMessages, options, cancellationToken).ConfigureAwait(false);

            var newMessages = new List<Dictionary<string, object?>>();

            // Extract assistant text content (may be null when only tool calls are present).
            string? assistantContent = null;
            foreach (var part in completion.Content)
            {
                if (part.Kind == ChatMessageContentPartKind.Text)
                {
                    assistantContent = part.Text;
                    break;
                }
            }

            // Collect tool calls as checkpoint-safe maps.
            var toolCallMaps = new List<Dictionary<string, object?>>();
            foreach (var toolCall in completion.ToolCalls)
            {
                toolCallMaps.Add(new()
                {
                    ["id"] = toolCall.Id,
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = toolCall.FunctionName,
                        ["arguments"] = toolCall.FunctionArguments.ToString(),
                    },
                });
            }

            var assistantMsg = new Dictionary<string, object?> { ["role"] = "assistant" };
            if (assistantContent != null)
            {
                assistantMsg["content"] = assistantContent;
            }
            if (toolCallMaps.Count > 0)
            {
                assistantMsg["tool_calls"] = toolCallMaps;
            }
            newMessages.Add(assistantMsg);

            bool done = toolCallMaps.Count == 0
                || completion.FinishReason == ChatFinishReason.Stop
                || completion.FinishReason == ChatFinishReason.Length;
            if (done)
            {
                return new(newMessages, Done: true);
            }

            // Dispatch each tool call and return results as separate tool messages.
            foreach (var toolCall in completion.ToolCalls)
            {
                var name = toolCall.FunctionName;
                var argsJson = toolCall.FunctionArguments.ToString();
                IReadOnlyDictionary<string, object?> input;
                if (!string.IsNullOrEmpty(argsJson))
                {
                    var rawInput = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson)!;
                    input = JsonElementConverter.Materialize(rawInput);
                }
                else
                {
                    input = new Dictionary<string, object?>();
                }

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

                newMessages.Add(new()
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = toolCall.Id,
                    ["content"] = result,
                });
            }

            return new(newMessages, Done: false);
        }

        private static AssistantChatMessage BuildAssistantMessage(Dictionary<string, object?> msg)
        {
            var toolCallsObj = msg.GetValueOrDefault("tool_calls");
            if (toolCallsObj is List<Dictionary<string, object?>> toolCallsList && toolCallsList.Count > 0)
            {
                var toolCalls = new List<ChatToolCall>();
                foreach (var tcMap in toolCallsList)
                {
                    var id = (string)tcMap["id"]!;
                    var fn = (Dictionary<string, object?>)tcMap["function"]!;
                    var fnName = (string)fn["name"]!;
                    var fnArgs = fn.GetValueOrDefault("arguments") as string ?? string.Empty;
                    toolCalls.Add(ChatToolCall.CreateFunctionToolCall(
                        id, fnName, BinaryData.FromString(fnArgs)));
                }
                return new AssistantChatMessage(toolCalls);
            }

            var content = msg.GetValueOrDefault("content") as string ?? string.Empty;
            return new AssistantChatMessage(content);
        }

        private static List<ChatTool> BuildChatTools(IReadOnlyList<ToolDef> tools)
        {
            var result = new List<ChatTool>(tools.Count);
            foreach (var def in tools)
            {
                var schemaJson = JsonSerializer.Serialize(def.InputSchema);
                result.Add(ChatTool.CreateFunctionTool(
                    functionName: def.Name,
                    functionDescription: def.Description,
                    functionParameters: BinaryData.FromString(schemaJson)));
            }
            return result;
        }

        private List<ChatMessage> BuildChatMessages(IList<Dictionary<string, object?>> messages)
        {
            var result = new List<ChatMessage> { new SystemChatMessage(system) };
            foreach (var msg in messages)
            {
                var role = (string)msg["role"]!;
                switch (role)
                {
                    case "user":
                        var userContent = msg.GetValueOrDefault("content") as string
                            ?? string.Empty;
                        result.Add(new UserChatMessage(userContent));
                        break;

                    case "assistant":
                        result.Add(BuildAssistantMessage(msg));
                        break;

                    case "tool":
                        var toolCallId = (string)msg["tool_call_id"]!;
                        var toolContent = msg.GetValueOrDefault("content") as string
                            ?? string.Empty;
                        result.Add(new ToolChatMessage(toolCallId, toolContent));
                        break;
                }
            }
            return result;
        }
    }
}
#endif
