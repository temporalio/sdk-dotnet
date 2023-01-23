using System;

namespace Temporalio.Workflow
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    public class WorkflowAttribute : Attribute
    {
        public string? Name { get; set; }
    }
}
