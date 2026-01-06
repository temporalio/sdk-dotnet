using Temporalio.Bridge.Api.ActivityTask;

namespace Temporalio.Tests.Worker;

internal class ManualPollCompletionBridgeWorker : Bridge.Worker
{
    private Task<ActivityTask?>? leftoverPollTask;

    public ManualPollCompletionBridgeWorker(Bridge.Worker underlying)
        : base(underlying.Runtime, underlying.Handle)
    {
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
}
