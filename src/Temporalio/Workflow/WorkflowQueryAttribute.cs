using System;

namespace Temporalio.Workflow
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class WorkflowQueryAttribute : Attribute
    {
        public string? Name { get; set; }
    }
}
