using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Temporalio.Extensions.ToolRegistry;
using Temporalio.Extensions.ToolRegistry.Testing;
using Xunit;

namespace Temporalio.Extensions.ToolRegistry.Tests
{
    public class TestingTests
    {
        // ── MockProvider ────────────────────────────────────────────────────────

        [Fact]
        public async Task MockProvider_ReturnsResponsesInOrder()
        {
            var provider = new MockProvider(new[]
            {
                MockResponse.Done("first"),
                MockResponse.Done("second"),
            });

            var r1 = await provider.RunTurnAsync(new List<Dictionary<string, object?>>(), Array.Empty<ToolDef>());
            var r2 = await provider.RunTurnAsync(new List<Dictionary<string, object?>>(), Array.Empty<ToolDef>());
            var r3 = await provider.RunTurnAsync(new List<Dictionary<string, object?>>(), Array.Empty<ToolDef>());

            Assert.True(r1.Done);
            Assert.True(r2.Done);
            // Exhausted — returns empty done.
            Assert.True(r3.Done);
            Assert.Empty(r3.NewMessages);
        }

        [Fact]
        public async Task MockProvider_ToolCall_DispatchesToRegistry()
        {
            var registry = new ToolRegistry();
            var received = new List<string>();
            registry.Register(
                new("flag", "Flag an issue", new Dictionary<string, object?>()),
                input =>
                {
                    received.Add((string)input["desc"]!);
                    return "ok";
                });

            var provider = new MockProvider(new[]
            {
                MockResponse.ToolCall("flag", new Dictionary<string, object?> { ["desc"] = "problem" }),
                MockResponse.Done(),
            });
            // Wrap registry in an adapter so MockProvider can dispatch.
            provider.WithRegistry(new RegistryAdapter(registry));

            var turn1 = await provider.RunTurnAsync(new List<Dictionary<string, object?>>(), registry.Definitions());

            Assert.False(turn1.Done);
            Assert.Single(received);
            Assert.Equal("problem", received[0]);
            // newMessages: assistant + tool_result
            Assert.Equal(2, turn1.NewMessages.Count);
        }

        [Fact]
        public async Task MockProvider_WithFakeToolRegistry_RecordsCalls()
        {
            var fake = new FakeToolRegistry();
            fake.Register(
                new("noop", "No-op", new Dictionary<string, object?>()),
                _ => "done");

            var provider = new MockProvider(new[]
            {
                MockResponse.ToolCall("noop", new Dictionary<string, object?> { ["x"] = "1" }, "call-1"),
                MockResponse.Done(),
            }).WithRegistry(fake);

            await provider.RunTurnAsync(new List<Dictionary<string, object?>>(), Array.Empty<ToolDef>());

            Assert.Single(fake.Calls);
            Assert.Equal("noop", fake.Calls[0].Name);
            Assert.Equal("1", fake.Calls[0].Input["x"]);
            Assert.Equal("done", fake.Calls[0].Result);
        }

        // ── FakeToolRegistry ────────────────────────────────────────────────────

        [Fact]
        public void FakeToolRegistry_RecordsDispatchCalls()
        {
            var fake = new FakeToolRegistry();
            fake.Register(
                new("echo", "Echo", new Dictionary<string, object?>()),
                input => (string)input["v"]!);

            fake.Dispatch("echo", new Dictionary<string, object?> { ["v"] = "hello" });
            fake.Dispatch("echo", new Dictionary<string, object?> { ["v"] = "world" });

            Assert.Equal(2, fake.Calls.Count);
            Assert.Equal("echo", fake.Calls[0].Name);
            Assert.Equal("echo", fake.Calls[1].Name);
            Assert.Equal("hello", fake.Calls[0].Input["v"]);
            Assert.Equal("world", fake.Calls[1].Input["v"]);
            Assert.Equal("hello", fake.Calls[0].Result);
            Assert.Equal("world", fake.Calls[1].Result);
        }

        [Fact]
        public void FakeToolRegistry_Dispatch_DelegatesToInnerRegistry()
        {
            var fake = new FakeToolRegistry();
            fake.Register(
                new("add", "Add two numbers", new Dictionary<string, object?>()),
                input => ((long)input["a"]! + (long)input["b"]!).ToString());

            var result = fake.Dispatch("add", new Dictionary<string, object?> { ["a"] = 3L, ["b"] = 4L });

            Assert.Equal("7", result);
        }

