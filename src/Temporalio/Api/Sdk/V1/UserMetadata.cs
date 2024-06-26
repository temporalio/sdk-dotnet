// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/sdk/v1/user_metadata.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.Sdk.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/sdk/v1/user_metadata.proto</summary>
  public static partial class UserMetadataReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/sdk/v1/user_metadata.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static UserMetadataReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Cid0ZW1wb3JhbC9hcGkvc2RrL3YxL3VzZXJfbWV0YWRhdGEucHJvdG8SE3Rl",
            "bXBvcmFsLmFwaS5zZGsudjEaJHRlbXBvcmFsL2FwaS9jb21tb24vdjEvbWVz",
            "c2FnZS5wcm90byJyCgxVc2VyTWV0YWRhdGESMAoHc3VtbWFyeRgBIAEoCzIf",
            "LnRlbXBvcmFsLmFwaS5jb21tb24udjEuUGF5bG9hZBIwCgdkZXRhaWxzGAIg",
            "ASgLMh8udGVtcG9yYWwuYXBpLmNvbW1vbi52MS5QYXlsb2FkQn8KFmlvLnRl",
            "bXBvcmFsLmFwaS5zZGsudjFCEVVzZXJNZXRhZGF0YVByb3RvUAFaHWdvLnRl",
            "bXBvcmFsLmlvL2FwaS9zZGsvdjE7c2RrqgIVVGVtcG9yYWxpby5BcGkuU2Rr",
            "LlYx6gIYVGVtcG9yYWxpbzo6QXBpOjpTZGs6OlYxYgZwcm90bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Temporalio.Api.Common.V1.MessageReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Temporalio.Api.Sdk.V1.UserMetadata), global::Temporalio.Api.Sdk.V1.UserMetadata.Parser, new[]{ "Summary", "Details" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  /// <summary>
  /// Information a user can set, often for use by user interfaces.
  /// </summary>
  public sealed partial class UserMetadata : pb::IMessage<UserMetadata>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<UserMetadata> _parser = new pb::MessageParser<UserMetadata>(() => new UserMetadata());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<UserMetadata> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Temporalio.Api.Sdk.V1.UserMetadataReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public UserMetadata() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public UserMetadata(UserMetadata other) : this() {
      summary_ = other.summary_ != null ? other.summary_.Clone() : null;
      details_ = other.details_ != null ? other.details_.Clone() : null;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public UserMetadata Clone() {
      return new UserMetadata(this);
    }

    /// <summary>Field number for the "summary" field.</summary>
    public const int SummaryFieldNumber = 1;
    private global::Temporalio.Api.Common.V1.Payload summary_;
    /// <summary>
    /// Short-form text that provides a summary. This payload should be a "json/plain"-encoded payload
    /// that is a single JSON string for use in user interfaces. User interface formatting may not
    /// apply to this text when used in "title" situations. The payload data section is limited to 400
    /// bytes by default.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Api.Common.V1.Payload Summary {
      get { return summary_; }
      set {
        summary_ = value;
      }
    }

    /// <summary>Field number for the "details" field.</summary>
    public const int DetailsFieldNumber = 2;
    private global::Temporalio.Api.Common.V1.Payload details_;
    /// <summary>
    /// Long-form text that provides details. This payload should be a "json/plain"-encoded payload
    /// that is a single JSON string for use in user interfaces. User interface formatting may apply to
    /// this text in common use. The payload data section is limited to 20000 bytes by default.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Api.Common.V1.Payload Details {
      get { return details_; }
      set {
        details_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as UserMetadata);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(UserMetadata other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (!object.Equals(Summary, other.Summary)) return false;
      if (!object.Equals(Details, other.Details)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (summary_ != null) hash ^= Summary.GetHashCode();
      if (details_ != null) hash ^= Details.GetHashCode();
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
      if (summary_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(Summary);
      }
      if (details_ != null) {
        output.WriteRawTag(18);
        output.WriteMessage(Details);
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
      if (summary_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(Summary);
      }
      if (details_ != null) {
        output.WriteRawTag(18);
        output.WriteMessage(Details);
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
      if (summary_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(Summary);
      }
      if (details_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(Details);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(UserMetadata other) {
      if (other == null) {
        return;
      }
      if (other.summary_ != null) {
        if (summary_ == null) {
          Summary = new global::Temporalio.Api.Common.V1.Payload();
        }
        Summary.MergeFrom(other.Summary);
      }
      if (other.details_ != null) {
        if (details_ == null) {
          Details = new global::Temporalio.Api.Common.V1.Payload();
        }
        Details.MergeFrom(other.Details);
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
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 10: {
            if (summary_ == null) {
              Summary = new global::Temporalio.Api.Common.V1.Payload();
            }
            input.ReadMessage(Summary);
            break;
          }
          case 18: {
            if (details_ == null) {
              Details = new global::Temporalio.Api.Common.V1.Payload();
            }
            input.ReadMessage(Details);
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
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 10: {
            if (summary_ == null) {
              Summary = new global::Temporalio.Api.Common.V1.Payload();
            }
            input.ReadMessage(Summary);
            break;
          }
          case 18: {
            if (details_ == null) {
              Details = new global::Temporalio.Api.Common.V1.Payload();
            }
            input.ReadMessage(Details);
            break;
          }
        }
      }
    }
    #endif

  }

  #endregion

}

#endregion Designer generated code
