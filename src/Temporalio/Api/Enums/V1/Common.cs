// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/enums/v1/common.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.Enums.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/enums/v1/common.proto</summary>
  public static partial class CommonReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/enums/v1/common.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static CommonReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CiJ0ZW1wb3JhbC9hcGkvZW51bXMvdjEvY29tbW9uLnByb3RvEhV0ZW1wb3Jh",
            "bC5hcGkuZW51bXMudjEqXwoMRW5jb2RpbmdUeXBlEh0KGUVOQ09ESU5HX1RZ",
            "UEVfVU5TUEVDSUZJRUQQABIYChRFTkNPRElOR19UWVBFX1BST1RPMxABEhYK",
            "EkVOQ09ESU5HX1RZUEVfSlNPThACKpECChBJbmRleGVkVmFsdWVUeXBlEiIK",
            "HklOREVYRURfVkFMVUVfVFlQRV9VTlNQRUNJRklFRBAAEhsKF0lOREVYRURf",
            "VkFMVUVfVFlQRV9URVhUEAESHgoaSU5ERVhFRF9WQUxVRV9UWVBFX0tFWVdP",
            "UkQQAhIaChZJTkRFWEVEX1ZBTFVFX1RZUEVfSU5UEAMSHQoZSU5ERVhFRF9W",
            "QUxVRV9UWVBFX0RPVUJMRRAEEhsKF0lOREVYRURfVkFMVUVfVFlQRV9CT09M",
            "EAUSHwobSU5ERVhFRF9WQUxVRV9UWVBFX0RBVEVUSU1FEAYSIwofSU5ERVhF",
            "RF9WQUxVRV9UWVBFX0tFWVdPUkRfTElTVBAHKl4KCFNldmVyaXR5EhgKFFNF",
            "VkVSSVRZX1VOU1BFQ0lGSUVEEAASEQoNU0VWRVJJVFlfSElHSBABEhMKD1NF",
            "VkVSSVRZX01FRElVTRACEhAKDFNFVkVSSVRZX0xPVxADQoMBChhpby50ZW1w",
            "b3JhbC5hcGkuZW51bXMudjFCC0NvbW1vblByb3RvUAFaIWdvLnRlbXBvcmFs",
            "LmlvL2FwaS9lbnVtcy92MTtlbnVtc6oCF1RlbXBvcmFsaW8uQXBpLkVudW1z",
            "LlYx6gIaVGVtcG9yYWxpbzo6QXBpOjpFbnVtczo6VjFiBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(new[] {typeof(global::Temporalio.Api.Enums.V1.EncodingType), typeof(global::Temporalio.Api.Enums.V1.IndexedValueType), typeof(global::Temporalio.Api.Enums.V1.Severity), }, null, null));
    }
    #endregion

  }
  #region Enums
  public enum EncodingType {
    [pbr::OriginalName("ENCODING_TYPE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("ENCODING_TYPE_PROTO3")] Proto3 = 1,
    [pbr::OriginalName("ENCODING_TYPE_JSON")] Json = 2,
  }

  public enum IndexedValueType {
    [pbr::OriginalName("INDEXED_VALUE_TYPE_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("INDEXED_VALUE_TYPE_TEXT")] Text = 1,
    [pbr::OriginalName("INDEXED_VALUE_TYPE_KEYWORD")] Keyword = 2,
    [pbr::OriginalName("INDEXED_VALUE_TYPE_INT")] Int = 3,
    [pbr::OriginalName("INDEXED_VALUE_TYPE_DOUBLE")] Double = 4,
    [pbr::OriginalName("INDEXED_VALUE_TYPE_BOOL")] Bool = 5,
    [pbr::OriginalName("INDEXED_VALUE_TYPE_DATETIME")] Datetime = 6,
    [pbr::OriginalName("INDEXED_VALUE_TYPE_KEYWORD_LIST")] KeywordList = 7,
  }

  public enum Severity {
    [pbr::OriginalName("SEVERITY_UNSPECIFIED")] Unspecified = 0,
    [pbr::OriginalName("SEVERITY_HIGH")] High = 1,
    [pbr::OriginalName("SEVERITY_MEDIUM")] Medium = 2,
    [pbr::OriginalName("SEVERITY_LOW")] Low = 3,
  }

  #endregion

}

#endregion Designer generated code
