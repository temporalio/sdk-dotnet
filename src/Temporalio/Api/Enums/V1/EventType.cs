// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/enums/v1/event_type.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.Enums.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/enums/v1/event_type.proto</summary>
  public static partial class EventTypeReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/enums/v1/event_type.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static EventTypeReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CiZ0ZW1wb3JhbC9hcGkvZW51bXMvdjEvZXZlbnRfdHlwZS5wcm90bxIVdGVt",
            "cG9yYWwuYXBpLmVudW1zLnYxKugTCglFdmVudFR5cGUSGgoWRVZFTlRfVFlQ",
            "RV9VTlNQRUNJRklFRBAAEikKJUVWRU5UX1RZUEVfV09SS0ZMT1dfRVhFQ1VU",
            "SU9OX1NUQVJURUQQARIrCidFVkVOVF9UWVBFX1dPUktGTE9XX0VYRUNVVElP",
            "Tl9DT01QTEVURUQQAhIoCiRFVkVOVF9UWVBFX1dPUktGTE9XX0VYRUNVVElP",
            "Tl9GQUlMRUQQAxIrCidFVkVOVF9UWVBFX1dPUktGTE9XX0VYRUNVVElPTl9U",
            "SU1FRF9PVVQQBBImCiJFVkVOVF9UWVBFX1dPUktGTE9XX1RBU0tfU0NIRURV",
            "TEVEEAUSJAogRVZFTlRfVFlQRV9XT1JLRkxPV19UQVNLX1NUQVJURUQQBhIm",
            "CiJFVkVOVF9UWVBFX1dPUktGTE9XX1RBU0tfQ09NUExFVEVEEAcSJgoiRVZF",
            "TlRfVFlQRV9XT1JLRkxPV19UQVNLX1RJTUVEX09VVBAIEiMKH0VWRU5UX1RZ",
            "UEVfV09SS0ZMT1dfVEFTS19GQUlMRUQQCRImCiJFVkVOVF9UWVBFX0FDVElW",
            "SVRZX1RBU0tfU0NIRURVTEVEEAoSJAogRVZFTlRfVFlQRV9BQ1RJVklUWV9U",
            "QVNLX1NUQVJURUQQCxImCiJFVkVOVF9UWVBFX0FDVElWSVRZX1RBU0tfQ09N",
            "UExFVEVEEAwSIwofRVZFTlRfVFlQRV9BQ1RJVklUWV9UQVNLX0ZBSUxFRBAN",
            "EiYKIkVWRU5UX1RZUEVfQUNUSVZJVFlfVEFTS19USU1FRF9PVVQQDhItCilF",
            "VkVOVF9UWVBFX0FDVElWSVRZX1RBU0tfQ0FOQ0VMX1JFUVVFU1RFRBAPEiUK",
            "IUVWRU5UX1RZUEVfQUNUSVZJVFlfVEFTS19DQU5DRUxFRBAQEhwKGEVWRU5U",
            "X1RZUEVfVElNRVJfU1RBUlRFRBAREhoKFkVWRU5UX1RZUEVfVElNRVJfRklS",
            "RUQQEhIdChlFVkVOVF9UWVBFX1RJTUVSX0NBTkNFTEVEEBMSMgouRVZFTlRf",
            "VFlQRV9XT1JLRkxPV19FWEVDVVRJT05fQ0FOQ0VMX1JFUVVFU1RFRBAUEioK",
            "JkVWRU5UX1RZUEVfV09SS0ZMT1dfRVhFQ1VUSU9OX0NBTkNFTEVEEBUSQwo/",
            "RVZFTlRfVFlQRV9SRVFVRVNUX0NBTkNFTF9FWFRFUk5BTF9XT1JLRkxPV19F",
            "WEVDVVRJT05fSU5JVElBVEVEEBYSQAo8RVZFTlRfVFlQRV9SRVFVRVNUX0NB",
            "TkNFTF9FWFRFUk5BTF9XT1JLRkxPV19FWEVDVVRJT05fRkFJTEVEEBcSOwo3",
            "RVZFTlRfVFlQRV9FWFRFUk5BTF9XT1JLRkxPV19FWEVDVVRJT05fQ0FOQ0VM",
            "X1JFUVVFU1RFRBAYEh4KGkVWRU5UX1RZUEVfTUFSS0VSX1JFQ09SREVEEBkS",
            "KgomRVZFTlRfVFlQRV9XT1JLRkxPV19FWEVDVVRJT05fU0lHTkFMRUQQGhIs",
            "CihFVkVOVF9UWVBFX1dPUktGTE9XX0VYRUNVVElPTl9URVJNSU5BVEVEEBsS",
            "MgouRVZFTlRfVFlQRV9XT1JLRkxPV19FWEVDVVRJT05fQ09OVElOVUVEX0FT",
            "X05FVxAcEjcKM0VWRU5UX1RZUEVfU1RBUlRfQ0hJTERfV09SS0ZMT1dfRVhF",
            "Q1VUSU9OX0lOSVRJQVRFRBAdEjQKMEVWRU5UX1RZUEVfU1RBUlRfQ0hJTERf",
            "V09SS0ZMT1dfRVhFQ1VUSU9OX0ZBSUxFRBAeEi8KK0VWRU5UX1RZUEVfQ0hJ",
            "TERfV09SS0ZMT1dfRVhFQ1VUSU9OX1NUQVJURUQQHxIxCi1FVkVOVF9UWVBF",
            "X0NISUxEX1dPUktGTE9XX0VYRUNVVElPTl9DT01QTEVURUQQIBIuCipFVkVO",
            "VF9UWVBFX0NISUxEX1dPUktGTE9XX0VYRUNVVElPTl9GQUlMRUQQIRIwCixF",
            "VkVOVF9UWVBFX0NISUxEX1dPUktGTE9XX0VYRUNVVElPTl9DQU5DRUxFRBAi",
            "EjEKLUVWRU5UX1RZUEVfQ0hJTERfV09SS0ZMT1dfRVhFQ1VUSU9OX1RJTUVE",
            "X09VVBAjEjIKLkVWRU5UX1RZUEVfQ0hJTERfV09SS0ZMT1dfRVhFQ1VUSU9O",
            "X1RFUk1JTkFURUQQJBI7CjdFVkVOVF9UWVBFX1NJR05BTF9FWFRFUk5BTF9X",
            "T1JLRkxPV19FWEVDVVRJT05fSU5JVElBVEVEECUSOAo0RVZFTlRfVFlQRV9T",
            "SUdOQUxfRVhURVJOQUxfV09SS0ZMT1dfRVhFQ1VUSU9OX0ZBSUxFRBAmEjMK",
            "L0VWRU5UX1RZUEVfRVhURVJOQUxfV09SS0ZMT1dfRVhFQ1VUSU9OX1NJR05B",
            "TEVEECcSMAosRVZFTlRfVFlQRV9VUFNFUlRfV09SS0ZMT1dfU0VBUkNIX0FU",
            "VFJJQlVURVMQKBIxCi1FVkVOVF9UWVBFX1dPUktGTE9XX0VYRUNVVElPTl9V",
            "UERBVEVfQURNSVRURUQQLxIxCi1FVkVOVF9UWVBFX1dPUktGTE9XX0VYRUNV",
            "VElPTl9VUERBVEVfQUNDRVBURUQQKRIxCi1FVkVOVF9UWVBFX1dPUktGTE9X",
            "X0VYRUNVVElPTl9VUERBVEVfUkVKRUNURUQQKhIyCi5FVkVOVF9UWVBFX1dP",
            "UktGTE9XX0VYRUNVVElPTl9VUERBVEVfQ09NUExFVEVEECsSNgoyRVZFTlRf",
            "VFlQRV9XT1JLRkxPV19QUk9QRVJUSUVTX01PRElGSUVEX0VYVEVSTkFMTFkQ",
            "LBI2CjJFVkVOVF9UWVBFX0FDVElWSVRZX1BST1BFUlRJRVNfTU9ESUZJRURf",
            "RVhURVJOQUxMWRAtEisKJ0VWRU5UX1RZUEVfV09SS0ZMT1dfUFJPUEVSVElF",
            "U19NT0RJRklFRBAuEigKJEVWRU5UX1RZUEVfTkVYVVNfT1BFUkFUSU9OX1ND",
            "SEVEVUxFRBAwEiYKIkVWRU5UX1RZUEVfTkVYVVNfT1BFUkFUSU9OX1NUQVJU",
            "RUQQMRIoCiRFVkVOVF9UWVBFX05FWFVTX09QRVJBVElPTl9DT01QTEVURUQQ",
            "MhIlCiFFVkVOVF9UWVBFX05FWFVTX09QRVJBVElPTl9GQUlMRUQQMxInCiNF",
            "VkVOVF9UWVBFX05FWFVTX09QRVJBVElPTl9DQU5DRUxFRBA0EigKJEVWRU5U",
            "X1RZUEVfTkVYVVNfT1BFUkFUSU9OX1RJTUVEX09VVBA1Ei8KK0VWRU5UX1RZ",
            "UEVfTkVYVVNfT1BFUkFUSU9OX0NBTkNFTF9SRVFVRVNURUQQNkKGAQoYaW8u",
            "dGVtcG9yYWwuYXBpLmVudW1zLnYxQg5FdmVudFR5cGVQcm90b1ABWiFnby50",
            "ZW1wb3JhbC5pby9hcGkvZW51bXMvdjE7ZW51bXOqAhdUZW1wb3JhbGlvLkFw",
            "aS5FbnVtcy5WMeoCGlRlbXBvcmFsaW86OkFwaTo6RW51bXM6OlYxYgZwcm90",
            "bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::Temporalio.Api.Enums.V1.EventType), }, null, null));
    }
    #endregion

  }
  #region Enums
  /// <summary>
  /// Whenever this list of events is changed do change the function shouldBufferEvent in mutableStateBuilder.go to make sure to do the correct event ordering
  /// </summary>
  public enum EventType {
    /// <summary>
    /// Place holder and should never appear in a Workflow execution history
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_UNSPECIFIED")] Unspecified = 0,
    /// <summary>
    /// Workflow execution has been triggered/started
    /// It contains Workflow execution inputs, as well as Workflow timeout configurations
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_STARTED")] WorkflowExecutionStarted = 1,
    /// <summary>
    /// Workflow execution has successfully completed and contains Workflow execution results
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_COMPLETED")] WorkflowExecutionCompleted = 2,
    /// <summary>
    /// Workflow execution has unsuccessfully completed and contains the Workflow execution error
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_FAILED")] WorkflowExecutionFailed = 3,
    /// <summary>
    /// Workflow execution has timed out by the Temporal Server
    /// Usually due to the Workflow having not been completed within timeout settings
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_TIMED_OUT")] WorkflowExecutionTimedOut = 4,
    /// <summary>
    /// Workflow Task has been scheduled and the SDK client should now be able to process any new history events
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_TASK_SCHEDULED")] WorkflowTaskScheduled = 5,
    /// <summary>
    /// Workflow Task has started and the SDK client has picked up the Workflow Task and is processing new history events
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_TASK_STARTED")] WorkflowTaskStarted = 6,
    /// <summary>
    /// Workflow Task has completed
    /// The SDK client picked up the Workflow Task and processed new history events
    /// SDK client may or may not ask the Temporal Server to do additional work, such as:
    /// EVENT_TYPE_ACTIVITY_TASK_SCHEDULED
    /// EVENT_TYPE_TIMER_STARTED
    /// EVENT_TYPE_UPSERT_WORKFLOW_SEARCH_ATTRIBUTES
    /// EVENT_TYPE_MARKER_RECORDED
    /// EVENT_TYPE_START_CHILD_WORKFLOW_EXECUTION_INITIATED
    /// EVENT_TYPE_REQUEST_CANCEL_EXTERNAL_WORKFLOW_EXECUTION_INITIATED
    /// EVENT_TYPE_SIGNAL_EXTERNAL_WORKFLOW_EXECUTION_INITIATED
    /// EVENT_TYPE_WORKFLOW_EXECUTION_COMPLETED
    /// EVENT_TYPE_WORKFLOW_EXECUTION_FAILED
    /// EVENT_TYPE_WORKFLOW_EXECUTION_CANCELED
    /// EVENT_TYPE_WORKFLOW_EXECUTION_CONTINUED_AS_NEW
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_TASK_COMPLETED")] WorkflowTaskCompleted = 7,
    /// <summary>
    /// Workflow Task encountered a timeout
    /// Either an SDK client with a local cache was not available at the time, or it took too long for the SDK client to process the task
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_TASK_TIMED_OUT")] WorkflowTaskTimedOut = 8,
    /// <summary>
    /// Workflow Task encountered a failure
    /// Usually this means that the Workflow was non-deterministic
    /// However, the Workflow reset functionality also uses this event
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_TASK_FAILED")] WorkflowTaskFailed = 9,
    /// <summary>
    /// Activity Task was scheduled
    /// The SDK client should pick up this activity task and execute
    /// This event type contains activity inputs, as well as activity timeout configurations
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_ACTIVITY_TASK_SCHEDULED")] ActivityTaskScheduled = 10,
    /// <summary>
    /// Activity Task has started executing
    /// The SDK client has picked up the Activity Task and is processing the Activity invocation
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_ACTIVITY_TASK_STARTED")] ActivityTaskStarted = 11,
    /// <summary>
    /// Activity Task has finished successfully
    /// The SDK client has picked up and successfully completed the Activity Task
    /// This event type contains Activity execution results
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_ACTIVITY_TASK_COMPLETED")] ActivityTaskCompleted = 12,
    /// <summary>
    /// Activity Task has finished unsuccessfully
    /// The SDK picked up the Activity Task but unsuccessfully completed it
    /// This event type contains Activity execution errors
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_ACTIVITY_TASK_FAILED")] ActivityTaskFailed = 13,
    /// <summary>
    /// Activity has timed out according to the Temporal Server
    /// Activity did not complete within the timeout settings
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_ACTIVITY_TASK_TIMED_OUT")] ActivityTaskTimedOut = 14,
    /// <summary>
    /// A request to cancel the Activity has occurred
    /// The SDK client will be able to confirm cancellation of an Activity during an Activity heartbeat
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_ACTIVITY_TASK_CANCEL_REQUESTED")] ActivityTaskCancelRequested = 15,
    /// <summary>
    /// Activity has been cancelled
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_ACTIVITY_TASK_CANCELED")] ActivityTaskCanceled = 16,
    /// <summary>
    /// A timer has started
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_TIMER_STARTED")] TimerStarted = 17,
    /// <summary>
    /// A timer has fired
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_TIMER_FIRED")] TimerFired = 18,
    /// <summary>
    /// A time has been cancelled
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_TIMER_CANCELED")] TimerCanceled = 19,
    /// <summary>
    /// A request has been made to cancel the Workflow execution
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_CANCEL_REQUESTED")] WorkflowExecutionCancelRequested = 20,
    /// <summary>
    /// SDK client has confirmed the cancellation request and the Workflow execution has been cancelled
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_CANCELED")] WorkflowExecutionCanceled = 21,
    /// <summary>
    /// Workflow has requested that the Temporal Server try to cancel another Workflow
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_REQUEST_CANCEL_EXTERNAL_WORKFLOW_EXECUTION_INITIATED")] RequestCancelExternalWorkflowExecutionInitiated = 22,
    /// <summary>
    /// Temporal Server could not cancel the targeted Workflow
    /// This is usually because the target Workflow could not be found
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_REQUEST_CANCEL_EXTERNAL_WORKFLOW_EXECUTION_FAILED")] RequestCancelExternalWorkflowExecutionFailed = 23,
    /// <summary>
    /// Temporal Server has successfully requested the cancellation of the target Workflow
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_EXTERNAL_WORKFLOW_EXECUTION_CANCEL_REQUESTED")] ExternalWorkflowExecutionCancelRequested = 24,
    /// <summary>
    /// A marker has been recorded.
    /// This event type is transparent to the Temporal Server
    /// The Server will only store it and will not try to understand it.
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_MARKER_RECORDED")] MarkerRecorded = 25,
    /// <summary>
    /// Workflow has received a Signal event
    /// The event type contains the Signal name, as well as a Signal payload
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_SIGNALED")] WorkflowExecutionSignaled = 26,
    /// <summary>
    /// Workflow execution has been forcefully terminated
    /// This is usually because the terminate Workflow API was called
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_TERMINATED")] WorkflowExecutionTerminated = 27,
    /// <summary>
    /// Workflow has successfully completed and a new Workflow has been started within the same transaction
    /// Contains last Workflow execution results as well as new Workflow execution inputs
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_CONTINUED_AS_NEW")] WorkflowExecutionContinuedAsNew = 28,
    /// <summary>
    /// Temporal Server will try to start a child Workflow
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_START_CHILD_WORKFLOW_EXECUTION_INITIATED")] StartChildWorkflowExecutionInitiated = 29,
    /// <summary>
    /// Child Workflow execution cannot be started/triggered
    /// Usually due to a child Workflow ID collision
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_START_CHILD_WORKFLOW_EXECUTION_FAILED")] StartChildWorkflowExecutionFailed = 30,
    /// <summary>
    /// Child Workflow execution has successfully started/triggered
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_CHILD_WORKFLOW_EXECUTION_STARTED")] ChildWorkflowExecutionStarted = 31,
    /// <summary>
    /// Child Workflow execution has successfully completed
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_CHILD_WORKFLOW_EXECUTION_COMPLETED")] ChildWorkflowExecutionCompleted = 32,
    /// <summary>
    /// Child Workflow execution has unsuccessfully completed
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_CHILD_WORKFLOW_EXECUTION_FAILED")] ChildWorkflowExecutionFailed = 33,
    /// <summary>
    /// Child Workflow execution has been cancelled
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_CHILD_WORKFLOW_EXECUTION_CANCELED")] ChildWorkflowExecutionCanceled = 34,
    /// <summary>
    /// Child Workflow execution has timed out by the Temporal Server
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_CHILD_WORKFLOW_EXECUTION_TIMED_OUT")] ChildWorkflowExecutionTimedOut = 35,
    /// <summary>
    /// Child Workflow execution has been terminated
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_CHILD_WORKFLOW_EXECUTION_TERMINATED")] ChildWorkflowExecutionTerminated = 36,
    /// <summary>
    /// Temporal Server will try to Signal the targeted Workflow
    /// Contains the Signal name, as well as a Signal payload
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_SIGNAL_EXTERNAL_WORKFLOW_EXECUTION_INITIATED")] SignalExternalWorkflowExecutionInitiated = 37,
    /// <summary>
    /// Temporal Server cannot Signal the targeted Workflow
    /// Usually because the Workflow could not be found
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_SIGNAL_EXTERNAL_WORKFLOW_EXECUTION_FAILED")] SignalExternalWorkflowExecutionFailed = 38,
    /// <summary>
    /// Temporal Server has successfully Signaled the targeted Workflow
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_EXTERNAL_WORKFLOW_EXECUTION_SIGNALED")] ExternalWorkflowExecutionSignaled = 39,
    /// <summary>
    /// Workflow search attributes should be updated and synchronized with the visibility store
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_UPSERT_WORKFLOW_SEARCH_ATTRIBUTES")] UpsertWorkflowSearchAttributes = 40,
    /// <summary>
    /// An update was admitted. Note that not all admitted updates result in this
    /// event. See UpdateAdmittedEventOrigin for situations in which this event
    /// is created.
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_UPDATE_ADMITTED")] WorkflowExecutionUpdateAdmitted = 47,
    /// <summary>
    /// An update was accepted (i.e. passed validation, perhaps because no validator was defined)
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_UPDATE_ACCEPTED")] WorkflowExecutionUpdateAccepted = 41,
    /// <summary>
    /// This event is never written to history.
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_UPDATE_REJECTED")] WorkflowExecutionUpdateRejected = 42,
    /// <summary>
    /// An update completed
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_EXECUTION_UPDATE_COMPLETED")] WorkflowExecutionUpdateCompleted = 43,
    /// <summary>
    /// Some property or properties of the workflow as a whole have changed by non-workflow code.
    /// The distinction of external vs. command-based modification is important so the SDK can
    /// maintain determinism when using the command-based approach.
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_PROPERTIES_MODIFIED_EXTERNALLY")] WorkflowPropertiesModifiedExternally = 44,
    /// <summary>
    /// Some property or properties of an already-scheduled activity have changed by non-workflow code.
    /// The distinction of external vs. command-based modification is important so the SDK can
    /// maintain determinism when using the command-based approach.
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_ACTIVITY_PROPERTIES_MODIFIED_EXTERNALLY")] ActivityPropertiesModifiedExternally = 45,
    /// <summary>
    /// Workflow properties modified by user workflow code
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_WORKFLOW_PROPERTIES_MODIFIED")] WorkflowPropertiesModified = 46,
    /// <summary>
    /// A Nexus operation was scheduled using a ScheduleNexusOperation command.
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_NEXUS_OPERATION_SCHEDULED")] NexusOperationScheduled = 48,
    /// <summary>
    /// An asynchronous Nexus operation was started by a Nexus handler.
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_NEXUS_OPERATION_STARTED")] NexusOperationStarted = 49,
    /// <summary>
    /// A Nexus operation completed successfully.
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_NEXUS_OPERATION_COMPLETED")] NexusOperationCompleted = 50,
    /// <summary>
    /// A Nexus operation failed.
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_NEXUS_OPERATION_FAILED")] NexusOperationFailed = 51,
    /// <summary>
    /// A Nexus operation completed as canceled.
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_NEXUS_OPERATION_CANCELED")] NexusOperationCanceled = 52,
    /// <summary>
    /// A Nexus operation timed out.
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_NEXUS_OPERATION_TIMED_OUT")] NexusOperationTimedOut = 53,
    /// <summary>
    /// A Nexus operation was requested to be canceled using a RequestCancelNexusOperation command.
    /// </summary>
    [pbr::OriginalName("EVENT_TYPE_NEXUS_OPERATION_CANCEL_REQUESTED")] NexusOperationCancelRequested = 54,
  }

  #endregion

}

#endregion Designer generated code
