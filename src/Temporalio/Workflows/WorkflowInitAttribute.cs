using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Designates a constructor as receiving the same arguments as the method with
    /// <see cref="WorkflowRunAttribute" />.
    /// </summary>
    /// <remarks>
    /// This is not inherited, so a constructor wanting workflow arguments must have this attribute
    /// on it regardless of base class. Only declared constructors are applied, not base class
    /// constructors.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
    public sealed class WorkflowInitAttribute : Attribute
    {
    }
}
