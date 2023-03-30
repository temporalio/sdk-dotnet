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
    internal class WorkflowTracingEventListener : EventListener
    {
        // Set to true to enable dumping of events
        private const bool DumpEvents = false;

        private const string TplEventSourceName = "System.Threading.Tasks.TplEventSource";
        private const int TplTaskWithSchedulerEventIDBegin = 7;
        private const int TplTaskWithSchedulerEventIDEnd = 11;
        private const EventKeywords TplTasksKeywords = (EventKeywords)2;

        private const string FrameworkEventSourceName = "System.Diagnostics.Eventing.FrameworkEventSource";
        private const EventKeywords FrameworkThreadTransferKeywords = (EventKeywords)0x0010;
        private const EventTask FrameworkThreadTransferTask = (EventTask)3;

        private static readonly Lazy<WorkflowTracingEventListener> LazyInstance = new(() => new());

        // Locks the fields below it only
        private readonly object eventSourceLock = new();
        private EventSource? tplEventSource;
        private EventSource? frameworkEventSource;
        private int eventSourceListenerCount;

        private WorkflowTracingEventListener()
        {
        }

        /// <summary>
        /// Gets a task event listener. Getting this the first time instantiates the event listener
        /// which adds it as a global listener. It should not be requested unless needed.
        /// </summary>
        public static WorkflowTracingEventListener Instance => LazyInstance.Value;

        /// <summary>
        /// Register this as needed by a worker.
        /// </summary>
        public void Register()
        {
            // We need to enable/disable under lock which should be cheap
            lock (eventSourceLock)
            {
                // Enable if we're the first and there is a source
                if (eventSourceListenerCount == 0 && tplEventSource != null)
                {
                    EnableNeededTplEvents(tplEventSource);
                }
                if (eventSourceListenerCount == 0 && frameworkEventSource != null)
                {
                    EnableNeededFrameworkEvents(frameworkEventSource);
                }
                eventSourceListenerCount++;
            }
        }

        /// <summary>
        /// Unregister this as no longer needed by a worker.
        /// </summary>
        public void Unregister()
        {
            lock (eventSourceLock)
            {
                eventSourceListenerCount--;
                // Disable if we're the last and there is a source
                // TODO(cretz): Any perf concern with thrashing enable/disable if they are
                // starting/stopping workers frequently?
                if (eventSourceListenerCount == 0 && tplEventSource != null)
                {
                    DisableEvents(tplEventSource);
                }
                if (eventSourceListenerCount == 0 && frameworkEventSource != null)
                {
                    DisableEvents(frameworkEventSource);
                }
            }
        }

        /// <inheritdoc />
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            base.OnEventSourceCreated(eventSource);
            if (eventSource.Name == TplEventSourceName)
            {
                lock (eventSourceLock)
                {
                    tplEventSource = eventSource;
                    // If there are listeners, enable now
                    if (eventSourceListenerCount > 0)
                    {
                        EnableNeededTplEvents(eventSource);
                    }
                }
            }
            else if (eventSource.Name == FrameworkEventSourceName)
            {
                lock (eventSourceLock)
                {
                    frameworkEventSource = eventSource;
                    // If there are listeners, enable now
                    if (eventSourceListenerCount > 0)
                    {
                        EnableNeededFrameworkEvents(eventSource);
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
            // We only care if we're the current scheduler and we're tracing. It is important that
            // this is early in this call because this will be executed on every task event. For the
            // non-workflow use case, this is essentially just a thread-local fetch + an instance of
            // check which is about as good as we can do performance wise.
            if (TaskScheduler.Current is not WorkflowInstance instance ||
                !instance.TracingEventsEnabled)
            {
                return;
            }
            if (eventData.EventSource.Guid == tplEventSource?.Guid)
            {
                OnTplEventWritten(instance, eventData);
            }
            else if (eventData.EventSource.Guid == frameworkEventSource?.Guid)
            {
                OnFrameworkEventWritten(instance, eventData);
            }
        }

        private static void DumpEvent(EventWrittenEventArgs evt) =>
            Console.WriteLine("Event: {0}", string.Join(" -- ", new List<object?>()
            {
                evt.EventSource.Name,
                evt.EventId,
                evt.EventName,
                evt.EventSource,
                evt.Keywords,
                evt.Message,
                $"Curr-Sched: {TaskScheduler.Current?.GetType()}",
                evt.PayloadNames == null ? "<none>" : string.Join(",", evt.PayloadNames),
                evt.Payload == null ? "<none>" : string.Join(",", evt.Payload),
            }));

        private static void OnTplEventWritten(
            WorkflowInstance instance, EventWrittenEventArgs eventData)
        {
            if (eventData.EventId >= TplTaskWithSchedulerEventIDBegin &&
                eventData.EventId <= TplTaskWithSchedulerEventIDEnd &&
                instance.Id != eventData.Payload?[0] as int?)
            {
                // We override the stack trace so it has the full value all the way back
                // to user code. We do not need to sanitize the trace, it is ok to show that it
                // comes all the way through this listener.
                instance.SetCurrentActivationException(
                    new InvalidWorkflowOperationException(
                        "Task during workflow run was not scheduled on workflow scheduler",
                        Environment.StackTrace));
            }
        }

        private static void OnFrameworkEventWritten(
            WorkflowInstance instance, EventWrittenEventArgs eventData)
        {
            if (eventData.Task == FrameworkThreadTransferTask)
            {
                instance.SetCurrentActivationException(
                    new InvalidWorkflowOperationException(
                        "Workflow attempted to perform a thread transfer task which is non-deterministic. " +
                        "This can be caused by timers or other non-deterministic async calls.",
                        Environment.StackTrace));
            }
        }

        private void EnableNeededTplEvents(EventSource eventSource) =>
            // EnableEvents(eventSource, EventLevel.LogAlways);
            EnableEvents(
                eventSource,
                EventLevel.Informational,
                TplTasksKeywords);

        private void EnableNeededFrameworkEvents(EventSource eventSource) =>
            // EnableEvents(eventSource, EventLevel.LogAlways);
            EnableEvents(
                eventSource,
                EventLevel.Informational,
                FrameworkThreadTransferKeywords);
    }
}