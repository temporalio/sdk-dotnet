using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Temporalio.Workflows;

namespace Temporalio.Extensions.OpenTelemetry
{
    /// <summary>
    /// Representation of a workflow-safe diagnostic activity.
    /// </summary>
    /// <remarks>
    /// This wraps an <see cref="Activity" />, but the inner activity may not be present because
    /// either this activity was created while replaying or the inner activity was not created by
    /// the <see cref="ActivitySource" /> because there are no listeners. Callers must call
    /// <see cref="Dispose()" /> to remove the created workflow activity from current.
    /// </remarks>
    public class WorkflowActivity : IDisposable
    {
        private static readonly AsyncLocal<WorkflowActivity?> CurrentLocal = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowActivity"/> class and sets as
        /// current.
        /// </summary>
        /// <param name="context">Context for the activity.</param>
        /// <param name="tags">Tags for the activity.</param>
        internal WorkflowActivity(
            ActivityContext context,
            IReadOnlyCollection<KeyValuePair<string, object?>>? tags)
        {
            // Trust thread safety only in workflow
            if (!Workflow.InWorkflow)
            {
                throw new InvalidOperationException("Not in workflow");
            }
            Context = context;
            Parent = Current;
            Tags = tags;
            CurrentLocal.Value = this;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowActivity"/> class and sets as
        /// current.
        /// </summary>
        /// <param name="activity">Inner activity.</param>
        /// <param name="tags">Tags for the activity.</param>
        internal WorkflowActivity(
            Activity? activity,
            IReadOnlyCollection<KeyValuePair<string, object?>>? tags)
        {
            // Trust thread safety only in workflow
            if (!Workflow.InWorkflow)
            {
                throw new InvalidOperationException("Not in workflow");
            }
            Activity = activity;
            Context = activity?.Context ?? Current?.Context ?? default;
            Parent = Current;
            Tags = tags;
            CurrentLocal.Value = this;
        }

        /// <summary>
        /// Gets the current workflow activity. This is never null <see cref="TracingInterceptor" />
        /// is configured (and something illegal didn't happen like leaving the async-local
        /// context).
        /// </summary>
        public static WorkflowActivity? Current => CurrentLocal.Value;

        /// <summary>
        /// Gets the parent of this activity, i.e. the <see cref="Current" /> when this activity was
        /// created.
        /// </summary>
        public WorkflowActivity? Parent { get; private init; }

        /// <summary>
        /// Gets the diagnostic activity context for this workflow activity.
        /// </summary>
        public ActivityContext Context { get; private init; }

        /// <summary>
        /// Gets the inner diagnostic activity for this workflow activity. This may be null if the
        /// activity was not created (e.g. during replay or activity source returned null). This
        /// activity has already been started and stopped.
        /// </summary>
        public Activity? Activity { get; private init; }

        /// <summary>
        /// Gets the set of tags for this activity. This is separate from <see cref="Activity" />
        /// tags because not only might these exist when the activity doesn't, but creating an
        /// activity needs them for default tag inheritance.
        /// </summary>
        public IReadOnlyCollection<KeyValuePair<string, object?>>? Tags { get; private init; }

        /// <summary>
        /// Create and set a workflow activity as current for the given context.
        /// </summary>
        /// <param name="context">Context for the current activity.</param>
        /// <param name="tags">Optional tags to set for the context.</param>
        /// <returns>Created workflow activity.</returns>
        public static WorkflowActivity AttachFromContext(
            ActivityContext context,
            IReadOnlyCollection<KeyValuePair<string, object?>>? tags = null) =>
            new(context, tags);

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Whether disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            CurrentLocal.Value = Parent;
        }
    }
}