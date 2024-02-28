// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/enums/v1/update.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.Enums.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/enums/v1/update.proto</summary>
  public static partial class UpdateReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/enums/v1/update.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static UpdateReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CiJ0ZW1wb3JhbC9hcGkvZW51bXMvdjEvdXBkYXRlLnByb3RvEhV0ZW1wb3Jh",
            "bC5hcGkuZW51bXMudjEqiwIKJVVwZGF0ZVdvcmtmbG93RXhlY3V0aW9uTGlm",
            "ZWN5Y2xlU3RhZ2USOQo1VVBEQVRFX1dPUktGTE9XX0VYRUNVVElPTl9MSUZF",
            "Q1lDTEVfU1RBR0VfVU5TUEVDSUZJRUQQABI2CjJVUERBVEVfV09SS0ZMT1df",
            "RVhFQ1VUSU9OX0xJRkVDWUNMRV9TVEFHRV9BRE1JVFRFRBABEjYKMlVQREFU",
            "RV9XT1JLRkxPV19FWEVDVVRJT05fTElGRUNZQ0xFX1NUQUdFX0FDQ0VQVEVE",
            "EAISNwozVVBEQVRFX1dPUktGTE9XX0VYRUNVVElPTl9MSUZFQ1lDTEVfU1RB",
            "R0VfQ09NUExFVEVEEAMqdgoaVXBkYXRlUmVxdWVzdGVkRXZlbnRPcmlnaW4S",
            "LQopVVBEQVRFX1JFUVVFU1RFRF9FVkVOVF9PUklHSU5fVU5TUEVDSUZJRUQQ",
            "ABIpCiVVUERBVEVfUkVRVUVTVEVEX0VWRU5UX09SSUdJTl9SRUFQUExZEAFC",
            "gwEKGGlvLnRlbXBvcmFsLmFwaS5lbnVtcy52MUILVXBkYXRlUHJvdG9QAVoh",
            "Z28udGVtcG9yYWwuaW8vYXBpL2VudW1zL3YxO2VudW1zqgIXVGVtcG9yYWxp",
            "by5BcGkuRW51bXMuVjHqAhpUZW1wb3JhbGlvOjpBcGk6OkVudW1zOjpWMWIG",
            "cHJvdG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::Temporalio.Api.Enums.V1.UpdateWorkflowExecutionLifecycleStage), typeof(global::Temporalio.Api.Enums.V1.UpdateRequestedEventOrigin), }, null, null));
    }
    #endregion

  }
  #region Enums
  /// <summary>
  /// UpdateWorkflowExecutionLifecycleStage is specified by clients invoking
  /// workflow execution updates and used to indicate to the server how long the
  /// client wishes to wait for a return value from the RPC. If any value other
  /// than UPDATE_WORKFLOW_EXECUTION_LIFECYCLE_STAGE_COMPLETED is sent by the
  /// client then the RPC will complete before the update is finished and will
  /// return a handle to the running update so that it can later be polled for
  /// completion.
  /// </summary>
  public enum UpdateWorkflowExecutionLifecycleStage {
    /// <summary>
    /// An unspecified vale for this enum.
    /// </summary>
    [pbr::OriginalName("UPDATE_WORKFLOW_EXECUTION_LIFECYCLE_STAGE_UNSPECIFIED")] Unspecified = 0,
    /// <summary>
    /// The gRPC call will not return until the update request has been admitted
    /// by the server - it may be the case that due to a considerations like load
    /// or resource limits that an update is made to wait before the server will
    /// indicate that it has been received and will be processed. This value
    /// does not wait for any sort of acknowledgement from a worker.
    /// </summary>
    [pbr::OriginalName("UPDATE_WORKFLOW_EXECUTION_LIFECYCLE_STAGE_ADMITTED")] Admitted = 1,
    /// <summary>
    /// The gRPC call will not return until the update has passed validation on
    /// a worker.
    /// </summary>
    [pbr::OriginalName("UPDATE_WORKFLOW_EXECUTION_LIFECYCLE_STAGE_ACCEPTED")] Accepted = 2,
    /// <summary>
    /// The gRPC call will not return until the update has executed to completion
    /// on a worker and has either been rejected or returned a value or an error.
    /// </summary>
    [pbr::OriginalName("UPDATE_WORKFLOW_EXECUTION_LIFECYCLE_STAGE_COMPLETED")] Completed = 3,
  }

  /// <summary>
  /// UpdateRequestedEventOrigin records why an
  /// WorkflowExecutionUpdateRequestedEvent was written to history. Note that not
  /// all update requests result in a WorkflowExecutionUpdateRequestedEvent.
  /// </summary>
  public enum UpdateRequestedEventOrigin {
    [pbr::OriginalName("UPDATE_REQUESTED_EVENT_ORIGIN_UNSPECIFIED")] Unspecified = 0,
    /// <summary>
    /// The UpdateRequested event was created when reapplying events during reset
    /// or replication. I.e. an accepted update on one branch of workflow history
    /// was converted into a requested update on a different branch.
    /// </summary>
    [pbr::OriginalName("UPDATE_REQUESTED_EVENT_ORIGIN_REAPPLY")] Reapply = 1,
  }

  #endregion

}

#endregion Designer generated code
