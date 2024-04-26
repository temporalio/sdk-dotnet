// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/sdk/v1/workflow_metadata.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.Sdk.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/sdk/v1/workflow_metadata.proto</summary>
  public static partial class WorkflowMetadataReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/sdk/v1/workflow_metadata.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static WorkflowMetadataReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Cit0ZW1wb3JhbC9hcGkvc2RrL3YxL3dvcmtmbG93X21ldGFkYXRhLnByb3Rv",
            "EhN0ZW1wb3JhbC5hcGkuc2RrLnYxIk8KEFdvcmtmbG93TWV0YWRhdGESOwoK",
            "ZGVmaW5pdGlvbhgBIAEoCzInLnRlbXBvcmFsLmFwaS5zZGsudjEuV29ya2Zs",
            "b3dEZWZpbml0aW9uIqYCChJXb3JrZmxvd0RlZmluaXRpb24SDAoEdHlwZRgB",
            "IAEoCRITCgtkZXNjcmlwdGlvbhgCIAEoCRJNChFxdWVyeV9kZWZpbml0aW9u",
            "cxgDIAMoCzIyLnRlbXBvcmFsLmFwaS5zZGsudjEuV29ya2Zsb3dJbnRlcmFj",
            "dGlvbkRlZmluaXRpb24STgoSc2lnbmFsX2RlZmluaXRpb25zGAQgAygLMjIu",
            "dGVtcG9yYWwuYXBpLnNkay52MS5Xb3JrZmxvd0ludGVyYWN0aW9uRGVmaW5p",
            "dGlvbhJOChJ1cGRhdGVfZGVmaW5pdGlvbnMYBSADKAsyMi50ZW1wb3JhbC5h",
            "cGkuc2RrLnYxLldvcmtmbG93SW50ZXJhY3Rpb25EZWZpbml0aW9uIkIKHVdv",
            "cmtmbG93SW50ZXJhY3Rpb25EZWZpbml0aW9uEgwKBG5hbWUYASABKAkSEwoL",
            "ZGVzY3JpcHRpb24YAiABKAlCgwEKFmlvLnRlbXBvcmFsLmFwaS5zZGsudjFC",
            "FVdvcmtmbG93TWV0YWRhdGFQcm90b1ABWh1nby50ZW1wb3JhbC5pby9hcGkv",
            "c2RrL3YxO3Nka6oCFVRlbXBvcmFsaW8uQXBpLlNkay5WMeoCGFRlbXBvcmFs",
            "aW86OkFwaTo6U2RrOjpWMWIGcHJvdG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Temporalio.Api.Sdk.V1.WorkflowMetadata), global::Temporalio.Api.Sdk.V1.WorkflowMetadata.Parser, new[]{ "Definition" }, null, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::Temporalio.Api.Sdk.V1.WorkflowDefinition), global::Temporalio.Api.Sdk.V1.WorkflowDefinition.Parser, new[]{ "Type", "Description", "QueryDefinitions", "SignalDefinitions", "UpdateDefinitions" }, null, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition), global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition.Parser, new[]{ "Name", "Description" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  /// <summary>
  /// The name of the query to retrieve this information is `__temporal_getWorkflowMetadata`.
  /// </summary>
  public sealed partial class WorkflowMetadata : pb::IMessage<WorkflowMetadata>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<WorkflowMetadata> _parser = new pb::MessageParser<WorkflowMetadata>(() => new WorkflowMetadata());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<WorkflowMetadata> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Temporalio.Api.Sdk.V1.WorkflowMetadataReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowMetadata() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowMetadata(WorkflowMetadata other) : this() {
      definition_ = other.definition_ != null ? other.definition_.Clone() : null;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowMetadata Clone() {
      return new WorkflowMetadata(this);
    }

    /// <summary>Field number for the "definition" field.</summary>
    public const int DefinitionFieldNumber = 1;
    private global::Temporalio.Api.Sdk.V1.WorkflowDefinition definition_;
    /// <summary>
    /// Metadata provided at declaration or creation time.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Api.Sdk.V1.WorkflowDefinition Definition {
      get { return definition_; }
      set {
        definition_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as WorkflowMetadata);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(WorkflowMetadata other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (!object.Equals(Definition, other.Definition)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (definition_ != null) hash ^= Definition.GetHashCode();
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
      if (definition_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(Definition);
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
      if (definition_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(Definition);
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
      if (definition_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(Definition);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(WorkflowMetadata other) {
      if (other == null) {
        return;
      }
      if (other.definition_ != null) {
        if (definition_ == null) {
          Definition = new global::Temporalio.Api.Sdk.V1.WorkflowDefinition();
        }
        Definition.MergeFrom(other.Definition);
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
            if (definition_ == null) {
              Definition = new global::Temporalio.Api.Sdk.V1.WorkflowDefinition();
            }
            input.ReadMessage(Definition);
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
            if (definition_ == null) {
              Definition = new global::Temporalio.Api.Sdk.V1.WorkflowDefinition();
            }
            input.ReadMessage(Definition);
            break;
          }
        }
      }
    }
    #endif

  }

  /// <summary>
  /// (-- api-linter: core::0203::optional=disabled --)
  /// </summary>
  public sealed partial class WorkflowDefinition : pb::IMessage<WorkflowDefinition>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<WorkflowDefinition> _parser = new pb::MessageParser<WorkflowDefinition>(() => new WorkflowDefinition());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<WorkflowDefinition> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Temporalio.Api.Sdk.V1.WorkflowMetadataReflection.Descriptor.MessageTypes[1]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowDefinition() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowDefinition(WorkflowDefinition other) : this() {
      type_ = other.type_;
      description_ = other.description_;
      queryDefinitions_ = other.queryDefinitions_.Clone();
      signalDefinitions_ = other.signalDefinitions_.Clone();
      updateDefinitions_ = other.updateDefinitions_.Clone();
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowDefinition Clone() {
      return new WorkflowDefinition(this);
    }

    /// <summary>Field number for the "type" field.</summary>
    public const int TypeFieldNumber = 1;
    private string type_ = "";
    /// <summary>
    /// A name scoped by the task queue that maps to this workflow definition.
    /// If missing, this workflow is a dynamic workflow.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string Type {
      get { return type_; }
      set {
        type_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "description" field.</summary>
    public const int DescriptionFieldNumber = 2;
    private string description_ = "";
    /// <summary>
    /// An optional workflow description provided by the application.
    /// By convention, external tools may interpret its first part,
    /// i.e., ending with a line break, as a summary of the description.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string Description {
      get { return description_; }
      set {
        description_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "query_definitions" field.</summary>
    public const int QueryDefinitionsFieldNumber = 3;
    private static readonly pb::FieldCodec<global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition> _repeated_queryDefinitions_codec
        = pb::FieldCodec.ForMessage(26, global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition.Parser);
    private readonly pbc::RepeatedField<global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition> queryDefinitions_ = new pbc::RepeatedField<global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public pbc::RepeatedField<global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition> QueryDefinitions {
      get { return queryDefinitions_; }
    }

    /// <summary>Field number for the "signal_definitions" field.</summary>
    public const int SignalDefinitionsFieldNumber = 4;
    private static readonly pb::FieldCodec<global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition> _repeated_signalDefinitions_codec
        = pb::FieldCodec.ForMessage(34, global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition.Parser);
    private readonly pbc::RepeatedField<global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition> signalDefinitions_ = new pbc::RepeatedField<global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public pbc::RepeatedField<global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition> SignalDefinitions {
      get { return signalDefinitions_; }
    }

    /// <summary>Field number for the "update_definitions" field.</summary>
    public const int UpdateDefinitionsFieldNumber = 5;
    private static readonly pb::FieldCodec<global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition> _repeated_updateDefinitions_codec
        = pb::FieldCodec.ForMessage(42, global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition.Parser);
    private readonly pbc::RepeatedField<global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition> updateDefinitions_ = new pbc::RepeatedField<global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public pbc::RepeatedField<global::Temporalio.Api.Sdk.V1.WorkflowInteractionDefinition> UpdateDefinitions {
      get { return updateDefinitions_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as WorkflowDefinition);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(WorkflowDefinition other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Type != other.Type) return false;
      if (Description != other.Description) return false;
      if(!queryDefinitions_.Equals(other.queryDefinitions_)) return false;
      if(!signalDefinitions_.Equals(other.signalDefinitions_)) return false;
      if(!updateDefinitions_.Equals(other.updateDefinitions_)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (Type.Length != 0) hash ^= Type.GetHashCode();
      if (Description.Length != 0) hash ^= Description.GetHashCode();
      hash ^= queryDefinitions_.GetHashCode();
      hash ^= signalDefinitions_.GetHashCode();
      hash ^= updateDefinitions_.GetHashCode();
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
      if (Type.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(Type);
      }
      if (Description.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(Description);
      }
      queryDefinitions_.WriteTo(output, _repeated_queryDefinitions_codec);
      signalDefinitions_.WriteTo(output, _repeated_signalDefinitions_codec);
      updateDefinitions_.WriteTo(output, _repeated_updateDefinitions_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (Type.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(Type);
      }
      if (Description.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(Description);
      }
      queryDefinitions_.WriteTo(ref output, _repeated_queryDefinitions_codec);
      signalDefinitions_.WriteTo(ref output, _repeated_signalDefinitions_codec);
      updateDefinitions_.WriteTo(ref output, _repeated_updateDefinitions_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      if (Type.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(Type);
      }
      if (Description.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(Description);
      }
      size += queryDefinitions_.CalculateSize(_repeated_queryDefinitions_codec);
      size += signalDefinitions_.CalculateSize(_repeated_signalDefinitions_codec);
      size += updateDefinitions_.CalculateSize(_repeated_updateDefinitions_codec);
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(WorkflowDefinition other) {
      if (other == null) {
        return;
      }
      if (other.Type.Length != 0) {
        Type = other.Type;
      }
      if (other.Description.Length != 0) {
        Description = other.Description;
      }
      queryDefinitions_.Add(other.queryDefinitions_);
      signalDefinitions_.Add(other.signalDefinitions_);
      updateDefinitions_.Add(other.updateDefinitions_);
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
            Type = input.ReadString();
            break;
          }
          case 18: {
            Description = input.ReadString();
            break;
          }
          case 26: {
            queryDefinitions_.AddEntriesFrom(input, _repeated_queryDefinitions_codec);
            break;
          }
          case 34: {
            signalDefinitions_.AddEntriesFrom(input, _repeated_signalDefinitions_codec);
            break;
          }
          case 42: {
            updateDefinitions_.AddEntriesFrom(input, _repeated_updateDefinitions_codec);
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
            Type = input.ReadString();
            break;
          }
          case 18: {
            Description = input.ReadString();
            break;
          }
          case 26: {
            queryDefinitions_.AddEntriesFrom(ref input, _repeated_queryDefinitions_codec);
            break;
          }
          case 34: {
            signalDefinitions_.AddEntriesFrom(ref input, _repeated_signalDefinitions_codec);
            break;
          }
          case 42: {
            updateDefinitions_.AddEntriesFrom(ref input, _repeated_updateDefinitions_codec);
            break;
          }
        }
      }
    }
    #endif

  }

  /// <summary>
  /// (-- api-linter: core::0123::resource-annotation=disabled
  ///     aip.dev/not-precedent: The `name` field is optional. --)
  /// (-- api-linter: core::0203::optional=disabled --)
  /// </summary>
  public sealed partial class WorkflowInteractionDefinition : pb::IMessage<WorkflowInteractionDefinition>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<WorkflowInteractionDefinition> _parser = new pb::MessageParser<WorkflowInteractionDefinition>(() => new WorkflowInteractionDefinition());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<WorkflowInteractionDefinition> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Temporalio.Api.Sdk.V1.WorkflowMetadataReflection.Descriptor.MessageTypes[2]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowInteractionDefinition() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowInteractionDefinition(WorkflowInteractionDefinition other) : this() {
      name_ = other.name_;
      description_ = other.description_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowInteractionDefinition Clone() {
      return new WorkflowInteractionDefinition(this);
    }

    /// <summary>Field number for the "name" field.</summary>
    public const int NameFieldNumber = 1;
    private string name_ = "";
    /// <summary>
    /// An optional name for the handler. If missing, it represents
    /// a dynamic handler that processes any interactions not handled by others.
    /// There is at most one dynamic handler per workflow and interaction kind.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string Name {
      get { return name_; }
      set {
        name_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "description" field.</summary>
    public const int DescriptionFieldNumber = 2;
    private string description_ = "";
    /// <summary>
    /// An optional interaction description provided by the application.
    /// By convention, external tools may interpret its first part,
    /// i.e., ending with a line break, as a summary of the description.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string Description {
      get { return description_; }
      set {
        description_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as WorkflowInteractionDefinition);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(WorkflowInteractionDefinition other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Name != other.Name) return false;
      if (Description != other.Description) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (Name.Length != 0) hash ^= Name.GetHashCode();
      if (Description.Length != 0) hash ^= Description.GetHashCode();
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
      if (Name.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(Name);
      }
      if (Description.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(Description);
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
      if (Name.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(Name);
      }
      if (Description.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(Description);
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
      if (Name.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(Name);
      }
      if (Description.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(Description);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(WorkflowInteractionDefinition other) {
      if (other == null) {
        return;
      }
      if (other.Name.Length != 0) {
        Name = other.Name;
      }
      if (other.Description.Length != 0) {
        Description = other.Description;
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
            Name = input.ReadString();
            break;
          }
          case 18: {
            Description = input.ReadString();
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
            Name = input.ReadString();
            break;
          }
          case 18: {
            Description = input.ReadString();
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
