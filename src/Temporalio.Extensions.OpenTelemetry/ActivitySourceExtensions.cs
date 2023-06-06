using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Temporalio.Workflows;

namespace Temporalio.Extensions.OpenTelemetry
{
    /// <summary>
    /// Temporal extensions to <see cref="ActivitySource" />.
    /// </summary>
    public static class ActivitySourceExtensions
    {
        /// <summary>
        /// Create a <see cref="WorkflowDiagnosticActivity" /> and set it as current. Can only be
        /// called within a workflow. WARNING: This method should not be considered stable and may
        /// change.
        /// </summary>
        /// <param name="source">The source to start an internal activity on if needed.</param>
        /// <param name="name">Operation name.</param>
        /// <param name="kind">Activity kind.</param>
        /// <param name="tags">Tags for the workflow activity. See <c>inheritParentTags</c>.</param>
        /// <param name="links">Activity links.</param>
        /// <param name="evenOnReplay">If false (the default), the internal activity on the source
        /// will only be started when the workflow isn't replaying. If true, the internal activity
        /// will always be started.</param>
        /// <param name="inheritParentTags">If true (the default), any tags on the parent
        /// WorkflowDiagnosticActivity will be prepended to any given tags and used. If false, only
        /// given tags are used. This differs from standard <c>ActivitySource.StartActivity</c>
        /// behavior by intention so workflow tags can be propagated.</param>
        /// <param name="updateActivity">If present and an internal activity is started from the
        /// source, this is invoked with the started activity before it is immediately stopped.
        /// </param>
        /// <returns>The started workflow activity set as current.</returns>
        /// <remarks>
        /// This starts and immediately stops an <see cref="Activity" /> since workflows do not
        /// support long-running .NET activities because workflows are interruptible/resumable. This
        /// method should be used instead of any other call on <see cref="ActivitySource" /> inside
        /// of workflows as it is replay safe. This will always return a value even if there is no
        /// .NET activity created (e.g. no listener, during replay, etc). Users must call
        /// <c>Dispose</c> on the activity to remove it from the current context.
        /// </remarks>
        public static WorkflowDiagnosticActivity TrackWorkflowDiagnosticActivity(
            this ActivitySource source,
            string name,
            ActivityKind kind = ActivityKind.Internal,
            IEnumerable<KeyValuePair<string, object?>>? tags = null,
            IEnumerable<ActivityLink>? links = null,
            bool evenOnReplay = false,
            bool inheritParentTags = true,
            Action<Activity>? updateActivity = null)
        {
            if (!Workflow.InWorkflow)
            {
                throw new InvalidOperationException("Not in a workflow");
            }

            // Inherit (prepend) parent tags
            IReadOnlyCollection<KeyValuePair<string, object?>>? combinedTags;
            if (inheritParentTags &&
                WorkflowDiagnosticActivity.Current?.Tags is IReadOnlyCollection<KeyValuePair<string, object?>> parentTags)
            {
                if (tags == null)
                {
                    combinedTags = parentTags;
                }
                else
                {
                    combinedTags = parentTags.Concat(tags).ToList();
                }
            }
            else
            {
                combinedTags = tags?.ToList();
            }

            // Try to set the parent context as the current _workflow_ activity context (which may
            // not have an activity itself)
            var parentContext = WorkflowDiagnosticActivity.Current?.Context ?? default;
            Activity? activity = null;
            if (!Workflow.Unsafe.IsReplaying || evenOnReplay)
            {
                // Start/stop immediately
                activity = source.StartActivity(name, kind, parentContext, tags, links);
                if (activity != null)
                {
                    updateActivity?.Invoke(activity);
                    activity.Stop();
                }
            }
            return new(activity, combinedTags);
        }
    }
}