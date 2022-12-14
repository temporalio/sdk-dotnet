// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/enums/v1/batch_operation.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.Enums.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/enums/v1/batch_operation.proto</summary>
  public static partial class BatchOperationReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/enums/v1/batch_operation.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static BatchOperationReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Cit0ZW1wb3JhbC9hcGkvZW51bXMvdjEvYmF0Y2hfb3BlcmF0aW9uLnByb3Rv",
            "EhV0ZW1wb3JhbC5hcGkuZW51bXMudjEqoAEKEkJhdGNoT3BlcmF0aW9uVHlw",
            "ZRIkCiBCQVRDSF9PUEVSQVRJT05fVFlQRV9VTlNQRUNJRklFRBAAEiIKHkJB",
            "VENIX09QRVJBVElPTl9UWVBFX1RFUk1JTkFURRABEh8KG0JBVENIX09QRVJB",
            "VElPTl9UWVBFX0NBTkNFTBACEh8KG0JBVENIX09QRVJBVElPTl9UWVBFX1NJ",
            "R05BTBADKqYBChNCYXRjaE9wZXJhdGlvblN0YXRlEiUKIUJBVENIX09QRVJB",
            "VElPTl9TVEFURV9VTlNQRUNJRklFRBAAEiEKHUJBVENIX09QRVJBVElPTl9T",
            "VEFURV9SVU5OSU5HEAESIwofQkFUQ0hfT1BFUkFUSU9OX1NUQVRFX0NPTVBM",
            "RVRFRBACEiAKHEJBVENIX09QRVJBVElPTl9TVEFURV9GQUlMRUQQA0KJAQoY",
            "aW8udGVtcG9yYWwuYXBpLmVudW1zLnYxQhNCYXRjaE9wZXJhdGlvblByb3Rv",
            "UAFaIWdvLnRlbXBvcmFsLmlvL2FwaS9lbnVtcy92MTtlbnVtc6oCF1RlbXBv",
            "cmFsaW8uQXBpLkVudW1zLlYx6gIYVGVtcG9yYWw6OkFwaTo6RW51bXM6OlYx",
            "YgZwcm90bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::Temporalio.Api.Enums.V1.BatchOperationType), typeof(global::Temporalio.Api.Enums.V1.BatchOperationState), }, null, null));
    }
    #endregion

  }
  #region Enums
  public enum BatchOperationType {
    [pbr::OriginalName("BATCH_OPERATION_TYPE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("BATCH_OPERATION_TYPE_TERMINATE")] Terminate = 1,
    [pbr::OriginalName("BATCH_OPERATION_TYPE_CANCEL")] Cancel = 2,
    [pbr::OriginalName("BATCH_OPERATION_TYPE_SIGNAL")] Signal = 3,
  }

  public enum BatchOperationState {
    [pbr::OriginalName("BATCH_OPERATION_STATE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("BATCH_OPERATION_STATE_RUNNING")] Running = 1,
    [pbr::OriginalName("BATCH_OPERATION_STATE_COMPLETED")] Completed = 2,
    [pbr::OriginalName("BATCH_OPERATION_STATE_FAILED")] Failed = 3,
  }

  #endregion

}

#endregion Designer generated code
