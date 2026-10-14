#pragma warning disable SA1600 // Internal implementation plumbing.

using Temporalio.Converters;
using Temporalio.Workflows;

namespace Temporalio.Nexus.SystemNexus
{
    internal static class WorkflowServiceSerializationContexts
    {
        internal static ISerializationContext SignalWithStartWorkflow(
            SignalWithStartWorkflowRequest request) =>
            new ISerializationContext.Workflow(request.Namespace, request.Id);
    }
}
