using Temporalio.Bridge.Api.ActivityTask;
using Temporalio.Bridge.Api.WorkflowActivation;

namespace Temporalio.Tests.Worker;

internal class ManualPollCompletionBridgeWorker : Bridge.Worker
{
    private readonly Bridge.Worker underlying;

    public ManualPollCompletionBridgeWorker(Bridge.Worker underlying)
        : base(underlying)
    {
        this.underlying = underlying;
    }

    public TaskCompletionSource<WorkflowActivation?> PollWorkflowCompletion { get; } = new();

    public TaskCompletionSource<ActivityTask?> PollActivityCompletion { get; } = new();

    public override Task<WorkflowActivation?> PollWorkflowActivationAsync()
    {
        return Task.WhenAny(PollWorkflowCompletion.Task, base.PollWorkflowActivationAsync()).Unwrap();
    }

    public override Task<ActivityTask?> PollActivityTaskAsync()
    {
        return Task.WhenAny(PollActivityCompletion.Task, base.PollActivityTaskAsync()).Unwrap();
    }

    protected override bool ReleaseHandle()
    {
        // This is only here to remove IDE complain that underlying is never used. We keep a strong
        // reference to underlying to ensure the handle is not removed before this is collected.
        GC.KeepAlive(underlying);
        return base.ReleaseHandle();
    }
}