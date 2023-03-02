using System;

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
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
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
    }
}
