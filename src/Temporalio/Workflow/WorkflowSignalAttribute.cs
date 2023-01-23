using System;

namespace Temporalio.Workflow
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class WorkflowSignalAttribute : Attribute
    {
        public string? Name { get; set; }
    }
}
