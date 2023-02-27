#pragma warning disable CA1724 // We know this clashes with Temporalio.Api.Workflow namespace

using System.Threading.Tasks;
using Temporalio.Worker;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Static class with all calls that can be made from a workflow. Methods on this class cannot
    /// be used outside of a workflow.
    /// </summary>
    public static class Workflow
    {
        private static WorkflowInstance Instance => (WorkflowInstance)TaskScheduler.Current;

        // TODO(cretz): all of the workflow features
    }
}