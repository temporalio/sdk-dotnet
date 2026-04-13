using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Temporalio.Extensions.ToolRegistry;
using Temporalio.Extensions.ToolRegistry.Providers;
using Temporalio.Extensions.ToolRegistry.Testing;
using Xunit;

namespace Temporalio.Extensions.ToolRegistry.Tests
{
    public class ToolRegistryTests
    {
        [Fact]
        public void Register_AddsDefinition()
        {
            var registry = new ToolRegistry();
            var def = new ToolDef(
                Name: "greet",
                Description: "Say hello",
                InputSchema: new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["name"] = new Dictionary<string, object?> { ["type"] = "string" },
                    },
                    ["required"] = new[] { "name" },
                });

            registry.Register(def, input => $"Hello, {input["name"]}!");

            var defs = registry.Definitions();
            Assert.Single(defs);
            Assert.Equal("greet", defs[0].Name);
        }

        [Fact]
        public void Dispatch_CallsHandler()
        {
            var registry = new ToolRegistry();
            registry.Register(
                new("echo", "Echoes input", new Dictionary<string, object?>()),
                input => (string)input["msg"]!);

            var result = registry.Dispatch("echo", new Dictionary<string, object?> { ["msg"] = "hi" });

            Assert.Equal("hi", result);
        }

        [Fact]
        public void Dispatch_UnknownTool_ThrowsKeyNotFoundException()
        {
            var registry = new ToolRegistry();

            Assert.Throws<KeyNotFoundException>(() =>
                registry.Dispatch("nope", new Dictionary<string, object?>()));
        }

        [Fact]
        public void Definitions_ReturnsSnapshot()
        {
            var registry = new ToolRegistry();
            registry.Register(
                new("a", "A", new Dictionary<string, object?>()),
                _ => "a");
            registry.Register(
                new("b", "B", new Dictionary<string, object?>()),
                _ => "b");

            var defs = registry.Definitions();

            Assert.Equal(2, defs.Count);
            Assert.Equal("a", defs[0].Name);
            Assert.Equal("b", defs[1].Name);
        }

        [Fact]
        public void ToOpenAI_ReturnsCorrectFormat()
        {
            var registry = new ToolRegistry();
            var schema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>(),
            };
            registry.Register(
                new("mytool", "Does something", schema),
                _ => "ok");

            var openAI = registry.ToOpenAI();

            Assert.Single(openAI);
            Assert.Equal("function", openAI[0]["type"]);
            var fn = (IReadOnlyDictionary<string, object?>)openAI[0]["function"]!;
            Assert.Equal("mytool", fn["name"]);
            Assert.Equal("Does something", fn["description"]);
        }

        [Fact]
        public async Task RunToolLoopAsync_SimpleExchange()
        {
            var registry = new ToolRegistry();
            var flagged = new List<string>();
            registry.Register(
                new("flag", "Flag an issue", new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["desc"] = new Dictionary<string, object?> { ["type"] = "string" },
                    },
                }),
                input =>
                {
                    flagged.Add((string)input["desc"]!);
                    return "recorded";
                });

            var provider = new MockProvider(new[]
            {
                MockResponse.ToolCall("flag", new Dictionary<string, object?> { ["desc"] = "broken" }),
                MockResponse.Done("done"),
            }).WithRegistry(registry);

            var messages = await ToolRegistry.RunToolLoopAsync(
                provider, registry, "system", "find issues");

            Assert.Single(flagged);
            Assert.Equal("broken", flagged[0]);
            // messages: user + assistant(tool_use) + user(tool_result) + assistant(done)
            Assert.True(messages.Count >= 4);
        }

        [Fact]
        public async Task RunToolLoopAsync_NoCalls_ReturnsDone()
        {
            var registry = new ToolRegistry();
            var provider = new MockProvider(new[] { MockResponse.Done("no tools needed") });

            var messages = await ToolRegistry.RunToolLoopAsync(
                provider, registry, "system", "hello");

            Assert.True(messages.Count >= 2);
            Assert.Equal("user", (string)messages[0]["role"]!);
        }

        // ── Checkpoint round-trip test (T6) ──────────────────────────────────────

        /// <summary>
        /// Verifies that an assistant message with tool_calls survives a JSON serialize/deserialize
        /// cycle with all field types preserved. This guards against the class of bug where a
        /// List&lt;Dictionary&lt;string,object?&gt;&gt; stored in-memory deserializes back as
        /// List&lt;object?&gt;, breaking pattern-matching in provider rebuild methods.
        /// </summary>
        [Fact]
        public void Checkpoint_RoundTrip_PreservesToolCallMessages()
        {
            var toolCallsInMemory = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["id"] = "call_abc",
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = "my_tool",
                        ["arguments"] = "{\"x\":1}",
                    },
                },
            };
            var assistantMsg = new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["tool_calls"] = toolCallsInMemory,
            };
            var issueInMemory = new Dictionary<string, object?>
            {
                ["type"] = "smell",
                ["file"] = "foo.cs",
            };
            var cp = new SessionCheckpoint
            {
                Messages = new() { assistantMsg },
                Issues = new() { issueInMemory },
            };

            // Simulate Temporal heartbeat round-trip via JSON serialization.
            var json = JsonSerializer.Serialize(cp);
            var restored = JsonSerializer.Deserialize<SessionCheckpoint>(json)!;
            var messages = JsonElementConverter.MaterializeList(restored.Messages!);
            var issues = JsonElementConverter.MaterializeList(restored.Issues!);

            // Verify assistant message role survived.
            Assert.Equal("assistant", (string)messages[0]["role"]!);

            // tool_calls must come back as List<Dictionary<string,object?>> so that
            // BuildAssistantMessage can pattern-match it on retry.
            var toolCallsRestored = messages[0]["tool_calls"];
            var toolCallsList = Assert.IsType<List<Dictionary<string, object?>>>(toolCallsRestored);
            Assert.Single(toolCallsList);
            Assert.Equal("call_abc", (string)toolCallsList[0]["id"]!);
            Assert.Equal("function", (string)toolCallsList[0]["type"]!);

            // Nested function dict must also be preserved.
            var fn = Assert.IsType<Dictionary<string, object?>>(toolCallsList[0]["function"]);
            Assert.Equal("my_tool", (string)fn["name"]!);

            // Issues must survive the round-trip.
            Assert.Single(issues);
            Assert.Equal("smell", (string)issues[0]["type"]!);
            Assert.Equal("foo.cs", (string)issues[0]["file"]!);
        }

        // ── FromMcpTools ─────────────────────────────────────────────────────────

        [Fact]
        public void FromMcpTools_PopulatesRegistry()
        {
            var tools = new[]
            {
                new McpTool(
                    "read_file",
                    "Read a file",
                    new Dictionary<string, object?>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object?>
                        {
                            ["path"] = new Dictionary<string, object?> { ["type"] = "string" },
                        },
                    }),
                new McpTool("list_dir", null, null), // null schema → empty object schema
            };

            var reg = ToolRegistry.FromMcpTools(tools);
            var defs = reg.Definitions();

            Assert.Equal(2, defs.Count);
            Assert.Equal("read_file", defs[0].Name);
            Assert.Equal("Read a file", defs[0].Description);
            Assert.Equal("list_dir", defs[1].Name);
            Assert.Equal("object", defs[1].InputSchema["type"]); // null schema defaulted
            // no-op handler returns empty string
            Assert.Equal(
                string.Empty,
                reg.Dispatch("read_file", new Dictionary<string, object?> { ["path"] = "/etc/hosts" }));
        }

        // ── Integration tests (skipped unless RUN_INTEGRATION_TESTS is set) ────

        [Fact]
        public async Task Integration_Anthropic()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS")))
            {
                return;
            }

            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            Assert.False(string.IsNullOrEmpty(apiKey), "ANTHROPIC_API_KEY required");

            var (registry, collected) = MakeRecordRegistry();
            using var provider = new AnthropicProvider(
                new AnthropicConfig { ApiKey = apiKey },
                registry,
                "You must call record() exactly once with value='hello'.");

            await ToolRegistry.RunToolLoopAsync(
                provider,
                registry,
                string.Empty,
                "Please call the record tool with value='hello'.");

            Assert.Contains("hello", collected);
        }

        [Fact]
        public async Task Integration_OpenAI()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS")))
            {
                return;
            }

            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            Assert.False(string.IsNullOrEmpty(apiKey), "OPENAI_API_KEY required");

            var (registry, collected) = MakeRecordRegistry();
#pragma warning disable CA2000 // OpenAIProvider does not implement IDisposable
            var provider = new OpenAIProvider(
                new OpenAIConfig { ApiKey = apiKey },
                registry,
                "You must call record() exactly once with value='hello'.");
#pragma warning restore CA2000

            await ToolRegistry.RunToolLoopAsync(
                provider,
                registry,
                string.Empty,
                "Please call the record tool with value='hello'.");

            Assert.Contains("hello", collected);
        }

        private static (ToolRegistry Registry, List<string> Collected) MakeRecordRegistry()
        {
            var collected = new List<string>();
            var registry = new ToolRegistry();
            registry.Register(
                new ToolDef(
                    Name: "record",
                    Description: "Record a value",
                    InputSchema: new Dictionary<string, object?>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object?>
                            { ["value"] = new Dictionary<string, object?> { ["type"] = "string" } },
                        ["required"] = new[] { "value" },
                    }),
                inp =>
                {
                    collected.Add((string)inp["value"]!);
                    return "recorded";
                });
            return (registry, collected);
        }
    }
}