        // ── MockAgenticSession ──────────────────────────────────────────────────

        [Fact]
        public async Task MockAgenticSession_RunToolLoopAsync_IsNoop()
        {
            var session = new MockAgenticSession();
            session.Issues.Add(new() { ["type"] = "bug", ["desc"] = "missing null check" });

            await session.RunToolLoopAsync(null, null, "sys", "find issues");

            Assert.Single(session.Issues);
            Assert.Equal("bug", session.Issues[0]["type"]);
            Assert.Single(session.Messages);
            Assert.Equal("find issues", session.Messages[0]["content"]);
            Assert.Equal("find issues", session.CapturedPrompt);
        }

        // ── CrashAfterTurns ─────────────────────────────────────────────────────

        [Fact]
        public async Task CrashAfterTurns_CompletesNormallyForNTurns()
        {
            var provider = new CrashAfterTurns { N = 2 };

            var r1 = await provider.RunTurnAsync(new List<Dictionary<string, object?>>(), Array.Empty<ToolDef>());
            var r2 = await provider.RunTurnAsync(new List<Dictionary<string, object?>>(), Array.Empty<ToolDef>());

            Assert.False(r1.Done);
            Assert.True(r2.Done);
        }

        [Fact]
        public async Task CrashAfterTurns_ThrowsAfterNTurns()
        {
            var provider = new CrashAfterTurns { N = 1 };

            await provider.RunTurnAsync(new List<Dictionary<string, object?>>(), Array.Empty<ToolDef>());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.RunTurnAsync(new List<Dictionary<string, object?>>(), Array.Empty<ToolDef>()));
        }

        [Fact]
        public async Task CrashAfterTurns_DelegatesFirstNTurns()
        {
            var inner = new MockProvider(new[]
            {
                MockResponse.Done("delegated"),
            });
            var provider = new CrashAfterTurns { N = 1, Delegate = inner };

            var r1 = await provider.RunTurnAsync(new List<Dictionary<string, object?>>(), Array.Empty<ToolDef>());

            // First turn should come from the delegate, not the stub.
            Assert.True(r1.Done);
            Assert.Single(r1.NewMessages);
        }

        // ── MockResponse ────────────────────────────────────────────────────────

        [Fact]
        public void MockResponse_Done_SetsStopTrue()
        {
            var r = MockResponse.Done("finished");
            Assert.True(r.Stop);
            Assert.Single(r.Content);
            Assert.Equal("text", r.Content[0]["type"]);
            Assert.Equal("finished", r.Content[0]["text"]);
        }

        [Fact]
        public void MockResponse_ToolCall_SetsStopFalse()
        {
            var r = MockResponse.ToolCall("my_tool", new Dictionary<string, object?> { ["k"] = "v" }, "id-1");
            Assert.False(r.Stop);
            Assert.Single(r.Content);
            Assert.Equal("tool_use", r.Content[0]["type"]);
            Assert.Equal("my_tool", r.Content[0]["name"]);
            Assert.Equal("id-1", r.Content[0]["id"]);
        }

        // ── JsonElementConverter ────────────────────────────────────────────────

        [Fact]
        public void JsonElementConverter_Materialize_ConvertsNestedObjects()
        {
            // Simulate what System.Text.Json gives us on deserialization.
            var inner = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                @"{""a"": 1, ""b"": ""hello"", ""c"": [1, 2], ""d"": {""nested"": true}}");

            var result = JsonElementConverter.Materialize(inner!);

            Assert.IsType<long>(result["a"]);
            Assert.Equal(1L, result["a"]);
            Assert.IsType<string>(result["b"]);
            Assert.Equal("hello", result["b"]);
            Assert.IsType<List<object?>>(result["c"]);
            Assert.IsType<Dictionary<string, object?>>(result["d"]);
        }

        // Helper adapter: wraps ToolRegistry as IDispatcher.
        private sealed class RegistryAdapter : IDispatcher
        {
            private readonly ToolRegistry registry;
            public RegistryAdapter(ToolRegistry r) => registry = r;
            public string Dispatch(string name, IReadOnlyDictionary<string, object?> input) =>
                registry.Dispatch(name, input);
        }
    }
}
