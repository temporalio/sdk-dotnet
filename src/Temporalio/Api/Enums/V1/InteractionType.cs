// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/enums/v1/interaction_type.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.Enums.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/enums/v1/interaction_type.proto</summary>
  public static partial class InteractionTypeReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/enums/v1/interaction_type.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static InteractionTypeReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Cix0ZW1wb3JhbC9hcGkvZW51bXMvdjEvaW50ZXJhY3Rpb25fdHlwZS5wcm90",
            "bxIVdGVtcG9yYWwuYXBpLmVudW1zLnYxKqQBCg9JbnRlcmFjdGlvblR5cGUS",
            "IAocSU5URVJBQ1RJT05fVFlQRV9VTlNQRUNJRklFRBAAEiMKH0lOVEVSQUNU",
            "SU9OX1RZUEVfV09SS0ZMT1dfUVVFUlkQARIkCiBJTlRFUkFDVElPTl9UWVBF",
            "X1dPUktGTE9XX1VQREFURRACEiQKIElOVEVSQUNUSU9OX1RZUEVfV09SS0ZM",
            "T1dfU0lHTkFMEANCjAEKGGlvLnRlbXBvcmFsLmFwaS5lbnVtcy52MUIUSW50",
            "ZXJhY3Rpb25UeXBlUHJvdG9QAVohZ28udGVtcG9yYWwuaW8vYXBpL2VudW1z",
            "L3YxO2VudW1zqgIXVGVtcG9yYWxpby5BcGkuRW51bXMuVjHqAhpUZW1wb3Jh",
            "bGlvOjpBcGk6OkVudW1zOjpWMWIGcHJvdG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::Temporalio.Api.Enums.V1.InteractionType), }, null, null));
    }
    #endregion

  }
  #region Enums
  public enum InteractionType {
    [pbr::OriginalName("INTERACTION_TYPE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("INTERACTION_TYPE_WORKFLOW_QUERY")] WorkflowQuery = 1,
    [pbr::OriginalName("INTERACTION_TYPE_WORKFLOW_UPDATE")] WorkflowUpdate = 2,
    [pbr::OriginalName("INTERACTION_TYPE_WORKFLOW_SIGNAL")] WorkflowSignal = 3,
  }

  #endregion

}

#endregion Designer generated code
