// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: grpc_health.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Bridge.Api.Grpc.Health.V1 {

  /// <summary>Holder for reflection information generated from grpc_health.proto</summary>
  internal static partial class GrpcHealthReflection {

    #region Descriptor
    /// <summary>File descriptor for grpc_health.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static GrpcHealthReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChFncnBjX2hlYWx0aC5wcm90bxIWY29yZXNkay5ncnBjLmhlYWx0aC52MSIl",
            "ChJIZWFsdGhDaGVja1JlcXVlc3QSDwoHc2VydmljZRgBIAEoCSKxAQoTSGVh",
            "bHRoQ2hlY2tSZXNwb25zZRJJCgZzdGF0dXMYASABKA4yOS5jb3Jlc2RrLmdy",
            "cGMuaGVhbHRoLnYxLkhlYWx0aENoZWNrUmVzcG9uc2UuU2VydmluZ1N0YXR1",
            "cyJPCg1TZXJ2aW5nU3RhdHVzEgsKB1VOS05PV04QABILCgdTRVJWSU5HEAES",
            "DwoLTk9UX1NFUlZJTkcQAhITCg9TRVJWSUNFX1VOS05PV04QAzLOAQoGSGVh",
            "bHRoEmAKBUNoZWNrEiouY29yZXNkay5ncnBjLmhlYWx0aC52MS5IZWFsdGhD",
            "aGVja1JlcXVlc3QaKy5jb3Jlc2RrLmdycGMuaGVhbHRoLnYxLkhlYWx0aENo",
            "ZWNrUmVzcG9uc2USYgoFV2F0Y2gSKi5jb3Jlc2RrLmdycGMuaGVhbHRoLnYx",
            "LkhlYWx0aENoZWNrUmVxdWVzdBorLmNvcmVzZGsuZ3JwYy5oZWFsdGgudjEu",
            "SGVhbHRoQ2hlY2tSZXNwb25zZTABYgZwcm90bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckRequest), global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckRequest.Parser, new[]{ "Service" }, null, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse), global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse.Parser, new[]{ "Status" }, null, new[]{ typeof(global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus) }, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  [global::System.Diagnostics.DebuggerDisplayAttribute("{ToString(),nq}")]
  internal sealed partial class HealthCheckRequest : pb::IMessage<HealthCheckRequest>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<HealthCheckRequest> _parser = new pb::MessageParser<HealthCheckRequest>(() => new HealthCheckRequest());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<HealthCheckRequest> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Temporalio.Bridge.Api.Grpc.Health.V1.GrpcHealthReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HealthCheckRequest() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HealthCheckRequest(HealthCheckRequest other) : this() {
      service_ = other.service_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HealthCheckRequest Clone() {
      return new HealthCheckRequest(this);
    }

    /// <summary>Field number for the "service" field.</summary>
    public const int ServiceFieldNumber = 1;
    private string service_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string Service {
      get { return service_; }
      set {
        service_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as HealthCheckRequest);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(HealthCheckRequest other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Service != other.Service) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (Service.Length != 0) hash ^= Service.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (Service.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(Service);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (Service.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(Service);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      if (Service.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(Service);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(HealthCheckRequest other) {
      if (other == null) {
        return;
      }
      if (other.Service.Length != 0) {
        Service = other.Service;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
      if ((tag & 7) == 4) {
        // Abort on any end group tag.
        return;
      }
      switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 10: {
            Service = input.ReadString();
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
      if ((tag & 7) == 4) {
        // Abort on any end group tag.
        return;
      }
      switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 10: {
            Service = input.ReadString();
            break;
          }
        }
      }
    }
    #endif

  }

  [global::System.Diagnostics.DebuggerDisplayAttribute("{ToString(),nq}")]
  internal sealed partial class HealthCheckResponse : pb::IMessage<HealthCheckResponse>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<HealthCheckResponse> _parser = new pb::MessageParser<HealthCheckResponse>(() => new HealthCheckResponse());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<HealthCheckResponse> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Temporalio.Bridge.Api.Grpc.Health.V1.GrpcHealthReflection.Descriptor.MessageTypes[1]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HealthCheckResponse() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HealthCheckResponse(HealthCheckResponse other) : this() {
      status_ = other.status_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public HealthCheckResponse Clone() {
      return new HealthCheckResponse(this);
    }

    /// <summary>Field number for the "status" field.</summary>
    public const int StatusFieldNumber = 1;
    private global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus status_ = global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus.Unknown;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus Status {
      get { return status_; }
      set {
        status_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as HealthCheckResponse);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(HealthCheckResponse other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Status != other.Status) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (Status != global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus.Unknown) hash ^= Status.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (Status != global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus.Unknown) {
        output.WriteRawTag(8);
        output.WriteEnum((int) Status);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (Status != global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus.Unknown) {
        output.WriteRawTag(8);
        output.WriteEnum((int) Status);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      if (Status != global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus.Unknown) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) Status);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(HealthCheckResponse other) {
      if (other == null) {
        return;
      }
      if (other.Status != global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus.Unknown) {
        Status = other.Status;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
      if ((tag & 7) == 4) {
        // Abort on any end group tag.
        return;
      }
      switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 8: {
            Status = (global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus) input.ReadEnum();
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
      if ((tag & 7) == 4) {
        // Abort on any end group tag.
        return;
      }
      switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 8: {
            Status = (global::Temporalio.Bridge.Api.Grpc.Health.V1.HealthCheckResponse.Types.ServingStatus) input.ReadEnum();
            break;
          }
        }
      }
    }
    #endif

    #region Nested types
    /// <summary>Container for nested types declared in the HealthCheckResponse message type.</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static partial class Types {
      internal enum ServingStatus {
        [pbr::OriginalName("UNKNOWN")] Unknown = 0,
        [pbr::OriginalName("SERVING")] Serving = 1,
        [pbr::OriginalName("NOT_SERVING")] NotServing = 2,
        /// <summary>
        /// Used only by the Watch method.
        /// </summary>
        [pbr::OriginalName("SERVICE_UNKNOWN")] ServiceUnknown = 3,
      }

    }
    #endregion

  }

  #endregion

}

#endregion Designer generated code
