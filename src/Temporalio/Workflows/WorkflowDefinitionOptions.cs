using System;
using Temporalio.Common;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Additional options for a workflow definition that may be returned by
    /// a function annotated with <see cref="WorkflowDynamicOptionsAttribute"/>.
    /// </summary>
    public class WorkflowDefinitionOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the types of exceptions that, if a workflow-thrown exception extends, will
        /// cause the workflow/update to fail instead of suspending the workflow via task failure.
        /// These are applied in addition to
        /// <see cref="Worker.TemporalWorkerOptions.WorkflowFailureExceptionTypes" /> for the
        /// overall worker. If <c>typeof(Exception)</c> is set, it effectively will fail a
        /// workflow/update in all user exception cases.
        /// </summary>
        /// <remarks>
        /// If set, overrides any value set on <see cref="WorkflowAttribute.FailureExceptionTypes"/>.
        /// </remarks>
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
        /// <remarks>
        /// If set to a non-unspecified value, overrides any value set on
        /// <see cref="WorkflowAttribute.VersioningBehavior"/>.
        /// </remarks>
        public VersioningBehavior VersioningBehavior { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public object Clone() => (WorkflowDefinitionOptions)MemberwiseClone();
    }
}
