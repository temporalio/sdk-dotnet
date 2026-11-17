**Experimental**: Remove the generic `WorkflowOutboundInterceptor.ScheduleSystemNexusOperationAsync` hook and its public input type. System Nexus operations use only their operation-specific interceptor chain before scheduling.
**Experimental**: `SignalWithStartWorkflowOptions` takes only the workflow ID in its constructor. Set its `TaskQueue` property to override the caller's task queue.
