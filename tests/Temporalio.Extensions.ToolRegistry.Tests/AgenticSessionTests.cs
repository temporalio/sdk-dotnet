using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Temporalio.Activities;
using Temporalio.Converters;
using Temporalio.Extensions.ToolRegistry;
using Temporalio.Extensions.ToolRegistry.Testing;
using Temporalio.Testing;
using Xunit;

namespace Temporalio.Extensions.ToolRegistry.Tests
{
    public class AgenticSessionTests
    {
        [Fact]
        public async Task RunToolLoopAsync_HeartbeatsBeforeEachTurn()
        {
            var registry = new ToolRegistry();
            registry.Register(
                new("noop", "No-op", new Dictionary<string, object?>()),
                _ => "ok");

            var provider = new MockProvider(new[]
            {
                MockResponse.ToolCall("noop", new Dictionary<string, object?>()),
                MockResponse.Done("done"),
            });

            var captured = new List<object?[]>();
            var env = new ActivityEnvironment { Heartbeater = details => captured.Add(details) };

            await env.RunAsync(async () =>
            {
                var session = new AgenticSession();
                await session.RunToolLoopAsync(
                    provider,
                    registry,
                    "sys",
                    "go",
                    ActivityExecutionContext.Current.CancellationToken);
            });

            // One heartbeat before turn 1 (tool_call), one before turn 2 (done) = 2
            Assert.Equal(2, captured.Count);
        }

        [Fact]
        public async Task RunToolLoopAsync_CancellationAfterHeartbeat()
        {
            var registry = new ToolRegistry();
            var provider = new MockProvider(new[] { MockResponse.Done("done") });

            var env = new ActivityEnvironment();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await env.RunAsync(async () =>
                {
                    // Cancel before entering the loop.
                    env.Cancel();
                    var session = new AgenticSession();
                    await session.RunToolLoopAsync(
                        provider,
                        registry,
                        "sys",
                        "go",
                        ActivityExecutionContext.Current.CancellationToken);
                });
            });
        }

        [Fact]
        public async Task Checkpoint_HeartbeatsSessionState()
        {
            object?[]? capturedDetail = null;
            var env = new ActivityEnvironment
            {
                Heartbeater = details => capturedDetail = details,
            };

            await env.RunAsync(async () =>
            {
                var session = new AgenticSession();
                session.Messages.Add(new() { ["role"] = "user", ["content"] = "hello" });
                session.Issues.Add(new() { ["type"] = "bug" });
                session.Checkpoint();
                await Task.CompletedTask;
            });

            Assert.NotNull(capturedDetail);
            Assert.Single(capturedDetail!);
            var cp = capturedDetail![0] as SessionCheckpoint;
            Assert.NotNull(cp);
            Assert.Single(cp!.Messages!);
            Assert.Single(cp.Issues!);
        }

        [Fact]
        public async Task RunWithSessionAsync_FreshStart()
        {
            var registry = new ToolRegistry();
            var provider = new MockProvider(new[] { MockResponse.Done("done") });
            var captured = new List<object?[]>();
            var env = new ActivityEnvironment { Heartbeater = details => captured.Add(details) };

            List<Dictionary<string, object?>>? sessionMessages = null;

            await env.RunAsync(async () =>
            {
                await AgenticSession.RunWithSessionAsync(async session =>
                {
                    await session.RunToolLoopAsync(provider, registry, "sys", "hello");
                    sessionMessages = new(session.Messages);
                });
            });

            Assert.NotNull(sessionMessages);
            Assert.Equal("user", (string)sessionMessages![0]["role"]!);
            Assert.Equal("hello", sessionMessages[0]["content"]);
        }

        [Fact]
        public async Task RunWithSessionAsync_RestoresFromCheckpoint()
        {
            // Serialize a checkpoint and pre-seed the ActivityInfo heartbeat details.
            var checkpointMessages = new List<Dictionary<string, object?>>
            {
                new() { ["role"] = "user", ["content"] = "restored-prompt" },
                new()
                {
                    ["role"] = "assistant",
                    ["content"] = new List<object?>
                    {
                        new Dictionary<string, object?> { ["type"] = "text", ["text"] = "prior response" },
                    },
                },
            };
            var cp = new SessionCheckpoint { Messages = checkpointMessages, Issues = new() };

            // Serialize checkpoint via DataConverter so it can be seeded into heartbeat details.
            var payload = await DataConverter.Default.ToPayloadAsync(cp);

            var seededInfo = ActivityEnvironment.DefaultInfo with
            {
                HeartbeatDetails = new[] { payload },
            };

            List<Dictionary<string, object?>>? restoredMessages = null;
            var env = new ActivityEnvironment
            {
                Info = seededInfo,
                Heartbeater = _ => { },
            };

            await env.RunAsync(async () =>
            {
                await AgenticSession.RunWithSessionAsync(session =>
                {
                    restoredMessages = new(session.Messages);
                    return Task.CompletedTask;
                });
            });

            Assert.NotNull(restoredMessages);
            Assert.Equal(2, restoredMessages!.Count);
            Assert.Equal("restored-prompt", restoredMessages[0]["content"]);
        }

        [Fact]
        public async Task RunWithSessionAsync_HandlesCorruptCheckpoint()
        {
            // If heartbeat details can't be deserialized as SessionCheckpoint, start fresh.
            var badPayload = await DataConverter.Default.ToPayloadAsync("not-a-checkpoint");

            var seededInfo = ActivityEnvironment.DefaultInfo with
            {
                HeartbeatDetails = new[] { badPayload },
            };

            List<Dictionary<string, object?>>? sessionMessages = null;
            var env = new ActivityEnvironment
            {
                Info = seededInfo,
                Heartbeater = _ => { },
            };

            await env.RunAsync(async () =>
            {
                await AgenticSession.RunWithSessionAsync(session =>
                {
                    sessionMessages = new(session.Messages);
                    return Task.CompletedTask;
                });
            });

            // Fresh start — no messages restored.
            Assert.NotNull(sessionMessages);
            Assert.Empty(sessionMessages!);
        }
    }
}
