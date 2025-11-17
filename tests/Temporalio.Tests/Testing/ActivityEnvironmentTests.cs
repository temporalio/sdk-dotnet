namespace Temporalio.Tests.Testing;

using Temporalio.Activities;
using Temporalio.Testing;
using Xunit;
using Xunit.Abstractions;

public class ActivityEnvironmentTests : TestBase
{
    public ActivityEnvironmentTests(ITestOutputHelper output)
        : base(output)
    {
    }

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
        await env.WorkerShutdownTokenSource.CancelAsync();
        env.Cancel(ActivityCancelReason.Timeout);
        var ret = await env.RunAsync(async () =>
        {
            info = ActivityExecutionContext.Current.Info;
            ActivityExecutionContext.Current.Heartbeat("foo", "bar");
            await Task.WhenAny(
                Task.Delay(Timeout.Infinite, ActivityExecutionContext.Current.WorkerShutdownToken));
            ActivityExecutionContext.Current.Heartbeat("baz", "qux");
            cancelReason = ActivityExecutionContext.Current.CancelReason;
            await Task.WhenAny(
                Task.Delay(Timeout.Infinite, ActivityExecutionContext.Current.CancellationToken));
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