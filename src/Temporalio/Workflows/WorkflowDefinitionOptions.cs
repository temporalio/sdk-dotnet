using System;
using Temporalio.Common;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Additional options for a workflow definition that may be returned by
    /// a function annotated with <see cref="WorkflowDynamicOptionsAttribute"/>.
    /// </summary>
    public class WorkflowDefinitionOptions
    {
        /// <summary>
        /// Gets the types of exceptions that, if a workflow-thrown exception extends, will
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
        public Type[]? FailureExceptionTypes { get; init; }

        /// <summary>
        /// Gets the versioning behavior to use for this workflow.
        /// </summary>
        /// <remarks>WARNING: Deployment-based versioning is experimental and APIs may
        /// change.</remarks>
        public VersioningBehavior VersioningBehavior { get; init; }
    }
}
