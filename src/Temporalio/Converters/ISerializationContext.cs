#pragma warning disable CA1724 // We don't care that Workflow/Activity clash with API namespace

using System;

namespace Temporalio.Converters
{
    /// <summary>
    /// Base interface for all serialization contexts that can be passed in to
    /// <see cref="IWithSerializationContext{TResult}.WithSerializationContext(ISerializationContext)"/>.
    /// The implementations used by the SDK are <see cref="Activity"/>, <see cref="Workflow"/>, and
    /// <see cref="Nexus"/>.
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
            /// Gets the ID for the workflow, or null if not applicable (e.g. standalone activities).
            /// </summary>
            /// <remarks>
            /// Note, when creating/describing schedules, this may be the workflow ID prefix as
            /// configured, not the final workflow ID when the workflow is created by the schedule.
            /// </remarks>
            string? WorkflowId { get; }
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
        /// <param name="ActivityId">Activity ID. Only set for standalone activities.</param>
        /// <param name="WorkflowId">Workflow ID. Only set for workflow activities. Note, when
        /// creating/describing schedules, this may be the workflow ID prefix as configured, not
        /// the final workflow ID when the workflow is created by the schedule.</param>
        /// <param name="WorkflowType">Workflow type. Only set for workflow activities.</param>
        /// <param name="ActivityType">Activity type.</param>
        /// <param name="ActivityTaskQueue">Activity task queue.</param>
        /// <param name="IsLocal">Whether the activity is a local activity.</param>
        public sealed record Activity(
            string Namespace,
            string? ActivityId,
            string? WorkflowId,
            string? WorkflowType,
            [property: Obsolete("This value may not be set in some situations and should not be relied on.")]
            string? ActivityType,
            [property: Obsolete("This value may not be set in some situations and should not be relied on.")]
            string? ActivityTaskQueue,
            bool IsLocal) : IHasWorkflow
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Activity"/> class using info.
            /// </summary>
            /// <param name="info">Info available within an activity.</param>
            public Activity(Activities.ActivityInfo info)
                : this(
                    Namespace: info.Namespace,
                    ActivityId: info.ActivityId,
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

        /// <summary>
        /// Serialization context for Nexus operation payloads, identifying the Nexus endpoint,
        /// service, and resolved operation the payload belongs to. See
        /// <see cref="IWithSerializationContext{TResult}.WithSerializationContext(ISerializationContext)"/>
        /// for information on when this is made available to converters and codecs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Callers receive this context when encoding operation inputs and when decoding operation
        /// results and failures. Handlers receive it when decoding operation inputs, encoding
        /// synchronous operation results, and encoding failures produced while handling a Nexus
        /// task.
        /// </para>
        /// <para>
        /// The context is not propagated to the eventual result of an asynchronous operation,
        /// because the operation is completed out of band rather than by the task the handler was
        /// invoked for. A standalone operation handle uses the context of its start request,
        /// including when the start request returns an already-running operation; a handle obtained
        /// by operation ID without starting an operation has no endpoint, service, or operation to
        /// build a context from and therefore serializes without one.
        /// </para>
        /// <para>
        /// Failure conversion is not symmetric: a failure is encoded by the handler and decoded by
        /// the caller, so an implementation sees this context on only one side of a given failure,
        /// and for some operation paths it sees no context at all. Context-dependent encodings must
        /// therefore be self-describing, and decoders must keep accepting payloads that were
        /// encoded without a context.
        /// </para>
        /// <para>
        /// WARNING: This constructor may have required properties added and is not guaranteed to
        /// remain compatible from one version to the next.
        /// </para>
        /// </remarks>
        /// <param name="Endpoint">Nexus endpoint name.</param>
        /// <param name="Service">Nexus service name.</param>
        /// <param name="Operation">Resolved Nexus operation name.</param>
        public sealed record Nexus(
            string Endpoint,
            string Service,
            string Operation) : ISerializationContext;
    }
}
