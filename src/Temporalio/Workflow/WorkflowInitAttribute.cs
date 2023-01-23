using System;

namespace Temporalio.Workflow
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class WorkflowInitAttribute : Attribute
    {
        public string? Name { get; set; }
    }
}
