using System.Reflection;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowInboundInterceptor.ExecuteWorkflowAsync" />.
    /// </summary>
    /// <param name="Instance">Workflow instance.</param>
    /// <param name="RunMethod">Method to invoke on the instance.</param>
    /// <param name="Args">Run method arguments.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record ExecuteWorkflowInput(
        object Instance,
        MethodInfo RunMethod,
        object?[] Args);
}