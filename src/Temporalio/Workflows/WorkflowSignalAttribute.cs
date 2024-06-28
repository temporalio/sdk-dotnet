using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Designate a method as a signal handler.
    /// </summary>
    /// <remarks>
    /// This is not inherited, so if a method is overridden, it must also have this attribute. The
    /// method must be a public non-static and return a task (not a task with a type embedded).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class WorkflowSignalAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowSignalAttribute"/> class with the
        /// default name. See <see cref="Name" />.
        /// </summary>
        public WorkflowSignalAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowSignalAttribute"/> class with the
        /// given name.
        /// </summary>
        /// <param name="name">Workflow signal name to use. See <see cref="Name" />.</param>
        public WorkflowSignalAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets the workflow signal name. If this is unset, it defaults to the unqualified method
        /// name. If the method name ends with "Async", that is trimmed off when creating the
        /// default.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the signal is dynamic. If a signal is dynamic,
        /// it cannot be given a name in this attribute and the method must accept a string name and
        /// an array of <see cref="Converters.IRawValue" />.
        /// </summary>
        public bool Dynamic { get; set; }

        /// <summary>
        /// Gets or sets the actions taken if a workflow exits with a running instance of this
        /// handler. Default is <see cref="HandlerUnfinishedPolicy.WarnAndAbandon" />.
        /// </summary>
        public HandlerUnfinishedPolicy UnfinishedPolicy { get; set; } = HandlerUnfinishedPolicy.WarnAndAbandon;
    }
}
