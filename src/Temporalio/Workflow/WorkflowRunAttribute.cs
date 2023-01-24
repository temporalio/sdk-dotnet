using System;

namespace Temporalio.Workflow
{
    /// <summary>
    /// Designate a method as the entry point to a workflow.
    /// </summary>
    /// <remarks>
    /// All workflows must have a single method with this attribute. The method must be declared on
    /// the workflow class with this attribute even if it just delegates to a base class. The method
    /// must return a task. It is encouraged to use a single parameter record and a return type
    /// record so that fields can be added to either in a future-proof way.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class WorkflowRunAttribute : Attribute
    {
    }
}
