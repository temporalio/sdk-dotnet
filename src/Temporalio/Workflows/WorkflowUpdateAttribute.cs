using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Designate a method as an update handler.
    /// </summary>
    /// <remarks>
    /// This is not inherited, so if a method is overridden, it must also have this attribute. The
    /// method must be public, non-static, and return a task (can be a task with result).
    /// </remarks>
    /// <remarks>
    /// Update failure/suspend behavior is the same as workflows, based on failure exception types.
    /// See <see cref="WorkflowAttribute.FailureExceptionTypes" /> or
    /// <see cref="Worker.TemporalWorkerOptions.WorkflowFailureExceptionTypes"/> to change the
    /// default behavior.
    /// </remarks>
    /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class WorkflowUpdateAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateAttribute"/> class with the
        /// default name. See <see cref="Name" />.
        /// </summary>
        public WorkflowUpdateAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateAttribute"/> class with the
        /// given name.
        /// </summary>
        /// <param name="name">Workflow update name to use. See <see cref="Name" />.</param>
        public WorkflowUpdateAttribute(string name) => Name = name;

        /// <summary>
        /// Gets the workflow update name. If this is unset, it defaults to the unqualified method
        /// name. If the method name ends with "Async", that is trimmed off when creating the
        /// default.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the update is dynamic. If a update is dynamic,
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