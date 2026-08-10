namespace Temporalio.Tests.Worker;

using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Bridge;
using Temporalio.Worker;
using Xunit;

public class TemporalWorkerOptionsTests
{
    [Fact]
    public void ToInteropOptions_ForwardsMaxEagerActivityReservationsPerWorkflowTask()
    {
        var options = new TemporalWorkerOptions("task-queue")
        {
            MaxEagerActivityReservationsPerWorkflowTask = 7,
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(
            scope,
            "namespace",
            NullLoggerFactory.Instance);

        Assert.Equal(
            7U,
            interopOptions.max_eager_activity_reservations_per_workflow_task);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ToInteropOptions_RejectsNonPositiveMaxEagerActivityReservationsPerWorkflowTask(
        int value)
    {
        var options = new TemporalWorkerOptions("task-queue")
        {
            MaxEagerActivityReservationsPerWorkflowTask = value,
        };

        using var scope = new Scope();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            options.ToInteropOptions(scope, "namespace", NullLoggerFactory.Instance));

        Assert.Contains(
            "set DisableEagerActivityExecution to true to disable eager activity execution",
            exception.Message);
    }
}
