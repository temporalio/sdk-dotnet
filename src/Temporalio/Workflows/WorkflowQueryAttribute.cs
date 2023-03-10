using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Designate a method as a query handler.
    /// </summary>
    /// <remarks>
    /// This is not inherited, so if a method is overridden, it must also have this attribute. The
    /// method must be a non-async method (i.e. cannot return a Task) and must return a non-void
    /// value.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class WorkflowQueryAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowQueryAttribute"/> class with the
        /// default name. See <see cref="Name" />.
        /// </summary>
        public WorkflowQueryAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowQueryAttribute"/> class with the
        /// given name.
        /// </summary>
        /// <param name="name">Workflow query name to use. See <see cref="Name" />.</param>
        public WorkflowQueryAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets the workflow query name. If this is unset, it defaults to the unqualified method
        /// name.
        /// </summary>
        public string? Name { get; }
    }
}
