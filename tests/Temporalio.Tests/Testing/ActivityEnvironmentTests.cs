namespace Temporalio.Tests.Testing;

using Temporalio.Activities;
using Temporalio.Testing;
using Xunit;

public class ActivityEnvironmentTests
{
    [Fact]
    public async Task RunAsync_SimpleActivity_Succeeds()
    {
        ActivityInfo? info = null;
        var cancelReason = ActivityCancelReason.None;
        var heartbeats = new List<object?[]>();
        var env = new ActivityEnvironment()
        {
            Info = ActivityEnvironment.DefaultInfo with { ActivityType = "SomeActivity" },
            Heartbeater = heartbeats.Add,
        };
        env.WorkerShutdownTokenSource.Cancel();
        env.Cancel(ActivityCancelReason.Timeout);
        var ret = await env.RunAsync(async () =>
        {
            info = ActivityContext.Current.Info;
            ActivityContext.Current.Heartbeat("foo", "bar");
            await Task.WhenAny(
                Task.Delay(Timeout.Infinite, ActivityContext.Current.WorkerShutdownToken));
            ActivityContext.Current.Heartbeat("baz", "qux");
            cancelReason = ActivityContext.Current.CancelReason;
            await Task.WhenAny(
                Task.Delay(Timeout.Infinite, ActivityContext.Current.CancellationToken));
            return "done!";
        });
        Assert.Equal("done!", ret);
        Assert.Equal("SomeActivity", info!.ActivityType);
        Assert.Equal(ActivityCancelReason.Timeout, cancelReason);
        Assert.Equal(
            new List<object?[]> { new string[] { "foo", "bar" }, new string[] { "baz", "qux" } },
            heartbeats);
    }
}