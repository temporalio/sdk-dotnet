#pragma warning disable SA1600 // Internal implementation plumbing.

using Temporalio.Workflows;

namespace Temporalio.Nexus.SystemNexus
{
    internal static class TemporalWorkflowContext
    {
        internal static string WorkflowNamespace() => Workflow.Info.Namespace;
    }
}
