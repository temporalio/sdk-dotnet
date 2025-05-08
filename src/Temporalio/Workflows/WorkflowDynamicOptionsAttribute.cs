using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Designate a method as the dynamic options provider for a workflow.
    /// </summary>
    /// <remarks>
    /// Because dynamic workflows may conceptually represent more than one workflow type, it may be
    /// desirable to have different settings for fields that would normally be defined at workflow
    /// declaration time, but vary based on the workflow type name or other information available in
    /// the workflow's context. The method with this attribute will be called after the workflow's
    /// initialization (if applicable), but before the workflow's execution method.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class WorkflowDynamicOptionsAttribute : Attribute
    {
    }
}
