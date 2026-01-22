using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;

namespace Temporalio.Tests.Worker;

public class GeneralWorkerTests : WorkflowEnvironmentTestBase
{
    public GeneralWorkerTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task ExecuteAsync_VoidWorkflowFailure_PropagatesException()
    {
        // This test verifies that ExecuteAsync(Func<Task>) properly propagates exceptions
        // from void-returning tasks. This is a regression test for a bug where
        // ContinueWith with OnlyOnRanToCompletion caused exceptions to be swallowed.
        static void Ignored() => throw new NotImplementedException();
        using var worker = new TemporalWorker(
            Env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddActivity(Ignored));

        // Throw an arbitrary exception type to verify it is propagated out of the worker execution.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await worker.ExecuteAsync(async () => throw new InvalidOperationException(null)));
    }
}