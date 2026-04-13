using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Extensions.ToolRegistry.Testing
{
    /// <summary>
    /// Implements <see cref="IProvider"/> using pre-scripted responses. No LLM API calls are made.
    /// Responses are consumed in order; once exhausted the loop stops cleanly.
    /// </summary>
    /// <remarks>
    /// Use <see cref="WithRegistry(IDispatcher)"/> to inject a <see cref="FakeToolRegistry"/> if you need to
    /// record which tool calls the scripted responses trigger.
    /// <para>
    /// Example:
    /// <code>
    /// var provider = new MockProvider(new[]
    /// {
    ///     MockResponse.ToolCall("flag", new Dictionary&lt;string, object?&gt; { ["desc"] = "broken" }),
    ///     MockResponse.Done("said hello"),
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class MockProvider : IProvider
    {
        private readonly IReadOnlyList<MockResponse> responses;
        private IDispatcher dispatcher;
        private int index;

        /// <summary>
        /// Initializes a new instance of the <see cref="MockProvider"/> class with the given
        /// scripted responses. Uses an empty <see cref="ToolRegistry"/> for dispatch by default.
        /// </summary>
        /// <param name="responses">Scripted responses to return in order.</param>
        public MockProvider(IReadOnlyList<MockResponse> responses)
        {
            this.responses = responses;
            dispatcher = new ToolRegistryDispatcher(new ToolRegistry());
        }

        /// <summary>
        /// Replaces the dispatch registry and returns <c>this</c> for chaining.
        /// </summary>
        /// <param name="registry">Dispatcher to use for tool calls.</param>
        /// <returns>This instance.</returns>
        public MockProvider WithRegistry(IDispatcher registry)
        {
            dispatcher = registry;
            return this;
        }

        /// <summary>
        /// Replaces the dispatch registry with a <see cref="ToolRegistry"/> and returns
        /// <c>this</c> for chaining.
        /// </summary>
        /// <param name="registry">Registry whose handlers will be invoked for tool calls.</param>
        /// <returns>This instance.</returns>
        public MockProvider WithRegistry(ToolRegistry registry)
        {
            dispatcher = new ToolRegistryDispatcher(registry);
            return this;
        }

        /// <inheritdoc/>
        public Task<ProviderTurnResult> RunTurnAsync(
            IList<Dictionary<string, object?>> messages,
            IReadOnlyList<ToolDef> tools,
            CancellationToken cancellationToken = default)
        {
            if (index >= responses.Count)
            {
                return Task.FromResult(new ProviderTurnResult(
                    new List<Dictionary<string, object?>>(), Done: true));
            }

            var resp = responses[index++];
            var newMessages = new List<Dictionary<string, object?>>
            {
                new() { ["role"] = "assistant", ["content"] = new List<object?>(resp.Content) },
            };

            if (!resp.Stop)
            {
                var toolResults = new List<Dictionary<string, object?>>();
                foreach (var block in resp.Content)
                {
                    if (!block.TryGetValue("type", out var typeVal) || typeVal as string != "tool_use")
                    {
                        continue;
                    }
                    var name = (string)block["name"]!;
                    var id = (string)block["id"]!;
                    var inputObj = block["input"];
                    IReadOnlyDictionary<string, object?> input;
                    if (inputObj is IReadOnlyDictionary<string, object?> roDict)
                    {
                        input = roDict;
                    }
                    else if (inputObj is Dictionary<string, object?> dict)
                    {
                        input = dict;
                    }
                    else
                    {
                        input = new Dictionary<string, object?>();
                    }

                    string result;
                    try
                    {
                        result = dispatcher.Dispatch(name, input);
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
                if (toolResults.Count > 0)
                {
                    newMessages.Add(new() { ["role"] = "user", ["content"] = toolResults });
                }
            }

            return Task.FromResult(new ProviderTurnResult(newMessages, Done: resp.Stop));
        }

        /// <summary>
        /// Wraps a <see cref="ToolRegistry"/> to implement <see cref="IDispatcher"/>.
        /// </summary>
        private sealed class ToolRegistryDispatcher : IDispatcher
        {
            private readonly ToolRegistry registry;

            public ToolRegistryDispatcher(ToolRegistry registry) => this.registry = registry;

            public string Dispatch(string name, IReadOnlyDictionary<string, object?> input) =>
                registry.Dispatch(name, input);
        }
    }
}
