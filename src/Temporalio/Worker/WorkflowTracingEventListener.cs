#pragma warning disable CS0162 // We intentionally have dead code in this file

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
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
        // Set to true to enable dumping of events (slow perf!)
        private const bool DumpEvents = false;

        // Set to true to enable tracking of task events (slow perf!)
        private const bool TrackTaskEvents = false;

        private const string TplEventSourceName = "System.Threading.Tasks.TplEventSource";
        private const int TplTaskWithSchedulerEventIdBegin = 7;
        private const int TplTaskWithSchedulerEventIdEnd = 11;
        private const EventKeywords TplTasksKeywords = (EventKeywords)2;

        private const string FrameworkEventSourceName = "System.Diagnostics.Eventing.FrameworkEventSource";
        private const EventKeywords FrameworkThreadTransferKeywords = (EventKeywords)0x0010;
        private const EventTask FrameworkThreadTransferTask = (EventTask)3;

        private static readonly Lazy<WorkflowTracingEventListener> LazyInstance = new(() => new());

        // Only non-null when TrackTaskEvents is true
        private readonly Dictionary<int, List<TaskEvent>>? taskEvents;

        // Locks the fields below it only
        private readonly object eventSourceLock = new();
        private EventSource? tplEventSource;
        private EventSource? frameworkEventSource;
        private int eventSourceListenerCount;

        private WorkflowTracingEventListener()
        {
            if (TrackTaskEvents)
            {
                taskEvents = new();
            }
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
        protected override void OnEventWritten(EventWrittenEventArgs evt)
        {
            // No need to invoke base class
            if (DumpEvents)
            {
                Console.WriteLine(EventToString(evt));
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
            if (evt.EventSource.Guid == tplEventSource?.Guid)
            {
                OnTplEventWritten(instance, evt);
            }
            else if (evt.EventSource.Guid == frameworkEventSource?.Guid)
            {
                OnFrameworkEventWritten(instance, evt);
            }
        }

        private static string EventToString(EventWrittenEventArgs evt, string prefix = "Event: ")
        {
            var values = new List<KeyValuePair<string, object?>>
            {
                new("EventSource", evt.EventSource.Name),
                new("EventName", evt.EventName),
                new("EventId", evt.EventId),
                new("CurrSched", TaskScheduler.Current?.GetType()),
            };
            if (evt.PayloadNames != null && evt.Payload != null)
            {
                for (var i = 0; i < evt.PayloadNames.Count; i++)
                {
                    values.Add(new(evt.PayloadNames[i], evt.Payload[i]));
                }
            }
            var bld = new StringBuilder(prefix).
                AppendFormat("EventSource = {0}, ", evt.EventSource.Name).
                AppendFormat("EventName = {0}, ", evt.EventName).
                AppendFormat("EventId = {0}, ", evt.EventId).
                AppendFormat("CurrSched = {0}", TaskScheduler.Current?.GetType());
            if (evt.PayloadNames != null && evt.Payload != null)
            {
                for (var i = 0; i < evt.PayloadNames.Count; i++)
                {
                    bld.AppendFormat(", {0} = {1}", evt.PayloadNames[i], evt.Payload[i]);
                }
            }
            return bld.ToString();
        }

        private static void OnFrameworkEventWritten(
            WorkflowInstance instance, EventWrittenEventArgs evt)
        {
            if (evt.Task == FrameworkThreadTransferTask)
            {
                instance.SetCurrentActivationException(
                    new InvalidWorkflowSchedulerException(
                        "Workflow attempted to perform a thread transfer task which is non-deterministic. " +
                        "This can be caused by timers or other non-deterministic async calls.",
                        Environment.StackTrace));
            }
        }

        private static int TaskIdOfEvent(EventWrittenEventArgs evt)
        {
            if (PayloadOfEvent(evt, "TaskId") is int taskId)
            {
                return taskId;
            }
            return -1;
        }

        private static object? PayloadOfEvent(EventWrittenEventArgs evt, string name)
        {
            var index = evt.PayloadNames?.IndexOf(name) ?? -1;
            return index < 0 ? null : evt.Payload?.ElementAtOrDefault(index);
        }

        private void OnTplEventWritten(
            WorkflowInstance instance, EventWrittenEventArgs evt)
        {
            // Track event if wanted
            if (TrackTaskEvents)
            {
                TrackTaskEvent(evt);
            }

            if (evt.EventId >= TplTaskWithSchedulerEventIdBegin &&
                evt.EventId <= TplTaskWithSchedulerEventIdEnd &&
                instance.Id != evt.Payload?[0] as int?)
            {
                // Dump failure event
                if (TrackTaskEvents)
                {
                    Console.WriteLine(FailureEventToString(evt));
                }
                // We override the stack trace so it has the full value all the way back
                // to user code. We do not need to sanitize the trace, it is ok to show that it
                // comes all the way through this listener.
                instance.SetCurrentActivationException(
                    new InvalidWorkflowSchedulerException(
                        "Task during workflow run was not scheduled on workflow scheduler",
                        Environment.StackTrace));
            }
        }

        private void EnableNeededTplEvents(EventSource eventSource)
        {
            // Get all events if tracking task events
            if (TrackTaskEvents)
            {
                EnableEvents(eventSource, EventLevel.LogAlways);
            }
            else
            {
                EnableEvents(
                    eventSource,
                    EventLevel.Informational,
                    TplTasksKeywords);
            }
        }

        private void EnableNeededFrameworkEvents(EventSource eventSource) =>
            EnableEvents(
                eventSource,
                EventLevel.Informational,
                FrameworkThreadTransferKeywords);

        private void TrackTaskEvent(EventWrittenEventArgs evt)
        {
            var taskId = TaskIdOfEvent(evt);
            if (taskId < 0)
            {
                return;
            }
            lock (taskEvents!)
            {
                if (!taskEvents!.TryGetValue(taskId, out var eventList))
                {
                    eventList = new();
                    taskEvents![taskId] = eventList;
                }
                eventList.Add(new(evt));
            }
        }

        private string FailureEventToString(
            EventWrittenEventArgs evt,
            string prefix = "Task that failed: ",
            string indent = "")
        {
            var taskId = TaskIdOfEvent(evt);
            if (taskId < 0)
            {
                return $"{prefix}: <unknown>";
            }
            var bld = new StringBuilder(indent).
                Append(prefix).Append(taskId).Append(", events:").AppendLine();
            lock (taskEvents!)
            {
                if (taskEvents!.TryGetValue(taskId, out var eventList))
                {
                    foreach (var taskEvent in eventList)
                    {
                        bld.Append(indent).Append("  ").Append(EventToString(taskEvent.Event)).
                            Append(", Stack =").AppendLine().Append(taskEvent.Stack).AppendLine();
                    }
                }
            }
            return bld.ToString();
        }

        private record TaskEvent(int TaskId, EventWrittenEventArgs Event, string Stack)
        {
            internal TaskEvent(EventWrittenEventArgs evt)
                : this(TaskIdOfEvent(evt), evt, new System.Diagnostics.StackTrace().ToString())
            {
            }
        }
    }
}