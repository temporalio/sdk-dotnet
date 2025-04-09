#pragma warning disable CA1724 // We don't care that Workflow/Activity clash with API namespace

namespace Temporalio.Converters
{
    /// <summary>
    /// Base interface for all serialization contexts that can be passed in to
    /// <see cref="IWithSerializationContext{TResult}.WithSerializationContext(ISerializationContext)"/>.
    /// The two implementations used by the SDK are <see cref="Activity"/> and <see cref="Workflow"/>.
    /// </summary>
    public interface ISerializationContext
    {
        /// <summary>
        /// Base interface for serialization contexts that have workflow information.
        /// </summary>
        public interface IHasWorkflow : ISerializationContext
        {
            /// <summary>
            /// Gets the namespace for the workflow.
            /// </summary>
#pragma warning disable CA1716 // We're ok with Namespace identifier here
            string Namespace { get; }
#pragma warning restore CA1716

            /// <summary>
            /// Gets the ID for the workflow.
            /// </summary>
            string WorkflowId { get; }
        }

        /// <summary>
        /// Serialization context for activities. See
        /// <see cref="IWithSerializationContext{TResult}.WithSerializationContext(ISerializationContext)"/>
        /// for information on when this is made available to converters and codecs.
        /// </summary>
        /// <remarks>
        /// WARNING: This constructor may have required properties added and is not guaranteed to
        /// remain compatible from one version to the next.
        /// </remarks>
        /// <param name="Namespace">Workflow/activity namespace.</param>
        /// <param name="WorkflowId">Workflow ID.</param>
        /// <param name="WorkflowType">Workflow Type.</param>
        /// <param name="ActivityType">Activity Type.</param>
        /// <param name="ActivityTaskQueue">ActivityTaskQueue.</param>
        /// <param name="IsLocal">Whether the activity is a local activity.</param>
        public sealed record Activity(
            string Namespace,
            string WorkflowId,
            string WorkflowType,
            string ActivityType,
            string ActivityTaskQueue,
            bool IsLocal) : IHasWorkflow
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Activity"/> class using info.
            /// </summary>
            /// <param name="info">Info available within an activity.</param>
            public Activity(Activities.ActivityInfo info)
                : this(
                    Namespace: info.WorkflowNamespace,
                    WorkflowId: info.WorkflowId,
                    WorkflowType: info.WorkflowType,
                    ActivityType: info.ActivityType,
                    ActivityTaskQueue: info.TaskQueue,
                    IsLocal: info.IsLocal)
            {
            }
        }

        /// <summary>
        /// Serialization context for workflows. See
        /// <see cref="IWithSerializationContext{TResult}.WithSerializationContext(ISerializationContext)"/>
        /// for information on when this is made available to converters and codecs.
        /// </summary>
        /// <remarks>
        /// WARNING: This constructor may have required properties added and is not guaranteed to
        /// remain compatible from one version to the next.
        /// </remarks>
        /// <param name="Namespace">Workflow namespace.</param>
        /// <param name="WorkflowId">Workflow ID.</param>
        public sealed record Workflow(
            string Namespace,
            string WorkflowId) : IHasWorkflow;
    }
}