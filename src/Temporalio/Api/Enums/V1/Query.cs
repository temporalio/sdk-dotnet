// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/enums/v1/query.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.Enums.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/enums/v1/query.proto</summary>
  public static partial class QueryReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/enums/v1/query.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static QueryReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CiF0ZW1wb3JhbC9hcGkvZW51bXMvdjEvcXVlcnkucHJvdG8SFXRlbXBvcmFs",
            "LmFwaS5lbnVtcy52MSpyCg9RdWVyeVJlc3VsdFR5cGUSIQodUVVFUllfUkVT",
            "VUxUX1RZUEVfVU5TUEVDSUZJRUQQABIeChpRVUVSWV9SRVNVTFRfVFlQRV9B",
            "TlNXRVJFRBABEhwKGFFVRVJZX1JFU1VMVF9UWVBFX0ZBSUxFRBACKrYBChRR",
            "dWVyeVJlamVjdENvbmRpdGlvbhImCiJRVUVSWV9SRUpFQ1RfQ09ORElUSU9O",
            "X1VOU1BFQ0lGSUVEEAASHwobUVVFUllfUkVKRUNUX0NPTkRJVElPTl9OT05F",
            "EAESIwofUVVFUllfUkVKRUNUX0NPTkRJVElPTl9OT1RfT1BFThACEjAKLFFV",
            "RVJZX1JFSkVDVF9DT05ESVRJT05fTk9UX0NPTVBMRVRFRF9DTEVBTkxZEANC",
            "gAEKGGlvLnRlbXBvcmFsLmFwaS5lbnVtcy52MUIKUXVlcnlQcm90b1ABWiFn",
            "by50ZW1wb3JhbC5pby9hcGkvZW51bXMvdjE7ZW51bXOqAhdUZW1wb3JhbGlv",
            "LkFwaS5FbnVtcy5WMeoCGFRlbXBvcmFsOjpBcGk6OkVudW1zOjpWMWIGcHJv",
            "dG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::Temporalio.Api.Enums.V1.QueryResultType), typeof(global::Temporalio.Api.Enums.V1.QueryRejectCondition), }, null, null));
    }
    #endregion

  }
  #region Enums
  public enum QueryResultType {
    [pbr::OriginalName("QUERY_RESULT_TYPE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("QUERY_RESULT_TYPE_ANSWERED")] Answered = 1,
    [pbr::OriginalName("QUERY_RESULT_TYPE_FAILED")] Failed = 2,
  }

  public enum QueryRejectCondition {
    [pbr::OriginalName("QUERY_REJECT_CONDITION_UNSPECIFIED")] Unspecified = 0,
    /// <summary>
    /// None indicates that query should not be rejected.
    /// </summary>
    [pbr::OriginalName("QUERY_REJECT_CONDITION_NONE")] None = 1,
    /// <summary>
    /// NotOpen indicates that query should be rejected if workflow is not open.
    /// </summary>
    [pbr::OriginalName("QUERY_REJECT_CONDITION_NOT_OPEN")] NotOpen = 2,
    /// <summary>
    /// NotCompletedCleanly indicates that query should be rejected if workflow did not complete cleanly.
    /// </summary>
    [pbr::OriginalName("QUERY_REJECT_CONDITION_NOT_COMPLETED_CLEANLY")] NotCompletedCleanly = 3,
  }

  #endregion

}

#endregion Designer generated code
