namespace Temporalio.Tests.Nexus;

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Nexus;
using Xunit;

/// <summary>
/// Server-independent unit tests for the generic Temporal Nexus operation handler: cancel routing
/// and validation, plus the start-time guards that fail fast before any server call.
/// </summary>
public class NexusOperationHandlerTests
{
    [Fact]
    public async Task CancelAsync_UpdateToken_DefaultThrowsNotImplemented()
    {
        var token = new NexusWorkflowUpdateHandle("my-ns", "wid", "rid", "uid", 0).ToToken();
        var handler = new TemporalOperationHandler<int, int>(
            (_, _, _) => throw new InvalidOperationException("start not expected"));

        var exc = await RunCancelAsync(handler, token);
        var handlerExc = Assert.IsType<HandlerException>(exc);
        Assert.Equal(HandlerErrorType.NotImplemented, handlerExc.ErrorType);
        Assert.Contains("cannot cancel an UpdateWorkflow operation", handlerExc.Message);
    }

    [Fact]
    public async Task CancelAsync_UpdateToken_CustomOverrideInvoked()
    {
        var token =
            new NexusWorkflowUpdateHandle("my-ns", "the-wid", "the-rid", "the-uid", 0).ToToken();
        var handler = new RecordingHandler();

        var exc = await RunCancelAsync(handler, token);
        Assert.Null(exc);
        Assert.Null(handler.RunInput);
        Assert.NotNull(handler.UpdateInput);
        Assert.Equal("the-wid", handler.UpdateInput!.WorkflowId);
        Assert.Equal("the-rid", handler.UpdateInput.RunId);
        Assert.Equal("the-uid", handler.UpdateInput.UpdateId);
    }

    [Fact]
    public async Task CancelAsync_WorkflowRunToken_RoutesToRunCancel()
    {
        var token = new NexusWorkflowRunHandle("my-ns", "run-wid", 0).ToToken();
        var handler = new RecordingHandler();

        var exc = await RunCancelAsync(handler, token);
        Assert.Null(exc);
        Assert.Null(handler.UpdateInput);
        Assert.NotNull(handler.RunInput);
        Assert.Equal("run-wid", handler.RunInput!.WorkflowId);
    }

    [Fact]
    public async Task CancelAsync_MalformedUpdateToken_ThrowsBadRequest()
    {
        // Update-type token (t=3) missing the update ID (uid): must be rejected as a bad request
        // before reaching the cancel override, rather than passing an empty update ID through.
        var json = """{"t":3,"ns":"my-ns","wid":"w"}""";
        var token = NexusWorkflowRunHandle.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        var handler = new RecordingHandler();

        var exc = await RunCancelAsync(handler, token);
        var handlerExc = Assert.IsType<HandlerException>(exc);
        Assert.Equal(HandlerErrorType.BadRequest, handlerExc.ErrorType);
        Assert.Null(handler.UpdateInput);
    }

    [Fact]
    public async Task StartWorkflowUpdateAsync_WrongWaitStage_FailsOperation()
    {
        // A wait stage other than Accepted surfaces as a failed operation (retryable at the
        // caller's discretion), not a handler bad request.
        var exc = await Assert.ThrowsAsync<OperationException>(() =>
            NexusWorkflowStartHelper.StartWorkflowUpdateAsync<int>(
                new OperationStartContext("svc", "op", default, "req-1"),
                MakeExecContext("my-ns"),
                "wid",
                "upd",
                new object?[] { 5 },
                new WorkflowUpdateStartOptions(WorkflowUpdateStage.Completed)));
        Assert.Equal(OperationState.Failed, exc.State);
    }

    [Fact]
    public async Task StartWorkflowUpdateAsync_MissingCallbackUrl_ThrowsBadRequest()
    {
        // Without a callback URL the async update completion cannot be delivered, so start fails
        // fast as a handler bad request (the OperationStartContext has no callback URL by default).
        var exc = await Assert.ThrowsAsync<HandlerException>(() =>
            NexusWorkflowStartHelper.StartWorkflowUpdateAsync<int>(
                new OperationStartContext("svc", "op", default, "req-1"),
                MakeExecContext("my-ns"),
                "wid",
                "upd",
                new object?[] { 5 },
                new WorkflowUpdateStartOptions(WorkflowUpdateStage.Accepted)));
        Assert.Equal(HandlerErrorType.BadRequest, exc.ErrorType);
    }

    private static NexusOperationExecutionContext MakeExecContext(string ns) =>
        new(
            handlerContext: new OperationStartContext("svc", "op", default, "req"),
            info: new NexusOperationInfo(ns, "my-tq", "my-endpoint"),
            logger: NullLogger.Instance,
            runtimeMetricMeter: new Lazy<MetricMeter>(
                () => throw new InvalidOperationException("metric meter not needed")),
            temporalClient: null);

    private static async Task<Exception?> RunCancelAsync(
        IOperationHandler<int, int> handler, string token)
    {
        var prev = NexusOperationExecutionContext.AsyncLocalCurrent.Value;
        NexusOperationExecutionContext.AsyncLocalCurrent.Value = MakeExecContext("my-ns");
        try
        {
            await handler.CancelAsync(new OperationCancelContext("svc", "op", default, token));
            return null;
        }
#pragma warning disable CA1031 // Tests deliberately capture any exception to assert on it
        catch (Exception e)
#pragma warning restore CA1031
        {
            return e;
        }
        finally
        {
            NexusOperationExecutionContext.AsyncLocalCurrent.Value = prev;
        }
    }

    private sealed class RecordingHandler : TemporalOperationHandler<int, int>
    {
        public RecordingHandler()
            : base((_, _, _) => throw new InvalidOperationException("start not expected"))
        {
        }

        public CancelWorkflowRunInput? RunInput { get; private set; }

        public CancelWorkflowUpdateInput? UpdateInput { get; private set; }

        protected override Task CancelWorkflowRunAsync(
            TemporalOperationCancelContext context, CancelWorkflowRunInput input)
        {
            RunInput = input;
            return Task.CompletedTask;
        }

        protected override Task CancelWorkflowUpdateAsync(
            TemporalOperationCancelContext context, CancelWorkflowUpdateInput input)
        {
            UpdateInput = input;
            return Task.CompletedTask;
        }
    }
}
