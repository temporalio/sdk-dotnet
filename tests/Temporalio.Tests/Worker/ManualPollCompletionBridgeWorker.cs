using Temporalio.Bridge.Api.ActivityTask;

namespace Temporalio.Tests.Worker;

internal class ManualPollCompletionBridgeWorker : Bridge.Worker
{
    private readonly Bridge.Worker underlying;
    private Task<ActivityTask?>? leftoverPollTask;

    public ManualPollCompletionBridgeWorker(Bridge.Worker underlying)
        : base(underlying)
    {
        this.underlying = underlying;
    }

    public TaskCompletionSource<ActivityTask?> PollActivityCompletion { get; private set; } = new();

    public override async Task<ActivityTask?> PollActivityTaskAsync()
    {
        // Start a poll if one not leftover
        leftoverPollTask ??= base.PollActivityTaskAsync();
        var completedTask = await Task.WhenAny(PollActivityCompletion.Task, leftoverPollTask!);
        // Remove leftover if completed task was leftover one
        if (completedTask == leftoverPollTask)
        {
            leftoverPollTask = null;
        }
        else
        {
            PollActivityCompletion = new();
        }
        return await completedTask;
    }

    protected override bool ReleaseHandle()
    {
        // This is only here to remove IDE complain that underlying is never used. We keep a strong
        // reference to underlying to ensure the handle is not removed before this is collected.
        GC.KeepAlive(underlying);
        return base.ReleaseHandle();
    }
}