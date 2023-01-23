using System;

namespace Temporalio.Workflow
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class WorkflowRunAttribute : Attribute {
    }
}
