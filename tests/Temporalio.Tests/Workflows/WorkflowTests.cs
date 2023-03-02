#pragma warning disable CA1822 // We don't want to force workflow methods to be static

namespace Temporalio.Tests.Workflows;

using Temporalio.Workflows;
using Xunit;

// NOTE: This only unit tests a couple of things with workflows. For the full workflow test suite,
// see Temporalio.Tests.Worker.WorkflowWorkerTests.
public class WorkflowTests
{
    [Fact]
    public void Info_AccessOutsideOfWorkflow_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _ = Workflow.Info);
        Assert.Contains("Not in workflow", ex.Message);
    }
}