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
namespace Temporal.Api.Enums.V1 {

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
            "ZW1wb3JhbC5hcGkuZW51bXMudjEqkAwKF1dvcmtmbG93VGFza0ZhaWxlZENh",
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
            "KvMBCiZTdGFydENoaWxkV29ya2Zsb3dFeGVjdXRpb25GYWlsZWRDYXVzZRI7",
            "CjdTVEFSVF9DSElMRF9XT1JLRkxPV19FWEVDVVRJT05fRkFJTEVEX0NBVVNF",
            "X1VOU1BFQ0lGSUVEEAASRwpDU1RBUlRfQ0hJTERfV09SS0ZMT1dfRVhFQ1VU",
            "SU9OX0ZBSUxFRF9DQVVTRV9XT1JLRkxPV19BTFJFQURZX0VYSVNUUxABEkMK",
            "P1NUQVJUX0NISUxEX1dPUktGTE9XX0VYRUNVVElPTl9GQUlMRURfQ0FVU0Vf",
            "TkFNRVNQQUNFX05PVF9GT1VORBACKpECCipDYW5jZWxFeHRlcm5hbFdvcmtm",
            "bG93RXhlY3V0aW9uRmFpbGVkQ2F1c2USPwo7Q0FOQ0VMX0VYVEVSTkFMX1dP",
            "UktGTE9XX0VYRUNVVElPTl9GQUlMRURfQ0FVU0VfVU5TUEVDSUZJRUQQABJZ",
            "ClVDQU5DRUxfRVhURVJOQUxfV09SS0ZMT1dfRVhFQ1VUSU9OX0ZBSUxFRF9D",
            "QVVTRV9FWFRFUk5BTF9XT1JLRkxPV19FWEVDVVRJT05fTk9UX0ZPVU5EEAES",
            "RwpDQ0FOQ0VMX0VYVEVSTkFMX1dPUktGTE9XX0VYRUNVVElPTl9GQUlMRURf",
            "Q0FVU0VfTkFNRVNQQUNFX05PVF9GT1VORBACKpECCipTaWduYWxFeHRlcm5h",
            "bFdvcmtmbG93RXhlY3V0aW9uRmFpbGVkQ2F1c2USPwo7U0lHTkFMX0VYVEVS",
            "TkFMX1dPUktGTE9XX0VYRUNVVElPTl9GQUlMRURfQ0FVU0VfVU5TUEVDSUZJ",
            "RUQQABJZClVTSUdOQUxfRVhURVJOQUxfV09SS0ZMT1dfRVhFQ1VUSU9OX0ZB",
            "SUxFRF9DQVVTRV9FWFRFUk5BTF9XT1JLRkxPV19FWEVDVVRJT05fTk9UX0ZP",
            "VU5EEAESRwpDU0lHTkFMX0VYVEVSTkFMX1dPUktGTE9XX0VYRUNVVElPTl9G",
            "QUlMRURfQ0FVU0VfTkFNRVNQQUNFX05PVF9GT1VORBACKskBChZSZXNvdXJj",
            "ZUV4aGF1c3RlZENhdXNlEigKJFJFU09VUkNFX0VYSEFVU1RFRF9DQVVTRV9V",
            "TlNQRUNJRklFRBAAEiYKIlJFU09VUkNFX0VYSEFVU1RFRF9DQVVTRV9SUFNf",
            "TElNSVQQARItCilSRVNPVVJDRV9FWEhBVVNURURfQ0FVU0VfQ09OQ1VSUkVO",
            "VF9MSU1JVBACEi4KKlJFU09VUkNFX0VYSEFVU1RFRF9DQVVTRV9TWVNURU1f",
            "T1ZFUkxPQURFRBADQoQBChhpby50ZW1wb3JhbC5hcGkuZW51bXMudjFCEEZh",
            "aWxlZENhdXNlUHJvdG9QAVohZ28udGVtcG9yYWwuaW8vYXBpL2VudW1zL3Yx",
            "O2VudW1zqgIVVGVtcG9yYWwuQXBpLkVudW1zLlYx6gIYVGVtcG9yYWw6OkFw",
            "aTo6RW51bXM6OlYxYgZwcm90bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::Temporal.Api.Enums.V1.WorkflowTaskFailedCause), typeof(global::Temporal.Api.Enums.V1.StartChildWorkflowExecutionFailedCause), typeof(global::Temporal.Api.Enums.V1.CancelExternalWorkflowExecutionFailedCause), typeof(global::Temporal.Api.Enums.V1.SignalExternalWorkflowExecutionFailedCause), typeof(global::Temporal.Api.Enums.V1.ResourceExhaustedCause), }, null, null));
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
  }

  #endregion

}

#endregion Designer generated code
