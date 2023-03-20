using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using Temporalio.Exceptions;

namespace Temporalio.Worker
{
    /// <summary>
    /// Event source listener for catching invalid calls from inside workflows. Workers are expected
    /// to <see cref="Register" /> and <see cref="Unregister" /> so this can know when the count
    /// reaches 0.
    /// </summary>
    internal class WorkflowTaskEventListener : EventListener
    {
        private const bool DumpEvents = false;
        private const int TaskScheduledEventID = 7;
        private const int TraceOperationStartEventID = 14;
        private const EventKeywords TaskTransferKeywords = (EventKeywords)1;
        private const EventKeywords AsyncCausalityOperationKeywords = (EventKeywords)8;
        private static readonly Lazy<WorkflowTaskEventListener> LazyInstance = new(() => new());

        // Locks the two fields below it only
        private readonly object tplEventSourceLock = new();
        private EventSource? tplEventSource;
        private int tplEventSourceListenerCount;

        private WorkflowTaskEventListener()
        {
        }

        /// <summary>
        /// Gets a task event listener. Getting this the first time instantiates the event listener
        /// which adds it as a global listener. It should not be requested unless needed.
        /// </summary>
        public static WorkflowTaskEventListener Instance => LazyInstance.Value;

        /// <summary>
        /// Register this as needed by a worker.
        /// </summary>
        public void Register()
        {
            // We need to enable/disable under lock which should be cheap
            lock (tplEventSourceLock)
            {
                // Enable if we're the first and there is a source
                if (tplEventSourceListenerCount == 0 && tplEventSource != null)
                {
                    EnableNeededEvents(tplEventSource);
                }
                tplEventSourceListenerCount++;
            }
        }

        /// <summary>
        /// Unregister this as no longer needed by a worker.
        /// </summary>
        public void Unregister()
        {
            lock (tplEventSourceLock)
            {
                tplEventSourceListenerCount--;
                // Disable if we're the last and there is a source
                // TODO(cretz): Any perf concern with thrashing enable/disable if they are
                // starting/stopping workers frequently?
                if (tplEventSourceListenerCount == 0 && tplEventSource != null)
                {
                    DisableEvents(tplEventSource);
                }
            }
        }

        /// <inheritdoc />
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            base.OnEventSourceCreated(eventSource);
            if (eventSource.Name == "System.Threading.Tasks.TplEventSource")
            {
                lock (tplEventSourceLock)
                {
                    tplEventSource = eventSource;
                    // If there are listeners, enable now
                    if (tplEventSourceListenerCount > 0)
                    {
                        EnableNeededEvents(tplEventSource);
                    }
                }
            }
        }

        /// <inheritdoc />
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            // No need to invoke base class
            if (DumpEvents)
            {
#pragma warning disable CS0162 // Can be dead code, it's for development purposes only
                DumpEvent(eventData);
#pragma warning restore CS0162
            }
            // We only care if we're the current scheduler and we're tracing
            if (TaskScheduler.Current is not WorkflowInstance instance ||
                !instance.TaskTracingEnabled)
            {
                return;
            }
            var error = eventData.EventId switch
            {
                TaskScheduledEventID when instance.Id != eventData.Payload?[0] as int? =>
                    "Task scheduled during workflow run was not scheduled on workflow scheduler",
                TraceOperationStartEventID when eventData.Payload?[1] as string == "Task.Delay" =>
                    "Task.Delay cannot be used in workflows",
                _ => null,
            };
            if (error != null)
            {
                // We override the stack trace so it has the full value all the way back
                // to user code.
                // TODO(cretz): Trim off some of the internals?
                instance.SetCurrentActivationException(
                    new InvalidWorkflowOperationException(error, Environment.StackTrace));
            }
        }

        private static void DumpEvent(EventWrittenEventArgs evt) =>
            Console.WriteLine("TPL Event: {0}", string.Join(" -- ", new List<object?>()
            {
                evt.EventId,
                evt.EventName,
                evt.EventSource,
                evt.Keywords,
                evt.Message,
                evt.PayloadNames == null ? "<none>" : string.Join(",", evt.PayloadNames),
                evt.Payload == null ? "<none>" : string.Join(",", evt.Payload),
            }));

        private void EnableNeededEvents(EventSource eventSource) =>
            EnableEvents(
                eventSource,
                EventLevel.Informational,
                TaskTransferKeywords | AsyncCausalityOperationKeywords);
    }
}