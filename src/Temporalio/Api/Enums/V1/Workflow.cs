// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/enums/v1/workflow.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.Enums.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/enums/v1/workflow.proto</summary>
  public static partial class WorkflowReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/enums/v1/workflow.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static WorkflowReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CiR0ZW1wb3JhbC9hcGkvZW51bXMvdjEvd29ya2Zsb3cucHJvdG8SFXRlbXBv",
            "cmFsLmFwaS5lbnVtcy52MSqLAgoVV29ya2Zsb3dJZFJldXNlUG9saWN5EigK",
            "JFdPUktGTE9XX0lEX1JFVVNFX1BPTElDWV9VTlNQRUNJRklFRBAAEiwKKFdP",
            "UktGTE9XX0lEX1JFVVNFX1BPTElDWV9BTExPV19EVVBMSUNBVEUQARI4CjRX",
            "T1JLRkxPV19JRF9SRVVTRV9QT0xJQ1lfQUxMT1dfRFVQTElDQVRFX0ZBSUxF",
            "RF9PTkxZEAISLQopV09SS0ZMT1dfSURfUkVVU0VfUE9MSUNZX1JFSkVDVF9E",
            "VVBMSUNBVEUQAxIxCi1XT1JLRkxPV19JRF9SRVVTRV9QT0xJQ1lfVEVSTUlO",
            "QVRFX0lGX1JVTk5JTkcQBCqkAQoRUGFyZW50Q2xvc2VQb2xpY3kSIwofUEFS",
            "RU5UX0NMT1NFX1BPTElDWV9VTlNQRUNJRklFRBAAEiEKHVBBUkVOVF9DTE9T",
            "RV9QT0xJQ1lfVEVSTUlOQVRFEAESHwobUEFSRU5UX0NMT1NFX1BPTElDWV9B",
            "QkFORE9OEAISJgoiUEFSRU5UX0NMT1NFX1BPTElDWV9SRVFVRVNUX0NBTkNF",
            "TBADKr0BChZDb250aW51ZUFzTmV3SW5pdGlhdG9yEikKJUNPTlRJTlVFX0FT",
            "X05FV19JTklUSUFUT1JfVU5TUEVDSUZJRUQQABImCiJDT05USU5VRV9BU19O",
            "RVdfSU5JVElBVE9SX1dPUktGTE9XEAESIwofQ09OVElOVUVfQVNfTkVXX0lO",
            "SVRJQVRPUl9SRVRSWRACEisKJ0NPTlRJTlVFX0FTX05FV19JTklUSUFUT1Jf",
            "Q1JPTl9TQ0hFRFVMRRADKuUCChdXb3JrZmxvd0V4ZWN1dGlvblN0YXR1cxIp",
            "CiVXT1JLRkxPV19FWEVDVVRJT05fU1RBVFVTX1VOU1BFQ0lGSUVEEAASJQoh",
            "V09SS0ZMT1dfRVhFQ1VUSU9OX1NUQVRVU19SVU5OSU5HEAESJwojV09SS0ZM",
            "T1dfRVhFQ1VUSU9OX1NUQVRVU19DT01QTEVURUQQAhIkCiBXT1JLRkxPV19F",
            "WEVDVVRJT05fU1RBVFVTX0ZBSUxFRBADEiYKIldPUktGTE9XX0VYRUNVVElP",
            "Tl9TVEFUVVNfQ0FOQ0VMRUQQBBIoCiRXT1JLRkxPV19FWEVDVVRJT05fU1RB",
            "VFVTX1RFUk1JTkFURUQQBRIuCipXT1JLRkxPV19FWEVDVVRJT05fU1RBVFVT",
            "X0NPTlRJTlVFRF9BU19ORVcQBhInCiNXT1JLRkxPV19FWEVDVVRJT05fU1RB",
            "VFVTX1RJTUVEX09VVBAHKrUBChRQZW5kaW5nQWN0aXZpdHlTdGF0ZRImCiJQ",
            "RU5ESU5HX0FDVElWSVRZX1NUQVRFX1VOU1BFQ0lGSUVEEAASJAogUEVORElO",
            "R19BQ1RJVklUWV9TVEFURV9TQ0hFRFVMRUQQARIiCh5QRU5ESU5HX0FDVElW",
            "SVRZX1NUQVRFX1NUQVJURUQQAhIrCidQRU5ESU5HX0FDVElWSVRZX1NUQVRF",
            "X0NBTkNFTF9SRVFVRVNURUQQAyqbAQoYUGVuZGluZ1dvcmtmbG93VGFza1N0",
            "YXRlEisKJ1BFTkRJTkdfV09SS0ZMT1dfVEFTS19TVEFURV9VTlNQRUNJRklF",
            "RBAAEikKJVBFTkRJTkdfV09SS0ZMT1dfVEFTS19TVEFURV9TQ0hFRFVMRUQQ",
            "ARInCiNQRU5ESU5HX1dPUktGTE9XX1RBU0tfU1RBVEVfU1RBUlRFRBACKpcB",
            "ChZIaXN0b3J5RXZlbnRGaWx0ZXJUeXBlEikKJUhJU1RPUllfRVZFTlRfRklM",
            "VEVSX1RZUEVfVU5TUEVDSUZJRUQQABInCiNISVNUT1JZX0VWRU5UX0ZJTFRF",
            "Ul9UWVBFX0FMTF9FVkVOVBABEikKJUhJU1RPUllfRVZFTlRfRklMVEVSX1RZ",
            "UEVfQ0xPU0VfRVZFTlQQAiqfAgoKUmV0cnlTdGF0ZRIbChdSRVRSWV9TVEFU",
            "RV9VTlNQRUNJRklFRBAAEhsKF1JFVFJZX1NUQVRFX0lOX1BST0dSRVNTEAES",
            "JQohUkVUUllfU1RBVEVfTk9OX1JFVFJZQUJMRV9GQUlMVVJFEAISFwoTUkVU",
            "UllfU1RBVEVfVElNRU9VVBADEigKJFJFVFJZX1NUQVRFX01BWElNVU1fQVRU",
            "RU1QVFNfUkVBQ0hFRBAEEiQKIFJFVFJZX1NUQVRFX1JFVFJZX1BPTElDWV9O",
            "T1RfU0VUEAUSJQohUkVUUllfU1RBVEVfSU5URVJOQUxfU0VSVkVSX0VSUk9S",
            "EAYSIAocUkVUUllfU1RBVEVfQ0FOQ0VMX1JFUVVFU1RFRBAHKrABCgtUaW1l",
            "b3V0VHlwZRIcChhUSU1FT1VUX1RZUEVfVU5TUEVDSUZJRUQQABIfChtUSU1F",
            "T1VUX1RZUEVfU1RBUlRfVE9fQ0xPU0UQARIiCh5USU1FT1VUX1RZUEVfU0NI",
            "RURVTEVfVE9fU1RBUlQQAhIiCh5USU1FT1VUX1RZUEVfU0NIRURVTEVfVE9f",
            "Q0xPU0UQAxIaChZUSU1FT1VUX1RZUEVfSEVBUlRCRUFUEARChQEKGGlvLnRl",
            "bXBvcmFsLmFwaS5lbnVtcy52MUINV29ya2Zsb3dQcm90b1ABWiFnby50ZW1w",
            "b3JhbC5pby9hcGkvZW51bXMvdjE7ZW51bXOqAhdUZW1wb3JhbGlvLkFwaS5F",
            "bnVtcy5WMeoCGlRlbXBvcmFsaW86OkFwaTo6RW51bXM6OlYxYgZwcm90bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::Temporalio.Api.Enums.V1.WorkflowIdReusePolicy), typeof(global::Temporalio.Api.Enums.V1.ParentClosePolicy), typeof(global::Temporalio.Api.Enums.V1.ContinueAsNewInitiator), typeof(global::Temporalio.Api.Enums.V1.WorkflowExecutionStatus), typeof(global::Temporalio.Api.Enums.V1.PendingActivityState), typeof(global::Temporalio.Api.Enums.V1.PendingWorkflowTaskState), typeof(global::Temporalio.Api.Enums.V1.HistoryEventFilterType), typeof(global::Temporalio.Api.Enums.V1.RetryState), typeof(global::Temporalio.Api.Enums.V1.TimeoutType), }, null, null));
    }
    #endregion

  }
  #region Enums
  /// <summary>
  /// Defines how new runs of a workflow with a particular ID may or may not be allowed. Note that
  /// it is *never* valid to have two actively running instances of the same workflow id.
  /// </summary>
  public enum WorkflowIdReusePolicy {
    [pbr::OriginalName("WORKFLOW_ID_REUSE_POLICY_UNSPECIFIED")] Unspecified = 0,
    /// <summary>
    /// Allow starting a workflow execution using the same workflow id.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_ID_REUSE_POLICY_ALLOW_DUPLICATE")] AllowDuplicate = 1,
    /// <summary>
    /// Allow starting a workflow execution using the same workflow id, only when the last
    /// execution's final state is one of [terminated, cancelled, timed out, failed].
    /// </summary>
    [pbr::OriginalName("WORKFLOW_ID_REUSE_POLICY_ALLOW_DUPLICATE_FAILED_ONLY")] AllowDuplicateFailedOnly = 2,
    /// <summary>
    /// Do not permit re-use of the workflow id for this workflow. Future start workflow requests
    /// could potentially change the policy, allowing re-use of the workflow id.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_ID_REUSE_POLICY_REJECT_DUPLICATE")] RejectDuplicate = 3,
    /// <summary>
    /// If a workflow is running using the same workflow ID, terminate it and start a new one.
    /// If no running workflow, then the behavior is the same as ALLOW_DUPLICATE
    /// </summary>
    [pbr::OriginalName("WORKFLOW_ID_REUSE_POLICY_TERMINATE_IF_RUNNING")] TerminateIfRunning = 4,
  }

  /// <summary>
  /// Defines how child workflows will react to their parent completing
  /// </summary>
  public enum ParentClosePolicy {
    [pbr::OriginalName("PARENT_CLOSE_POLICY_UNSPECIFIED")] Unspecified = 0,
    /// <summary>
    /// The child workflow will also terminate
    /// </summary>
    [pbr::OriginalName("PARENT_CLOSE_POLICY_TERMINATE")] Terminate = 1,
    /// <summary>
    /// The child workflow will do nothing
    /// </summary>
    [pbr::OriginalName("PARENT_CLOSE_POLICY_ABANDON")] Abandon = 2,
    /// <summary>
    /// Cancellation will be requested of the child workflow
    /// </summary>
    [pbr::OriginalName("PARENT_CLOSE_POLICY_REQUEST_CANCEL")] RequestCancel = 3,
  }

  public enum ContinueAsNewInitiator {
    [pbr::OriginalName("CONTINUE_AS_NEW_INITIATOR_UNSPECIFIED")] Unspecified = 0,
    /// <summary>
    /// The workflow itself requested to continue as new
    /// </summary>
    [pbr::OriginalName("CONTINUE_AS_NEW_INITIATOR_WORKFLOW")] Workflow = 1,
    /// <summary>
    /// The workflow continued as new because it is retrying
    /// </summary>
    [pbr::OriginalName("CONTINUE_AS_NEW_INITIATOR_RETRY")] Retry = 2,
    /// <summary>
    /// The workflow continued as new because cron has triggered a new execution
    /// </summary>
    [pbr::OriginalName("CONTINUE_AS_NEW_INITIATOR_CRON_SCHEDULE")] CronSchedule = 3,
  }

  /// <summary>
  /// (-- api-linter: core::0216::synonyms=disabled
  ///     aip.dev/not-precedent: There is WorkflowExecutionState already in another package. --)
  /// </summary>
  public enum WorkflowExecutionStatus {
    [pbr::OriginalName("WORKFLOW_EXECUTION_STATUS_UNSPECIFIED")] Unspecified = 0,
    /// <summary>
    /// Value 1 is hardcoded in SQL persistence.
    /// </summary>
    [pbr::OriginalName("WORKFLOW_EXECUTION_STATUS_RUNNING")] Running = 1,
    [pbr::OriginalName("WORKFLOW_EXECUTION_STATUS_COMPLETED")] Completed = 2,
    [pbr::OriginalName("WORKFLOW_EXECUTION_STATUS_FAILED")] Failed = 3,
    [pbr::OriginalName("WORKFLOW_EXECUTION_STATUS_CANCELED")] Canceled = 4,
    [pbr::OriginalName("WORKFLOW_EXECUTION_STATUS_TERMINATED")] Terminated = 5,
    [pbr::OriginalName("WORKFLOW_EXECUTION_STATUS_CONTINUED_AS_NEW")] ContinuedAsNew = 6,
    [pbr::OriginalName("WORKFLOW_EXECUTION_STATUS_TIMED_OUT")] TimedOut = 7,
  }

  public enum PendingActivityState {
    [pbr::OriginalName("PENDING_ACTIVITY_STATE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("PENDING_ACTIVITY_STATE_SCHEDULED")] Scheduled = 1,
    [pbr::OriginalName("PENDING_ACTIVITY_STATE_STARTED")] Started = 2,
    [pbr::OriginalName("PENDING_ACTIVITY_STATE_CANCEL_REQUESTED")] CancelRequested = 3,
  }

  public enum PendingWorkflowTaskState {
    [pbr::OriginalName("PENDING_WORKFLOW_TASK_STATE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("PENDING_WORKFLOW_TASK_STATE_SCHEDULED")] Scheduled = 1,
    [pbr::OriginalName("PENDING_WORKFLOW_TASK_STATE_STARTED")] Started = 2,
  }

  public enum HistoryEventFilterType {
    [pbr::OriginalName("HISTORY_EVENT_FILTER_TYPE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("HISTORY_EVENT_FILTER_TYPE_ALL_EVENT")] AllEvent = 1,
    [pbr::OriginalName("HISTORY_EVENT_FILTER_TYPE_CLOSE_EVENT")] CloseEvent = 2,
  }

  public enum RetryState {
    [pbr::OriginalName("RETRY_STATE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("RETRY_STATE_IN_PROGRESS")] InProgress = 1,
    [pbr::OriginalName("RETRY_STATE_NON_RETRYABLE_FAILURE")] NonRetryableFailure = 2,
    [pbr::OriginalName("RETRY_STATE_TIMEOUT")] Timeout = 3,
    [pbr::OriginalName("RETRY_STATE_MAXIMUM_ATTEMPTS_REACHED")] MaximumAttemptsReached = 4,
    [pbr::OriginalName("RETRY_STATE_RETRY_POLICY_NOT_SET")] RetryPolicyNotSet = 5,
    [pbr::OriginalName("RETRY_STATE_INTERNAL_SERVER_ERROR")] InternalServerError = 6,
    [pbr::OriginalName("RETRY_STATE_CANCEL_REQUESTED")] CancelRequested = 7,
  }

  public enum TimeoutType {
    [pbr::OriginalName("TIMEOUT_TYPE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("TIMEOUT_TYPE_START_TO_CLOSE")] StartToClose = 1,
    [pbr::OriginalName("TIMEOUT_TYPE_SCHEDULE_TO_START")] ScheduleToStart = 2,
    [pbr::OriginalName("TIMEOUT_TYPE_SCHEDULE_TO_CLOSE")] ScheduleToClose = 3,
    [pbr::OriginalName("TIMEOUT_TYPE_HEARTBEAT")] Heartbeat = 4,
  }

  #endregion

}

#endregion Designer generated code