namespace Temporalio.Tests.Worker;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Runtime;
using Temporalio.Testing;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

// Payload/memo size-limit enforcement lives in sdk-core. These tests only assert that the .NET
// plumbing reaches core: an oversized worker completion is failed proactively (with a forwarded
// [TMPRL1103] error log), the DisablePayloadErrorLimit opt-out lets the oversized payload reach
// (and be rejected by) the server, and the connection's PayloadsWarnSize threshold produces a
// forwarded [TMPRL1103] warning.
public class PayloadSizeLimitsTests : TestBase
{
    private const int PayloadErrorLimit = 10 * 1024;

    private static readonly string[] PayloadLimitsExtraArgs =
    {
        "--dynamic-config-value", $"limit.blobSize.error={PayloadErrorLimit}",
        // The server only enforces the error limit for payloads that also exceed the warn limit, so
        // the warn limit must be below the error limit.
        "--dynamic-config-value", "limit.blobSize.warn=2048",
    };

    public PayloadSizeLimitsTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task OversizedPayload_FailsTask_WithErrorLog()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync(
            new() { DevServerOptions = new() { ExtraArgs = PayloadLimitsExtraArgs } });

        using var loggerFactory = new TestUtils.LogCaptureFactory(LoggerFactory);
        var client = await TemporalClient.ConnectAsync(new()
        {
            TargetHost = env.Client.Connection.Options.TargetHost,
            Namespace = env.Client.Options.Namespace,
            Runtime = new TemporalRuntime(new()
            {
                Telemetry = new()
                {
                    Logging = new()
                    {
                        Forwarding = new() { Logger = loggerFactory.CreateLogger("core") },
                    },
                },
            }),
        });

