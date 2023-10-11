using System;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Designate a method as a validator for an update.
    /// </summary>
    /// <remarks>
    /// The method must be a public, non-static, and return <c>void</c>. The single argument must
    /// ne the <c>nameof</c> the update method it is validating and the parameters must match.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class WorkflowUpdateValidatorAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateValidatorAttribute"/> class
        /// with the name of the update method.
        /// </summary>
        /// <param name="updateMethod">Name of the update method in this same class.</param>
        public WorkflowUpdateValidatorAttribute(string updateMethod) => UpdateMethod = updateMethod;

        /// <summary>
        /// Gets the name of the update method this attribute applies to.
        /// </summary>
        public string UpdateMethod { get; private init; }
    }
}