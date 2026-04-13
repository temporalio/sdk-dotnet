# Temporalio.Extensions.ToolRegistry

LLM tool-calling primitives for Temporal activities — define tools once, use with
Anthropic or OpenAI.

## Before you start

A Temporal Activity is a function that Temporal monitors and retries automatically on failure. Temporal streams progress between retries via heartbeats — that's the mechanism `RunWithSessionAsync` uses to resume a crashed LLM conversation mid-turn.

`ToolRegistry.RunToolLoopAsync` works standalone in any function — no Temporal server needed. Add `AgenticSession` only when you need crash-safe resume inside a Temporal activity.

`AgenticSession` requires a running Temporal worker — it reads and writes heartbeat state from the active activity context. Use `ToolRegistry.RunToolLoopAsync` standalone for scripts, one-off jobs, or any code that runs outside a Temporal worker.

New to Temporal? → https://docs.temporal.io/develop

## Install

```bash
dotnet add package Temporalio.Extensions.ToolRegistry
# Add only the LLM SDK(s) you use:
dotnet add package Anthropic.SDK      # Anthropic
dotnet add package OpenAI             # OpenAI
```

## Quickstart

Tool definitions use [JSON Schema](https://json-schema.org/understanding-json-schema/) for `InputSchema`. The quickstart uses a single string field; for richer schemas refer to the JSON Schema docs.

```csharp
using Temporalio.Extensions.ToolRegistry;
using Temporalio.Extensions.ToolRegistry.Providers;

[Activity]  // Remove for standalone use — no worker needed
public async Task<List<string>> AnalyzeAsync(string prompt)
{
    var issues = new List<string>();
    var registry = new ToolRegistry();

    registry.Register(
        new ToolDefinition(
            Name: "flag_issue",
            Description: "Flag a problem found in the analysis",
            InputSchema: new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["description"] = new Dictionary<string, object> { ["type"] = "string" },
                },
                ["required"] = new[] { "description" },
            }),
        inp =>
        {
            issues.Add((string)inp["description"]);
            return Task.FromResult("recorded"); // this string is sent back to the LLM as the tool result
        });

    var provider = new AnthropicProvider(
        new AnthropicConfig { ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") },
        registry,
        "You are a code reviewer. Call flag_issue for each problem you find.");

    await ToolRegistry.RunToolLoopAsync(provider, registry, "", prompt);
    return issues;
}
```

### Selecting a model

The default model is `"claude-sonnet-4-6"` (Anthropic) or `"gpt-4o"` (OpenAI). Override with the `Model` property:

```csharp
var cfg = new AnthropicConfig
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
    Model = "claude-3-5-sonnet-20241022",
};
```

Model IDs are defined by the provider — see Anthropic or OpenAI docs for current names.

### OpenAI

```csharp
var cfg = new OpenAIConfig { ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") };
var provider = new OpenAIProvider(cfg, registry, "your system prompt");
await ToolRegistry.RunToolLoopAsync(provider, registry, "", prompt);
```

## Crash-safe agentic sessions

For multi-turn LLM conversations that must survive activity retries, use
`AgenticSession.RunWithSessionAsync`. It saves conversation history via
`ActivityExecutionContext.Heartbeat` on every turn and restores it on retry.

```csharp
[Activity]  // Remove for standalone use — no worker needed
public async Task<List<object>> LongAnalysisAsync(string prompt)
{
    var issues = new List<object>();

    await AgenticSession.RunWithSessionAsync(async session =>
    {
        var registry = new ToolRegistry();
        registry.Register(
            new ToolDefinition("flag", "...", new Dictionary<string, object> { ["type"] = "object" }),
            inp =>
            {
                session.Issues.Add(inp);
                return Task.FromResult("ok"); // this string is sent back to the LLM as the tool result
            });

        var provider = new AnthropicProvider(
            new AnthropicConfig { ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") },
            registry, "your system prompt");

        await session.RunToolLoopAsync(provider, registry, "your system prompt", prompt);
        issues.AddRange(session.Issues.Cast<object>()); // capture after loop completes
    });

    return issues;
}
```

## Testing without an API key

```csharp
using Temporalio.Extensions.ToolRegistry.Testing;

[Fact]
public async Task TestAnalyze()
{
    var registry = new ToolRegistry();
    registry.Register(
        new ToolDefinition("flag", "d", new Dictionary<string, object> { ["type"] = "object" }),
        _ => Task.FromResult("ok"));

    var provider = new MockProvider(
        MockResponse.ToolCall("flag", new Dictionary<string, object> { ["description"] = "stale API" }),
        MockResponse.Done("analysis complete")
    ).WithRegistry(registry);

    var msgs = await ToolRegistry.RunToolLoopAsync(provider, registry, "sys", "analyze");
    Assert.True(msgs.Count > 2);
}
```

## Integration testing with real providers

To run the integration tests against live Anthropic and OpenAI APIs:

```bash
RUN_INTEGRATION_TESTS=1 \
  ANTHROPIC_API_KEY=sk-ant-... \
  OPENAI_API_KEY=sk-proj-... \
  dotnet test --filter "Integration"
```

Tests skip automatically when `RUN_INTEGRATION_TESTS` is unset. Real API calls
incur billing — expect a few cents per full test run.

## Storing application results

`session.Issues` accumulates application-level
results during the tool loop. Elements are serialized to JSON inside each heartbeat
checkpoint — they must be plain maps/dicts with JSON-serializable values. A non-serializable
value raises a non-retryable `ApplicationError` at heartbeat time rather than silently
losing data on the next retry.

### Storing typed results

Convert your domain type to a plain dict at the tool-call site and back after the session:

```csharp
record Issue(string Type, string File);

// Inside tool handler:
session.Issues.Add(new() { ["type"] = "smell", ["file"] = "Foo.cs" });

// After session (using System.Text.Json):
var issues = session.Issues
    .Select(d => JsonSerializer.Deserialize<Issue>(JsonSerializer.Serialize(d))!)
    .ToList();
```

## Per-turn LLM timeout

Individual LLM calls inside the tool loop are unbounded by default. A hung HTTP
connection holds the activity open until Temporal's `ScheduleToCloseTimeout`
fires — potentially many minutes. Set a per-turn timeout on the provider client:

```csharp
var cfg = new AnthropicConfig
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
    Timeout = TimeSpan.FromSeconds(30),
};
var provider = new AnthropicProvider(cfg, registry, "your system prompt");
// provider now enforces 30s per turn
```

Recommended timeouts:

| Model type | Recommended |
|---|---|
| Standard (Claude 3.x, GPT-4o) | 30 s |
| Reasoning (o1, o3, extended thinking) | 300 s |

## MCP integration

`ToolRegistry.FromMcpTools` converts a sequence of `McpTool` records into a populated
registry. Handlers default to no-ops that return an empty string; override them with
`Register` after construction.

```csharp
// mcpTools is IEnumerable<McpTool> — populate from your MCP client.
var registry = ToolRegistry.FromMcpTools(mcpTools);

// Override specific handlers before running the loop.
registry.Register(
    new ToolDef("read_file", "Read a file", schema),
    inp => ReadFile((string)inp["path"]!));
```

`McpTool` mirrors the MCP protocol's `Tool` object: `Name`, `Description`, and
`InputSchema` (an `IReadOnlyDictionary<string, object?>` containing a JSON Schema
object). A `null` `InputSchema` is treated as an empty object schema.