        var workerOpts = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddWorkflow<LargePayloadWorkflow>().
            AddActivity(LargePayloadActivities.NoOp);
        using var worker = new TemporalWorker(client, workerOpts);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (LargePayloadWorkflow wf) => wf.RunAsync(new(0, PayloadErrorLimit + 1024)),
                new($"wf-{Guid.NewGuid()}", workerOpts.TaskQueue!)
                {
                    ExecutionTimeout = TimeSpan.FromSeconds(5),
                });

            await Assert.ThrowsAsync<WorkflowFailedException>(() => handle.GetResultAsync());

            await AssertMore.EventuallyAsync(() =>
            {
                Assert.Contains(loggerFactory.Logs, e =>
                    e.Level == LogLevel.Error &&
                    e.Formatted.Contains(
                        "[TMPRL1103] Attempted to upload payloads with size that exceeded the error limit."));
                return Task.CompletedTask;
            });

            // Failure mechanism: worker reported a WorkflowTaskFailed with PAYLOADS_TOO_LARGE.
            var history = await handle.FetchHistoryAsync();
            Assert.Contains(history.Events, e =>
                e.EventType == Temporalio.Api.Enums.V1.EventType.WorkflowTaskFailed &&
                e.WorkflowTaskFailedEventAttributes.Cause ==
                    Temporalio.Api.Enums.V1.WorkflowTaskFailedCause.PayloadsTooLarge);
        });
    }

    [Fact]
    public async Task DisablePayloadErrorLimit_SendsToServer()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync(
            new() { DevServerOptions = new() { ExtraArgs = PayloadLimitsExtraArgs } });

        var workerOpts = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
        {
            DisablePayloadErrorLimit = true,
        }.
            AddWorkflow<LargePayloadWorkflow>().
            AddActivity(LargePayloadActivities.NoOp);
        using var worker = new TemporalWorker(env.Client, workerOpts);
        await worker.ExecuteAsync(async () =>
        {
            // With the opt-out, core does not pre-fail; the oversized activity input reaches the
            // server.
            var handle = await env.Client.StartWorkflowAsync(
                (LargePayloadWorkflow wf) => wf.RunAsync(new(PayloadErrorLimit + 1024, 0)),
                new($"wf-{Guid.NewGuid()}", workerOpts.TaskQueue!)
                {
                    ExecutionTimeout = TimeSpan.FromSeconds(5),
                });
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() => handle.GetResultAsync());
            var terminated = Assert.IsType<TerminatedFailureException>(exc.InnerException);
            Assert.Equal(
                "BadScheduleActivityAttributes: ScheduleActivityTaskCommandAttributes.Input exceeds size limit.",
                terminated.Message);
        });
    }

    [Fact]
    public async Task PayloadsWarnSize_ProducesWarningLog()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync(
            new() { DevServerOptions = new() { ExtraArgs = PayloadLimitsExtraArgs } });

        using var loggerFactory = new TestUtils.LogCaptureFactory(LoggerFactory);
        // The warn threshold is configured on the (worker's) connection and forwarded to core.
        var client = await TemporalClient.ConnectAsync(new()
        {
            TargetHost = env.Client.Connection.Options.TargetHost,
            Namespace = env.Client.Options.Namespace,
            PayloadLimits = new() { PayloadsWarnSize = 1024 },
            Runtime = new TemporalRuntime(new()
            {
                Telemetry = new()
                {
                    Logging = new()
                    {
                        Forwarding = new() { Logger = loggerFactory.CreateLogger("core") },
                    },
                },
            }),
        });

        var workerOpts = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddWorkflow<LargePayloadWorkflow>().
            AddActivity(LargePayloadActivities.NoOp);
        using var worker = new TemporalWorker(client, workerOpts);
        await worker.ExecuteAsync(async () =>
        {
            // 2KiB result is above the 1KiB warn threshold but below the 10KiB error limit, so the
            // workflow completes and a warning is logged.
            await client.ExecuteWorkflowAsync(
                (LargePayloadWorkflow wf) => wf.RunAsync(new(0, 2 * 1024)),
                new($"wf-{Guid.NewGuid()}", workerOpts.TaskQueue!)
                {
                    ExecutionTimeout = TimeSpan.FromSeconds(10),
                });

            Assert.Contains(loggerFactory.Logs, e =>
                e.Level == LogLevel.Warning &&
                e.Formatted.Contains(
                    "[TMPRL1103] Attempted to upload payloads with size that exceeded the warning limit."));
        });
    }

    [Fact]
    public async Task MemoWarnSize_ProducesWarningLog()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync(
            new() { DevServerOptions = new() { ExtraArgs = PayloadLimitsExtraArgs } });

        using var loggerFactory = new TestUtils.LogCaptureFactory(LoggerFactory);
        // Only the memo threshold is lowered; the payloads threshold stays at its default so the
        // small workflow input/output do not warn.
        var client = await TemporalClient.ConnectAsync(new()
        {
            TargetHost = env.Client.Connection.Options.TargetHost,
            Namespace = env.Client.Options.Namespace,
            PayloadLimits = new() { MemoWarnSize = 1024 },
            Runtime = new TemporalRuntime(new()
            {
                Telemetry = new()
                {
                    Logging = new()
                    {
                        Forwarding = new() { Logger = loggerFactory.CreateLogger("core") },
                    },
                },
            }),
        });

        var workerOpts = new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
            AddWorkflow<LargePayloadWorkflow>().
            AddActivity(LargePayloadActivities.NoOp);
        using var worker = new TemporalWorker(client, workerOpts);
        await worker.ExecuteAsync(async () =>
        {
            // The memo rides on the StartWorkflowExecution request; its 2KiB value is above the 1KiB
            // memo warn threshold, so the workflow starts and a memo warning is logged.
            await client.ExecuteWorkflowAsync(
                (LargePayloadWorkflow wf) => wf.RunAsync(new(0, 0)),
                new($"wf-{Guid.NewGuid()}", workerOpts.TaskQueue!)
                {
                    ExecutionTimeout = TimeSpan.FromSeconds(10),
                    Memo = new Dictionary<string, object> { ["big"] = new string('m', 2 * 1024) },
                });

            Assert.Contains(loggerFactory.Logs, e =>
                e.Level == LogLevel.Warning &&
                e.Formatted.Contains(
                    "[TMPRL1103] Attempted to upload memo with size that exceeded the warning limit."));
        });
    }

    public record LargePayloadInput(int ActivityInputDataSize, int WorkflowOutputDataSize);

    public static class LargePayloadActivities
    {
        [Activity]
        public static void NoOp(string data)
        {
        }
    }

    [Workflow]
    public class LargePayloadWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(LargePayloadInput input)
        {
            if (input.ActivityInputDataSize > 0)
            {
                await Workflow.ExecuteActivityAsync(
                    () => LargePayloadActivities.NoOp(new string('i', input.ActivityInputDataSize)),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromSeconds(5) });
            }
            return new string('o', input.WorkflowOutputDataSize);
        }
    }
}
