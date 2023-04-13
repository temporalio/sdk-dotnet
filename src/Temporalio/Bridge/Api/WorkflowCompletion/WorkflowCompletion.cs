// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/sdk/core/workflow_completion/workflow_completion.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Bridge.Api.WorkflowCompletion {

  /// <summary>Holder for reflection information generated from temporal/sdk/core/workflow_completion/workflow_completion.proto</summary>
  internal static partial class WorkflowCompletionReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/sdk/core/workflow_completion/workflow_completion.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static WorkflowCompletionReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Cj90ZW1wb3JhbC9zZGsvY29yZS93b3JrZmxvd19jb21wbGV0aW9uL3dvcmtm",
            "bG93X2NvbXBsZXRpb24ucHJvdG8SG2NvcmVzZGsud29ya2Zsb3dfY29tcGxl",
            "dGlvbholdGVtcG9yYWwvYXBpL2ZhaWx1cmUvdjEvbWVzc2FnZS5wcm90bxoo",
            "dGVtcG9yYWwvYXBpL2VudW1zL3YxL2ZhaWxlZF9jYXVzZS5wcm90bxoldGVt",
            "cG9yYWwvc2RrL2NvcmUvY29tbW9uL2NvbW1vbi5wcm90bxo7dGVtcG9yYWwv",
            "c2RrL2NvcmUvd29ya2Zsb3dfY29tbWFuZHMvd29ya2Zsb3dfY29tbWFuZHMu",
            "cHJvdG8irAEKHFdvcmtmbG93QWN0aXZhdGlvbkNvbXBsZXRpb24SDgoGcnVu",
            "X2lkGAEgASgJEjoKCnN1Y2Nlc3NmdWwYAiABKAsyJC5jb3Jlc2RrLndvcmtm",
            "bG93X2NvbXBsZXRpb24uU3VjY2Vzc0gAEjYKBmZhaWxlZBgDIAEoCzIkLmNv",
            "cmVzZGsud29ya2Zsb3dfY29tcGxldGlvbi5GYWlsdXJlSABCCAoGc3RhdHVz",
            "ImQKB1N1Y2Nlc3MSPAoIY29tbWFuZHMYASADKAsyKi5jb3Jlc2RrLndvcmtm",
            "bG93X2NvbW1hbmRzLldvcmtmbG93Q29tbWFuZBIbChN1c2VkX2ludGVybmFs",
            "X2ZsYWdzGAYgAygNIoEBCgdGYWlsdXJlEjEKB2ZhaWx1cmUYASABKAsyIC50",
            "ZW1wb3JhbC5hcGkuZmFpbHVyZS52MS5GYWlsdXJlEkMKC2ZvcmNlX2NhdXNl",
            "GAIgASgOMi4udGVtcG9yYWwuYXBpLmVudW1zLnYxLldvcmtmbG93VGFza0Zh",
            "aWxlZENhdXNlQi7qAitUZW1wb3JhbGlvOjpCcmlkZ2U6OkFwaTo6V29ya2Zs",
            "b3dDb21wbGV0aW9uYgZwcm90bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Temporalio.Api.Failure.V1.MessageReflection.Descriptor, global::Temporalio.Api.Enums.V1.FailedCauseReflection.Descriptor, global::Temporalio.Bridge.Api.Common.CommonReflection.Descriptor, global::Temporalio.Bridge.Api.WorkflowCommands.WorkflowCommandsReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Temporalio.Bridge.Api.WorkflowCompletion.WorkflowActivationCompletion), global::Temporalio.Bridge.Api.WorkflowCompletion.WorkflowActivationCompletion.Parser, new[]{ "RunId", "Successful", "Failed" }, new[]{ "Status" }, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::Temporalio.Bridge.Api.WorkflowCompletion.Success), global::Temporalio.Bridge.Api.WorkflowCompletion.Success.Parser, new[]{ "Commands", "UsedInternalFlags" }, null, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::Temporalio.Bridge.Api.WorkflowCompletion.Failure), global::Temporalio.Bridge.Api.WorkflowCompletion.Failure.Parser, new[]{ "Failure_", "ForceCause" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  /// <summary>
  /// Result of a single workflow activation, reported from lang to core
  /// </summary>
  internal sealed partial class WorkflowActivationCompletion : pb::IMessage<WorkflowActivationCompletion>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<WorkflowActivationCompletion> _parser = new pb::MessageParser<WorkflowActivationCompletion>(() => new WorkflowActivationCompletion());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<WorkflowActivationCompletion> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Temporalio.Bridge.Api.WorkflowCompletion.WorkflowCompletionReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowActivationCompletion() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowActivationCompletion(WorkflowActivationCompletion other) : this() {
      runId_ = other.runId_;
      switch (other.StatusCase) {
        case StatusOneofCase.Successful:
          Successful = other.Successful.Clone();
          break;
        case StatusOneofCase.Failed:
          Failed = other.Failed.Clone();
          break;
      }

      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowActivationCompletion Clone() {
      return new WorkflowActivationCompletion(this);
    }

    /// <summary>Field number for the "run_id" field.</summary>
    public const int RunIdFieldNumber = 1;
    private string runId_ = "";
    /// <summary>
    /// The run id from the workflow activation you are completing
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string RunId {
      get { return runId_; }
      set {
        runId_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "successful" field.</summary>
    public const int SuccessfulFieldNumber = 2;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Bridge.Api.WorkflowCompletion.Success Successful {
      get { return statusCase_ == StatusOneofCase.Successful ? (global::Temporalio.Bridge.Api.WorkflowCompletion.Success) status_ : null; }
      set {
        status_ = value;
        statusCase_ = value == null ? StatusOneofCase.None : StatusOneofCase.Successful;
      }
    }

    /// <summary>Field number for the "failed" field.</summary>
    public const int FailedFieldNumber = 3;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Bridge.Api.WorkflowCompletion.Failure Failed {
      get { return statusCase_ == StatusOneofCase.Failed ? (global::Temporalio.Bridge.Api.WorkflowCompletion.Failure) status_ : null; }
      set {
        status_ = value;
        statusCase_ = value == null ? StatusOneofCase.None : StatusOneofCase.Failed;
      }
    }

    private object status_;
    /// <summary>Enum of possible cases for the "status" oneof.</summary>
    public enum StatusOneofCase {
      None = 0,
      Successful = 2,
      Failed = 3,
    }
    private StatusOneofCase statusCase_ = StatusOneofCase.None;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public StatusOneofCase StatusCase {
      get { return statusCase_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearStatus() {
      statusCase_ = StatusOneofCase.None;
      status_ = null;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as WorkflowActivationCompletion);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(WorkflowActivationCompletion other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (RunId != other.RunId) return false;
      if (!object.Equals(Successful, other.Successful)) return false;
      if (!object.Equals(Failed, other.Failed)) return false;
      if (StatusCase != other.StatusCase) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (RunId.Length != 0) hash ^= RunId.GetHashCode();
      if (statusCase_ == StatusOneofCase.Successful) hash ^= Successful.GetHashCode();
      if (statusCase_ == StatusOneofCase.Failed) hash ^= Failed.GetHashCode();
      hash ^= (int) statusCase_;
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
      if (RunId.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(RunId);
      }
      if (statusCase_ == StatusOneofCase.Successful) {
        output.WriteRawTag(18);
        output.WriteMessage(Successful);
      }
      if (statusCase_ == StatusOneofCase.Failed) {
        output.WriteRawTag(26);
        output.WriteMessage(Failed);
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
      if (RunId.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(RunId);
      }
      if (statusCase_ == StatusOneofCase.Successful) {
        output.WriteRawTag(18);
        output.WriteMessage(Successful);
      }
      if (statusCase_ == StatusOneofCase.Failed) {
        output.WriteRawTag(26);
        output.WriteMessage(Failed);
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
      if (RunId.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(RunId);
      }
      if (statusCase_ == StatusOneofCase.Successful) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(Successful);
      }
      if (statusCase_ == StatusOneofCase.Failed) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(Failed);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(WorkflowActivationCompletion other) {
      if (other == null) {
        return;
      }
      if (other.RunId.Length != 0) {
        RunId = other.RunId;
      }
      switch (other.StatusCase) {
        case StatusOneofCase.Successful:
          if (Successful == null) {
            Successful = new global::Temporalio.Bridge.Api.WorkflowCompletion.Success();
          }
          Successful.MergeFrom(other.Successful);
          break;
        case StatusOneofCase.Failed:
          if (Failed == null) {
            Failed = new global::Temporalio.Bridge.Api.WorkflowCompletion.Failure();
          }
          Failed.MergeFrom(other.Failed);
          break;
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
            RunId = input.ReadString();
            break;
          }
          case 18: {
            global::Temporalio.Bridge.Api.WorkflowCompletion.Success subBuilder = new global::Temporalio.Bridge.Api.WorkflowCompletion.Success();
            if (statusCase_ == StatusOneofCase.Successful) {
              subBuilder.MergeFrom(Successful);
            }
            input.ReadMessage(subBuilder);
            Successful = subBuilder;
            break;
          }
          case 26: {
            global::Temporalio.Bridge.Api.WorkflowCompletion.Failure subBuilder = new global::Temporalio.Bridge.Api.WorkflowCompletion.Failure();
            if (statusCase_ == StatusOneofCase.Failed) {
              subBuilder.MergeFrom(Failed);
            }
            input.ReadMessage(subBuilder);
            Failed = subBuilder;
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
            RunId = input.ReadString();
            break;
          }
          case 18: {
            global::Temporalio.Bridge.Api.WorkflowCompletion.Success subBuilder = new global::Temporalio.Bridge.Api.WorkflowCompletion.Success();
            if (statusCase_ == StatusOneofCase.Successful) {
              subBuilder.MergeFrom(Successful);
            }
            input.ReadMessage(subBuilder);
            Successful = subBuilder;
            break;
          }
          case 26: {
            global::Temporalio.Bridge.Api.WorkflowCompletion.Failure subBuilder = new global::Temporalio.Bridge.Api.WorkflowCompletion.Failure();
            if (statusCase_ == StatusOneofCase.Failed) {
              subBuilder.MergeFrom(Failed);
            }
            input.ReadMessage(subBuilder);
            Failed = subBuilder;
            break;
          }
        }
      }
    }
    #endif

  }

  /// <summary>
  /// Successful workflow activation with a list of commands generated by the workflow execution
  /// </summary>
  internal sealed partial class Success : pb::IMessage<Success>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<Success> _parser = new pb::MessageParser<Success>(() => new Success());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<Success> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Temporalio.Bridge.Api.WorkflowCompletion.WorkflowCompletionReflection.Descriptor.MessageTypes[1]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public Success() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public Success(Success other) : this() {
      commands_ = other.commands_.Clone();
      usedInternalFlags_ = other.usedInternalFlags_.Clone();
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public Success Clone() {
      return new Success(this);
    }

    /// <summary>Field number for the "commands" field.</summary>
    public const int CommandsFieldNumber = 1;
    private static readonly pb::FieldCodec<global::Temporalio.Bridge.Api.WorkflowCommands.WorkflowCommand> _repeated_commands_codec
        = pb::FieldCodec.ForMessage(10, global::Temporalio.Bridge.Api.WorkflowCommands.WorkflowCommand.Parser);
    private readonly pbc::RepeatedField<global::Temporalio.Bridge.Api.WorkflowCommands.WorkflowCommand> commands_ = new pbc::RepeatedField<global::Temporalio.Bridge.Api.WorkflowCommands.WorkflowCommand>();
    /// <summary>
    /// A list of commands to send back to the temporal server
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public pbc::RepeatedField<global::Temporalio.Bridge.Api.WorkflowCommands.WorkflowCommand> Commands {
      get { return commands_; }
    }

    /// <summary>Field number for the "used_internal_flags" field.</summary>
    public const int UsedInternalFlagsFieldNumber = 6;
    private static readonly pb::FieldCodec<uint> _repeated_usedInternalFlags_codec
        = pb::FieldCodec.ForUInt32(50);
    private readonly pbc::RepeatedField<uint> usedInternalFlags_ = new pbc::RepeatedField<uint>();
    /// <summary>
    /// Any internal flags which the lang SDK used in the processing of this activation
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public pbc::RepeatedField<uint> UsedInternalFlags {
      get { return usedInternalFlags_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as Success);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(Success other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if(!commands_.Equals(other.commands_)) return false;
      if(!usedInternalFlags_.Equals(other.usedInternalFlags_)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      hash ^= commands_.GetHashCode();
      hash ^= usedInternalFlags_.GetHashCode();
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
      commands_.WriteTo(output, _repeated_commands_codec);
      usedInternalFlags_.WriteTo(output, _repeated_usedInternalFlags_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      commands_.WriteTo(ref output, _repeated_commands_codec);
      usedInternalFlags_.WriteTo(ref output, _repeated_usedInternalFlags_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      size += commands_.CalculateSize(_repeated_commands_codec);
      size += usedInternalFlags_.CalculateSize(_repeated_usedInternalFlags_codec);
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(Success other) {
      if (other == null) {
        return;
      }
      commands_.Add(other.commands_);
      usedInternalFlags_.Add(other.usedInternalFlags_);
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
            commands_.AddEntriesFrom(input, _repeated_commands_codec);
            break;
          }
          case 50:
          case 48: {
            usedInternalFlags_.AddEntriesFrom(input, _repeated_usedInternalFlags_codec);
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
            commands_.AddEntriesFrom(ref input, _repeated_commands_codec);
            break;
          }
          case 50:
          case 48: {
            usedInternalFlags_.AddEntriesFrom(ref input, _repeated_usedInternalFlags_codec);
            break;
          }
        }
      }
    }
    #endif

  }

  /// <summary>
  /// Failure to activate or execute a workflow
  /// </summary>
  internal sealed partial class Failure : pb::IMessage<Failure>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<Failure> _parser = new pb::MessageParser<Failure>(() => new Failure());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<Failure> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Temporalio.Bridge.Api.WorkflowCompletion.WorkflowCompletionReflection.Descriptor.MessageTypes[2]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public Failure() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public Failure(Failure other) : this() {
      failure_ = other.failure_ != null ? other.failure_.Clone() : null;
      forceCause_ = other.forceCause_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public Failure Clone() {
      return new Failure(this);
    }

    /// <summary>Field number for the "failure" field.</summary>
    public const int Failure_FieldNumber = 1;
    private global::Temporalio.Api.Failure.V1.Failure failure_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Api.Failure.V1.Failure Failure_ {
      get { return failure_; }
      set {
        failure_ = value;
      }
    }

    /// <summary>Field number for the "force_cause" field.</summary>
    public const int ForceCauseFieldNumber = 2;
    private global::Temporalio.Api.Enums.V1.WorkflowTaskFailedCause forceCause_ = global::Temporalio.Api.Enums.V1.WorkflowTaskFailedCause.Unspecified;
    /// <summary>
    /// Forces overriding the WFT failure cause
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Api.Enums.V1.WorkflowTaskFailedCause ForceCause {
      get { return forceCause_; }
      set {
        forceCause_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as Failure);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(Failure other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (!object.Equals(Failure_, other.Failure_)) return false;
      if (ForceCause != other.ForceCause) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (failure_ != null) hash ^= Failure_.GetHashCode();
      if (ForceCause != global::Temporalio.Api.Enums.V1.WorkflowTaskFailedCause.Unspecified) hash ^= ForceCause.GetHashCode();
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
      if (failure_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(Failure_);
      }
      if (ForceCause != global::Temporalio.Api.Enums.V1.WorkflowTaskFailedCause.Unspecified) {
        output.WriteRawTag(16);
        output.WriteEnum((int) ForceCause);
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
      if (failure_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(Failure_);
      }
      if (ForceCause != global::Temporalio.Api.Enums.V1.WorkflowTaskFailedCause.Unspecified) {
        output.WriteRawTag(16);
        output.WriteEnum((int) ForceCause);
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
      if (failure_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(Failure_);
      }
      if (ForceCause != global::Temporalio.Api.Enums.V1.WorkflowTaskFailedCause.Unspecified) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) ForceCause);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(Failure other) {
      if (other == null) {
        return;
      }
      if (other.failure_ != null) {
        if (failure_ == null) {
          Failure_ = new global::Temporalio.Api.Failure.V1.Failure();
        }
        Failure_.MergeFrom(other.Failure_);
      }
      if (other.ForceCause != global::Temporalio.Api.Enums.V1.WorkflowTaskFailedCause.Unspecified) {
        ForceCause = other.ForceCause;
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
            if (failure_ == null) {
              Failure_ = new global::Temporalio.Api.Failure.V1.Failure();
            }
            input.ReadMessage(Failure_);
            break;
          }
          case 16: {
            ForceCause = (global::Temporalio.Api.Enums.V1.WorkflowTaskFailedCause) input.ReadEnum();
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
            if (failure_ == null) {
              Failure_ = new global::Temporalio.Api.Failure.V1.Failure();
            }
            input.ReadMessage(Failure_);
            break;
          }
          case 16: {
            ForceCause = (global::Temporalio.Api.Enums.V1.WorkflowTaskFailedCause) input.ReadEnum();
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
