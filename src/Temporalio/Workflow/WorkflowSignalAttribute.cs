using System;

namespace Temporalio.Workflow
{
    /// <summary>
    /// Designate a method as a signal handler.
    /// </summary>
    /// <remarks>
    /// This is not inherited, so if a method is overridden, it must also have this attribute. The
    /// method must return a task (not a task with a type embedded).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class WorkflowSignalAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowSignalAttribute"/> class with the
        /// default name.
        /// </summary>
        /// <seealso cref="Name" />
        public WorkflowSignalAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowSignalAttribute"/> class with the
        /// given name.
        /// </summary>
        /// <param name="name">Workflow signal name to use.</param>
        public WorkflowSignalAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets or sets the workflow signal name. If this is unset, it defaults to the unqualified
        /// method name. If the method name ends with "Async", that is trimmed off when creating the
        /// default.
        /// </summary>
        public string? Name { get; set; }
    }
}
