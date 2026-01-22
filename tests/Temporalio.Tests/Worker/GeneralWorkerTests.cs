using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

namespace Temporalio.Tests.Worker;

public class GeneralWorkerTests : TestBase
{
    public GeneralWorkerTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Workflow]
    public class VoidWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
        }
    }

    [Fact]
    public async Task ExecuteAsync_VoidWorkflowFailure_PropagatesException()
    {
        // This test verifies that ExecuteAsync(Func<Task>) properly propagates exceptions
        // from void-returning tasks. This is a regression test for a bug where
        // ContinueWith with OnlyOnRanToCompletion caused exceptions to be swallowed.
        await using var env = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<VoidWorkflow>());

        // Throw an arbitrary exception type to verify it is propagated out of the worker execution.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await worker.ExecuteAsync(async () => throw new InvalidOperationException(null)));
    }
}