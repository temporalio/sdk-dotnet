using System;
using System.Collections.Generic;
using Temporalio.Common;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for continue as new.
    /// </summary>
    public class ContinueAsNewOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the task queue to continue as new. If unset, defaults to current workflow's
        /// task queue.
        /// </summary>
        public string? TaskQueue { get; set; }

        /// <summary>
        /// Gets or sets the run timeout for continue as new. If unset, defaults to current
        /// workflow's run timeout.
        /// </summary>
        public TimeSpan? RunTimeout { get; set; }

        /// <summary>
        /// Gets or sets the task timeout for continue as new. If unset, defaults to current
        /// workflow's task timeout.
        /// </summary>
        public TimeSpan? TaskTimeout { get; set; }

        /// <summary>
        /// Gets or sets the retry policy for continue as new. If unset, defaults to the current
        /// workflow's retry policy.
        /// </summary>
        public RetryPolicy? RetryPolicy { get; set; }

        /// <summary>
        /// Gets or sets the memo for continue as new. If unset, default to the current workflow's
        /// memo.
        /// </summary>
        public IReadOnlyDictionary<string, object>? Memo { get; set; }

        /// <summary>
        /// Gets or sets the search attributes for continue as new. If unset, default to current
        /// workflow's search attribute.
        /// </summary>
        public SearchAttributeCollection? TypedSearchAttributes { get; set; }

        /// <summary>
        /// Gets or sets whether the continued Workflow should run on a worker with a compatible Build Id or not when
        /// using the Worker Versioning feature.
        /// </summary>
        public VersioningIntent? VersioningIntent { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}