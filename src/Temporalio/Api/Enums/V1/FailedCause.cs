// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/enums/v1/failed_cause.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.Enums.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/enums/v1/failed_cause.proto</summary>
  public static partial class FailedCauseReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/enums/v1/failed_cause.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static FailedCauseReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Cih0ZW1wb3JhbC9hcGkvZW51bXMvdjEvZmFpbGVkX2NhdXNlLnByb3RvEhV0",
            "ZW1wb3JhbC5hcGkuZW51bXMudjEq7hEKF1dvcmtmbG93VGFza0ZhaWxlZENh",
            "dXNlEioKJldPUktGTE9XX1RBU0tfRkFJTEVEX0NBVVNFX1VOU1BFQ0lGSUVE",
            "EAASMAosV09SS0ZMT1dfVEFTS19GQUlMRURfQ0FVU0VfVU5IQU5ETEVEX0NP",
            "TU1BTkQQARI/CjtXT1JLRkxPV19UQVNLX0ZBSUxFRF9DQVVTRV9CQURfU0NI",
            "RURVTEVfQUNUSVZJVFlfQVRUUklCVVRFUxACEkUKQVdPUktGTE9XX1RBU0tf",
            "RkFJTEVEX0NBVVNFX0JBRF9SRVFVRVNUX0NBTkNFTF9BQ1RJVklUWV9BVFRS",
            "SUJVVEVTEAMSOQo1V09SS0ZMT1dfVEFTS19GQUlMRURfQ0FVU0VfQkFEX1NU",
            "QVJUX1RJTUVSX0FUVFJJQlVURVMQBBI6CjZXT1JLRkxPV19UQVNLX0ZBSUxF",
            "RF9DQVVTRV9CQURfQ0FOQ0VMX1RJTUVSX0FUVFJJQlVURVMQBRI7CjdXT1JL",
            "RkxPV19UQVNLX0ZBSUxFRF9DQVVTRV9CQURfUkVDT1JEX01BUktFUl9BVFRS",
            "SUJVVEVTEAYSSQpFV09SS0ZMT1dfVEFTS19GQUlMRURfQ0FVU0VfQkFEX0NP",
            "TVBMRVRFX1dPUktGTE9XX0VYRUNVVElPTl9BVFRSSUJVVEVTEAcSRQpBV09S",
            "S0ZMT1dfVEFTS19GQUlMRURfQ0FVU0VfQkFEX0ZBSUxfV09SS0ZMT1dfRVhF",
            "Q1VUSU9OX0FUVFJJQlVURVMQCBJHCkNXT1JLRkxPV19UQVNLX0ZBSUxFRF9D",
            "QVVTRV9CQURfQ0FOQ0VMX1dPUktGTE9XX0VYRUNVVElPTl9BVFRSSUJVVEVT",
            "EAkSWApUV09SS0ZMT1dfVEFTS19GQUlMRURfQ0FVU0VfQkFEX1JFUVVFU1Rf",
            "Q0FOQ0VMX0VYVEVSTkFMX1dPUktGTE9XX0VYRUNVVElPTl9BVFRSSUJVVEVT",
            "EAoSPQo5V09SS0ZMT1dfVEFTS19GQUlMRURfQ0FVU0VfQkFEX0NPTlRJTlVF",
            "X0FTX05FV19BVFRSSUJVVEVTEAsSNwozV09SS0ZMT1dfVEFTS19GQUlMRURf",
            "Q0FVU0VfU1RBUlRfVElNRVJfRFVQTElDQVRFX0lEEAwSNgoyV09SS0ZMT1df",
            "VEFTS19GQUlMRURfQ0FVU0VfUkVTRVRfU1RJQ0tZX1RBU0tfUVVFVUUQDRJA",
            "CjxXT1JLRkxPV19UQVNLX0ZBSUxFRF9DQVVTRV9XT1JLRkxPV19XT1JLRVJf",
            "VU5IQU5ETEVEX0ZBSUxVUkUQDhJHCkNXT1JLRkxPV19UQVNLX0ZBSUxFRF9D",
            "QVVTRV9CQURfU0lHTkFMX1dPUktGTE9XX0VYRUNVVElPTl9BVFRSSUJVVEVT",
            "EA8SQwo/V09SS0ZMT1dfVEFTS19GQUlMRURfQ0FVU0VfQkFEX1NUQVJUX0NI",
            "SUxEX0VYRUNVVElPTl9BVFRSSUJVVEVTEBASMgouV09SS0ZMT1dfVEFTS19G",
            "QUlMRURfQ0FVU0VfRk9SQ0VfQ0xPU0VfQ09NTUFORBAREjUKMVdPUktGTE9X",
            "X1RBU0tfRkFJTEVEX0NBVVNFX0ZBSUxPVkVSX0NMT1NFX0NPTU1BTkQQEhI0",
            "CjBXT1JLRkxPV19UQVNLX0ZBSUxFRF9DQVVTRV9CQURfU0lHTkFMX0lOUFVU",
            "X1NJWkUQExItCilXT1JLRkxPV19UQVNLX0ZBSUxFRF9DQVVTRV9SRVNFVF9X",
            "T1JLRkxPVxAUEikKJVdPUktGTE9XX1RBU0tfRkFJTEVEX0NBVVNFX0JBRF9C",
            "SU5BUlkQFRI9CjlXT1JLRkxPV19UQVNLX0ZBSUxFRF9DQVVTRV9TQ0hFRFVM",
            "RV9BQ1RJVklUWV9EVVBMSUNBVEVfSUQQFhI0CjBXT1JLRkxPV19UQVNLX0ZB",
            "SUxFRF9DQVVTRV9CQURfU0VBUkNIX0FUVFJJQlVURVMQFxI2CjJXT1JLRkxP",
            "V19UQVNLX0ZBSUxFRF9DQVVTRV9OT05fREVURVJNSU5JU1RJQ19FUlJPUhAY",
            "EkgKRFdPUktGTE9XX1RBU0tfRkFJTEVEX0NBVVNFX0JBRF9NT0RJRllfV09S",
            "S0ZMT1dfUFJPUEVSVElFU19BVFRSSUJVVEVTEBkSRQpBV09SS0ZMT1dfVEFT",
            "S19GQUlMRURfQ0FVU0VfUEVORElOR19DSElMRF9XT1JLRkxPV1NfTElNSVRf",
            "RVhDRUVERUQQGhJACjxXT1JLRkxPV19UQVNLX0ZBSUxFRF9DQVVTRV9QRU5E",
            "SU5HX0FDVElWSVRJRVNfTElNSVRfRVhDRUVERUQQGxI9CjlXT1JLRkxPV19U",
            "QVNLX0ZBSUxFRF9DQVVTRV9QRU5ESU5HX1NJR05BTFNfTElNSVRfRVhDRUVE",
            "RUQQHBJECkBXT1JLRkxPV19UQVNLX0ZBSUxFRF9DQVVTRV9QRU5ESU5HX1JF",
            "UVVFU1RfQ0FOQ0VMX0xJTUlUX0VYQ0VFREVEEB0SRApAV09SS0ZMT1dfVEFT",
            "S19GQUlMRURfQ0FVU0VfQkFEX1VQREFURV9XT1JLRkxPV19FWEVDVVRJT05f",
            "TUVTU0FHRRAeEi8KK1dPUktGTE9XX1RBU0tfRkFJTEVEX0NBVVNFX1VOSEFO",
            "RExFRF9VUERBVEUQHxJGCkJXT1JLRkxPV19UQVNLX0ZBSUxFRF9DQVVTRV9C",
            "QURfU0NIRURVTEVfTkVYVVNfT1BFUkFUSU9OX0FUVFJJQlVURVMQIBJGCkJX",
            "T1JLRkxPV19UQVNLX0ZBSUxFRF9DQVVTRV9QRU5ESU5HX05FWFVTX09QRVJB",
            "VElPTlNfTElNSVRfRVhDRUVERUQQIRJMCkhXT1JLRkxPV19UQVNLX0ZBSUxF",
            "RF9DQVVTRV9CQURfUkVRVUVTVF9DQU5DRUxfTkVYVVNfT1BFUkFUSU9OX0FU",
            "VFJJQlVURVMQIhIvCitXT1JLRkxPV19UQVNLX0ZBSUxFRF9DQVVTRV9GRUFU",
            "VVJFX0RJU0FCTEVEECMq8wEKJlN0YXJ0Q2hpbGRXb3JrZmxvd0V4ZWN1dGlv",
            "bkZhaWxlZENhdXNlEjsKN1NUQVJUX0NISUxEX1dPUktGTE9XX0VYRUNVVElP",
            "Tl9GQUlMRURfQ0FVU0VfVU5TUEVDSUZJRUQQABJHCkNTVEFSVF9DSElMRF9X",
            "T1JLRkxPV19FWEVDVVRJT05fRkFJTEVEX0NBVVNFX1dPUktGTE9XX0FMUkVB",
            "RFlfRVhJU1RTEAESQwo/U1RBUlRfQ0hJTERfV09SS0ZMT1dfRVhFQ1VUSU9O",
            "X0ZBSUxFRF9DQVVTRV9OQU1FU1BBQ0VfTk9UX0ZPVU5EEAIqkQIKKkNhbmNl",
            "bEV4dGVybmFsV29ya2Zsb3dFeGVjdXRpb25GYWlsZWRDYXVzZRI/CjtDQU5D",
            "RUxfRVhURVJOQUxfV09SS0ZMT1dfRVhFQ1VUSU9OX0ZBSUxFRF9DQVVTRV9V",
            "TlNQRUNJRklFRBAAElkKVUNBTkNFTF9FWFRFUk5BTF9XT1JLRkxPV19FWEVD",
            "VVRJT05fRkFJTEVEX0NBVVNFX0VYVEVSTkFMX1dPUktGTE9XX0VYRUNVVElP",
            "Tl9OT1RfRk9VTkQQARJHCkNDQU5DRUxfRVhURVJOQUxfV09SS0ZMT1dfRVhF",
            "Q1VUSU9OX0ZBSUxFRF9DQVVTRV9OQU1FU1BBQ0VfTk9UX0ZPVU5EEAIq4gIK",
            "KlNpZ25hbEV4dGVybmFsV29ya2Zsb3dFeGVjdXRpb25GYWlsZWRDYXVzZRI/",
            "CjtTSUdOQUxfRVhURVJOQUxfV09SS0ZMT1dfRVhFQ1VUSU9OX0ZBSUxFRF9D",
            "QVVTRV9VTlNQRUNJRklFRBAAElkKVVNJR05BTF9FWFRFUk5BTF9XT1JLRkxP",
            "V19FWEVDVVRJT05fRkFJTEVEX0NBVVNFX0VYVEVSTkFMX1dPUktGTE9XX0VY",
            "RUNVVElPTl9OT1RfRk9VTkQQARJHCkNTSUdOQUxfRVhURVJOQUxfV09SS0ZM",
            "T1dfRVhFQ1VUSU9OX0ZBSUxFRF9DQVVTRV9OQU1FU1BBQ0VfTk9UX0ZPVU5E",
            "EAISTwpLU0lHTkFMX0VYVEVSTkFMX1dPUktGTE9XX0VYRUNVVElPTl9GQUlM",
            "RURfQ0FVU0VfU0lHTkFMX0NPVU5UX0xJTUlUX0VYQ0VFREVEEAMqhQMKFlJl",
            "c291cmNlRXhoYXVzdGVkQ2F1c2USKAokUkVTT1VSQ0VfRVhIQVVTVEVEX0NB",
            "VVNFX1VOU1BFQ0lGSUVEEAASJgoiUkVTT1VSQ0VfRVhIQVVTVEVEX0NBVVNF",
            "X1JQU19MSU1JVBABEi0KKVJFU09VUkNFX0VYSEFVU1RFRF9DQVVTRV9DT05D",
            "VVJSRU5UX0xJTUlUEAISLgoqUkVTT1VSQ0VfRVhIQVVTVEVEX0NBVVNFX1NZ",
            "U1RFTV9PVkVSTE9BREVEEAMSLgoqUkVTT1VSQ0VfRVhIQVVTVEVEX0NBVVNF",
            "X1BFUlNJU1RFTkNFX0xJTUlUEAQSKgomUkVTT1VSQ0VfRVhIQVVTVEVEX0NB",
            "VVNFX0JVU1lfV09SS0ZMT1cQBRImCiJSRVNPVVJDRV9FWEhBVVNURURfQ0FV",
            "U0VfQVBTX0xJTUlUEAYSNgoyUkVTT1VSQ0VfRVhIQVVTVEVEX0NBVVNFX1BF",
            "UlNJU1RFTkNFX1NUT1JBR0VfTElNSVQQByqPAQoWUmVzb3VyY2VFeGhhdXN0",
            "ZWRTY29wZRIoCiRSRVNPVVJDRV9FWEhBVVNURURfU0NPUEVfVU5TUEVDSUZJ",
            "RUQQABImCiJSRVNPVVJDRV9FWEhBVVNURURfU0NPUEVfTkFNRVNQQUNFEAES",
            "IwofUkVTT1VSQ0VfRVhIQVVTVEVEX1NDT1BFX1NZU1RFTRACQogBChhpby50",
            "ZW1wb3JhbC5hcGkuZW51bXMudjFCEEZhaWxlZENhdXNlUHJvdG9QAVohZ28u",
            "dGVtcG9yYWwuaW8vYXBpL2VudW1zL3YxO2VudW1zqgIXVGVtcG9yYWxpby5B",
            "cGkuRW51bXMuVjHqAhpUZW1wb3JhbGlvOjpBcGk6OkVudW1zOjpWMWIGcHJv",
            "dG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::Temporalio.Api.Enums.V1.WorkflowTaskFailedCause), typeof(global::Temporalio.Api.Enums.V1.StartChildWorkflowExecutionFailedCause), typeof(global::Temporalio.Api.Enums.V1.CancelExternalWorkflowExecutionFailedCause), typeof(global::Temporalio.Api.Enums.V1.SignalExternalWorkflowExecutionFailedCause), typeof(global::Temporalio.Api.Enums.V1.ResourceExhaustedCause), typeof(global::Temporalio.Api.Enums.V1.ResourceExhaustedScope), }, null, null));
    }
    #endregion

  }
  #region Enums
  /// <summary>
  /// Workflow tasks can fail for various reasons. Note that some of these reasons can only originate
  /// from the server, and some of them can only originate from the SDK/worker.
  /// </summary>
  public enum WorkflowTaskFailedCause {
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_UNSPECIFIED")] Unspecified = 0,
    /// <summary>
    /// Between starting and completing the workflow task (with a workflow completion command), some
    /// new command (like a signal) was processed into workflow history. The outstanding task will be
    /// failed with this reason, and a worker must pick up a new task.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_UNHANDLED_COMMAND")] UnhandledCommand = 1,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_SCHEDULE_ACTIVITY_ATTRIBUTES")] BadScheduleActivityAttributes = 2,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_REQUEST_CANCEL_ACTIVITY_ATTRIBUTES")] BadRequestCancelActivityAttributes = 3,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_START_TIMER_ATTRIBUTES")] BadStartTimerAttributes = 4,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_CANCEL_TIMER_ATTRIBUTES")] BadCancelTimerAttributes = 5,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_RECORD_MARKER_ATTRIBUTES")] BadRecordMarkerAttributes = 6,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_COMPLETE_WORKFLOW_EXECUTION_ATTRIBUTES")] BadCompleteWorkflowExecutionAttributes = 7,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_FAIL_WORKFLOW_EXECUTION_ATTRIBUTES")] BadFailWorkflowExecutionAttributes = 8,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_CANCEL_WORKFLOW_EXECUTION_ATTRIBUTES")] BadCancelWorkflowExecutionAttributes = 9,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_REQUEST_CANCEL_EXTERNAL_WORKFLOW_EXECUTION_ATTRIBUTES")] BadRequestCancelExternalWorkflowExecutionAttributes = 10,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_CONTINUE_AS_NEW_ATTRIBUTES")] BadContinueAsNewAttributes = 11,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_START_TIMER_DUPLICATE_ID")] StartTimerDuplicateId = 12,
    /// <summary>
    /// The worker wishes to fail the task and have the next one be generated on a normal, not sticky
    /// queue. Generally workers should prefer to use the explicit `ResetStickyTaskQueue` RPC call.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_RESET_STICKY_TASK_QUEUE")] ResetStickyTaskQueue = 13,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_WORKFLOW_WORKER_UNHANDLED_FAILURE")] WorkflowWorkerUnhandledFailure = 14,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_SIGNAL_WORKFLOW_EXECUTION_ATTRIBUTES")] BadSignalWorkflowExecutionAttributes = 15,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_START_CHILD_EXECUTION_ATTRIBUTES")] BadStartChildExecutionAttributes = 16,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_FORCE_CLOSE_COMMAND")] ForceCloseCommand = 17,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_FAILOVER_CLOSE_COMMAND")] FailoverCloseCommand = 18,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_SIGNAL_INPUT_SIZE")] BadSignalInputSize = 19,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_RESET_WORKFLOW")] ResetWorkflow = 20,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_BINARY")] BadBinary = 21,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_SCHEDULE_ACTIVITY_DUPLICATE_ID")] ScheduleActivityDuplicateId = 22,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_SEARCH_ATTRIBUTES")] BadSearchAttributes = 23,
    /// <summary>
    /// The worker encountered a mismatch while replaying history between what was expected, and
    /// what the workflow code actually did.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_NON_DETERMINISTIC_ERROR")] NonDeterministicError = 24,
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_MODIFY_WORKFLOW_PROPERTIES_ATTRIBUTES")] BadModifyWorkflowPropertiesAttributes = 25,
    /// <summary>
    /// We send the below error codes to users when their requests would violate a size constraint
    /// of their workflow. We do this to ensure that the state of their workflow does not become too
    /// large because that can cause severe performance degradation. You can modify the thresholds for
    /// each of these errors within your dynamic config.
    ///
    /// Spawning a new child workflow would cause this workflow to exceed its limit of pending child
    /// workflows.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_PENDING_CHILD_WORKFLOWS_LIMIT_EXCEEDED")] PendingChildWorkflowsLimitExceeded = 26,
    /// <summary>
    /// Starting a new activity would cause this workflow to exceed its limit of pending activities
    /// that we track.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_PENDING_ACTIVITIES_LIMIT_EXCEEDED")] PendingActivitiesLimitExceeded = 27,
    /// <summary>
    /// A workflow has a buffer of signals that have not yet reached their destination. We return this
    /// error when sending a new signal would exceed the capacity of this buffer.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_PENDING_SIGNALS_LIMIT_EXCEEDED")] PendingSignalsLimitExceeded = 28,
    /// <summary>
    /// Similarly, we have a buffer of pending requests to cancel other workflows. We return this error
    /// when our capacity for pending cancel requests is already reached.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_PENDING_REQUEST_CANCEL_LIMIT_EXCEEDED")] PendingRequestCancelLimitExceeded = 29,
    /// <summary>
    /// Workflow execution update message (update.Acceptance, update.Rejection, or update.Response)
    /// has wrong format, or missing required fields.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_UPDATE_WORKFLOW_EXECUTION_MESSAGE")] BadUpdateWorkflowExecutionMessage = 30,
    /// <summary>
    /// Similar to WORKFLOW_TASK_FAILED_CAUSE_UNHANDLED_COMMAND, but for updates.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_UNHANDLED_UPDATE")] UnhandledUpdate = 31,
    /// <summary>
    /// A workflow task completed with an invalid ScheduleNexusOperation command.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_SCHEDULE_NEXUS_OPERATION_ATTRIBUTES")] BadScheduleNexusOperationAttributes = 32,
    /// <summary>
    /// A workflow task completed requesting to schedule a Nexus Operation exceeding the server configured limit.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_PENDING_NEXUS_OPERATIONS_LIMIT_EXCEEDED")] PendingNexusOperationsLimitExceeded = 33,
    /// <summary>
    /// A workflow task completed with an invalid RequestCancelNexusOperation command.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_BAD_REQUEST_CANCEL_NEXUS_OPERATION_ATTRIBUTES")] BadRequestCancelNexusOperationAttributes = 34,
    /// <summary>
    /// A workflow task completed requesting a feature that's disabled on the server (either system wide or - typically -
    /// for the workflow's namespace).
    /// Check the workflow task failure message for more information.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_TASK_FAILED_CAUSE_FEATURE_DISABLED")] FeatureDisabled = 35,
  }

  public enum StartChildWorkflowExecutionFailedCause {
    [pbr::OriginalName("START_CHILD_WORKFLOW_EXECUTION_FAILED_CAUSE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("START_CHILD_WORKFLOW_EXECUTION_FAILED_CAUSE_WORKFLOW_ALREADY_EXISTS")] WorkflowAlreadyExists = 1,
    [pbr::OriginalName("START_CHILD_WORKFLOW_EXECUTION_FAILED_CAUSE_NAMESPACE_NOT_FOUND")] NamespaceNotFound = 2,
  }

  public enum CancelExternalWorkflowExecutionFailedCause {
    [pbr::OriginalName("CANCEL_EXTERNAL_WORKFLOW_EXECUTION_FAILED_CAUSE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("CANCEL_EXTERNAL_WORKFLOW_EXECUTION_FAILED_CAUSE_EXTERNAL_WORKFLOW_EXECUTION_NOT_FOUND")] ExternalWorkflowExecutionNotFound = 1,
    [pbr::OriginalName("CANCEL_EXTERNAL_WORKFLOW_EXECUTION_FAILED_CAUSE_NAMESPACE_NOT_FOUND")] NamespaceNotFound = 2,
  }

  public enum SignalExternalWorkflowExecutionFailedCause {
    [pbr::OriginalName("SIGNAL_EXTERNAL_WORKFLOW_EXECUTION_FAILED_CAUSE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("SIGNAL_EXTERNAL_WORKFLOW_EXECUTION_FAILED_CAUSE_EXTERNAL_WORKFLOW_EXECUTION_NOT_FOUND")] ExternalWorkflowExecutionNotFound = 1,
    [pbr::OriginalName("SIGNAL_EXTERNAL_WORKFLOW_EXECUTION_FAILED_CAUSE_NAMESPACE_NOT_FOUND")] NamespaceNotFound = 2,
    /// <summary>
    /// Signal count limit is per workflow and controlled by server dynamic config "history.maximumSignalsPerExecution"
    /// </summary>
    [pbr::OriginalName("SIGNAL_EXTERNAL_WORKFLOW_EXECUTION_FAILED_CAUSE_SIGNAL_COUNT_LIMIT_EXCEEDED")] SignalCountLimitExceeded = 3,
  }

  public enum ResourceExhaustedCause {
    [pbr::OriginalName("RESOURCE_EXHAUSTED_CAUSE_UNSPECIFIED")] Unspecified = 0,
    /// <summary>
    /// Caller exceeds request per second limit.
    /// </summary>
    [pbr::OriginalName("RESOURCE_EXHAUSTED_CAUSE_RPS_LIMIT")] RpsLimit = 1,
    /// <summary>
    /// Caller exceeds max concurrent request limit.
    /// </summary>
    [pbr::OriginalName("RESOURCE_EXHAUSTED_CAUSE_CONCURRENT_LIMIT")] ConcurrentLimit = 2,
    /// <summary>
    /// System overloaded.
    /// </summary>
    [pbr::OriginalName("RESOURCE_EXHAUSTED_CAUSE_SYSTEM_OVERLOADED")] SystemOverloaded = 3,
    /// <summary>
    /// Namespace exceeds persistence rate limit.
    /// </summary>
    [pbr::OriginalName("RESOURCE_EXHAUSTED_CAUSE_PERSISTENCE_LIMIT")] PersistenceLimit = 4,
    /// <summary>
    /// Workflow is busy
    /// </summary>
    [pbr::OriginalName("RESOURCE_EXHAUSTED_CAUSE_BUSY_WORKFLOW")] BusyWorkflow = 5,
    /// <summary>
    /// Caller exceeds action per second limit.
    /// </summary>
    [pbr::OriginalName("RESOURCE_EXHAUSTED_CAUSE_APS_LIMIT")] ApsLimit = 6,
    /// <summary>
    /// Persistence storage limit exceeded.
    /// </summary>
    [pbr::OriginalName("RESOURCE_EXHAUSTED_CAUSE_PERSISTENCE_STORAGE_LIMIT")] PersistenceStorageLimit = 7,
  }

  public enum ResourceExhaustedScope {
    [pbr::OriginalName("RESOURCE_EXHAUSTED_SCOPE_UNSPECIFIED")] Unspecified = 0,
    /// <summary>
    /// Exhausted resource is a system-level resource.
    /// </summary>
    [pbr::OriginalName("RESOURCE_EXHAUSTED_SCOPE_NAMESPACE")] Namespace = 1,
    /// <summary>
    /// Exhausted resource is a namespace-level resource.
    /// </summary>
    [pbr::OriginalName("RESOURCE_EXHAUSTED_SCOPE_SYSTEM")] System = 2,
  }

  #endregion

}

#endregion Designer generated code
