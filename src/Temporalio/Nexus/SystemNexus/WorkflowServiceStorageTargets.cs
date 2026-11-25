#pragma warning disable SA1600 // Internal implementation plumbing.

using Temporalio.Converters;
using Temporalio.Workflows;

namespace Temporalio.Nexus.SystemNexus
{
    internal static class WorkflowServiceStorageTargets
    {
        internal static IStorageDriverTargetInfo SignalWithStartWorkflow(
            SignalWithStartWorkflowRequest request) =>
            new IStorageDriverTargetInfo.Workflow(
                request.Namespace, request.Id, RunId: null, request.Workflow);
    }
}
