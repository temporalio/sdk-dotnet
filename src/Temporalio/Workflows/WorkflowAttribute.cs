using System;
using Temporalio.Common;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Designate a type as a workflow.
    /// </summary>
    /// <remarks>
    /// This attribute is not inherited, so if a base class has this attribute the registered
    /// subclass must have it too. Workflows must have a no-arg constructor unless there is a
    /// constructor with <see cref="WorkflowInitAttribute" />. All workflows must have a single
    /// <see cref="WorkflowRunAttribute" />.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, Inherited = false)]
    public sealed class WorkflowAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowAttribute"/> class with the default
        /// name. See <see cref="Name" />.
        /// </summary>
        public WorkflowAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowAttribute"/> class with the given
        /// name.
        /// </summary>
        /// <param name="name">Workflow type name to use. See <see cref="Name" />.</param>
        public WorkflowAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets the workflow type name. If this is unset, it defaults to the unqualified type name.
        /// If the type is an interface and the first character is a capital "I" followed by another
        /// capital letter, the "I" is trimmed when creating the default name.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the workflow is dynamic. If a workflow is
        /// dynamic, it cannot be given a name in this attribute and the run method must be an array
        /// of <see cref="Converters.IRawValue" />.
        /// </summary>
        public bool Dynamic { get; set; }

        /// <summary>
        /// Gets or sets the types of exceptions that, if a workflow-thrown exception extends, will
        /// cause the workflow/update to fail instead of suspending the workflow via task failure.
        /// These are applied in addition to
        /// <see cref="Worker.TemporalWorkerOptions.WorkflowFailureExceptionTypes" /> for the
        /// overall worker. If <c>typeof(Exception)</c> is set, it effectively will fail a
        /// workflow/update in all user exception cases.
        /// </summary>
        /// <remarks>
        /// WARNING: This property is experimental and may change in the future. If unset
        /// (i.e. left null), currently the default is to only fail the workflow/update on
        /// <see cref="Exceptions.FailureException" /> + cancellation and suspend via task failure
        /// all others. But this default may change in the future.
        /// </remarks>
        public Type[]? FailureExceptionTypes { get; set; }

        /// <summary>
        /// Gets or sets the versioning behavior to use for this workflow.
        /// </summary>
        public VersioningBehavior VersioningBehavior { get; set; }
    }
}
