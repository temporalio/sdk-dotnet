// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/query/v1/message.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.Query.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/query/v1/message.proto</summary>
  public static partial class MessageReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/query/v1/message.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static MessageReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CiN0ZW1wb3JhbC9hcGkvcXVlcnkvdjEvbWVzc2FnZS5wcm90bxIVdGVtcG9y",
            "YWwuYXBpLnF1ZXJ5LnYxGiF0ZW1wb3JhbC9hcGkvZW51bXMvdjEvcXVlcnku",
            "cHJvdG8aJHRlbXBvcmFsL2FwaS9lbnVtcy92MS93b3JrZmxvdy5wcm90bxok",
            "dGVtcG9yYWwvYXBpL2NvbW1vbi92MS9tZXNzYWdlLnByb3RvIokBCg1Xb3Jr",
            "Zmxvd1F1ZXJ5EhIKCnF1ZXJ5X3R5cGUYASABKAkSNAoKcXVlcnlfYXJncxgC",
            "IAEoCzIgLnRlbXBvcmFsLmFwaS5jb21tb24udjEuUGF5bG9hZHMSLgoGaGVh",
            "ZGVyGAMgASgLMh4udGVtcG9yYWwuYXBpLmNvbW1vbi52MS5IZWFkZXIimwEK",
            "E1dvcmtmbG93UXVlcnlSZXN1bHQSOwoLcmVzdWx0X3R5cGUYASABKA4yJi50",
            "ZW1wb3JhbC5hcGkuZW51bXMudjEuUXVlcnlSZXN1bHRUeXBlEjAKBmFuc3dl",
            "chgCIAEoCzIgLnRlbXBvcmFsLmFwaS5jb21tb24udjEuUGF5bG9hZHMSFQoN",
            "ZXJyb3JfbWVzc2FnZRgDIAEoCSJPCg1RdWVyeVJlamVjdGVkEj4KBnN0YXR1",
            "cxgBIAEoDjIuLnRlbXBvcmFsLmFwaS5lbnVtcy52MS5Xb3JrZmxvd0V4ZWN1",
            "dGlvblN0YXR1c0KEAQoYaW8udGVtcG9yYWwuYXBpLnF1ZXJ5LnYxQgxNZXNz",
            "YWdlUHJvdG9QAVohZ28udGVtcG9yYWwuaW8vYXBpL3F1ZXJ5L3YxO3F1ZXJ5",
            "qgIXVGVtcG9yYWxpby5BcGkuUXVlcnkuVjHqAhpUZW1wb3JhbGlvOjpBcGk6",
            "OlF1ZXJ5OjpWMWIGcHJvdG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Temporalio.Api.Enums.V1.QueryReflection.Descriptor, global::Temporalio.Api.Enums.V1.WorkflowReflection.Descriptor, global::Temporalio.Api.Common.V1.MessageReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Temporalio.Api.Query.V1.WorkflowQuery), global::Temporalio.Api.Query.V1.WorkflowQuery.Parser, new[]{ "QueryType", "QueryArgs", "Header" }, null, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::Temporalio.Api.Query.V1.WorkflowQueryResult), global::Temporalio.Api.Query.V1.WorkflowQueryResult.Parser, new[]{ "ResultType", "Answer", "ErrorMessage" }, null, null, null, null),
            new pbr::GeneratedClrTypeInfo(typeof(global::Temporalio.Api.Query.V1.QueryRejected), global::Temporalio.Api.Query.V1.QueryRejected.Parser, new[]{ "Status" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  /// <summary>
  /// See https://docs.temporal.io/docs/concepts/queries/
  /// </summary>
  [global::System.Diagnostics.DebuggerDisplayAttribute("{ToString(),nq}")]
  public sealed partial class WorkflowQuery : pb::IMessage<WorkflowQuery>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<WorkflowQuery> _parser = new pb::MessageParser<WorkflowQuery>(() => new WorkflowQuery());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<WorkflowQuery> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Temporalio.Api.Query.V1.MessageReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowQuery() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowQuery(WorkflowQuery other) : this() {
      queryType_ = other.queryType_;
      queryArgs_ = other.queryArgs_ != null ? other.queryArgs_.Clone() : null;
      header_ = other.header_ != null ? other.header_.Clone() : null;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowQuery Clone() {
      return new WorkflowQuery(this);
    }

    /// <summary>Field number for the "query_type" field.</summary>
    public const int QueryTypeFieldNumber = 1;
    private string queryType_ = "";
    /// <summary>
    /// The workflow-author-defined identifier of the query. Typically a function name.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string QueryType {
      get { return queryType_; }
      set {
        queryType_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "query_args" field.</summary>
    public const int QueryArgsFieldNumber = 2;
    private global::Temporalio.Api.Common.V1.Payloads queryArgs_;
    /// <summary>
    /// Serialized arguments that will be provided to the query handler.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Api.Common.V1.Payloads QueryArgs {
      get { return queryArgs_; }
      set {
        queryArgs_ = value;
      }
    }

    /// <summary>Field number for the "header" field.</summary>
    public const int HeaderFieldNumber = 3;
    private global::Temporalio.Api.Common.V1.Header header_;
    /// <summary>
    /// Headers that were passed by the caller of the query and copied by temporal 
    /// server into the workflow task.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Api.Common.V1.Header Header {
      get { return header_; }
      set {
        header_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as WorkflowQuery);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(WorkflowQuery other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (QueryType != other.QueryType) return false;
      if (!object.Equals(QueryArgs, other.QueryArgs)) return false;
      if (!object.Equals(Header, other.Header)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (QueryType.Length != 0) hash ^= QueryType.GetHashCode();
      if (queryArgs_ != null) hash ^= QueryArgs.GetHashCode();
      if (header_ != null) hash ^= Header.GetHashCode();
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
      if (QueryType.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(QueryType);
      }
      if (queryArgs_ != null) {
        output.WriteRawTag(18);
        output.WriteMessage(QueryArgs);
      }
      if (header_ != null) {
        output.WriteRawTag(26);
        output.WriteMessage(Header);
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
      if (QueryType.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(QueryType);
      }
      if (queryArgs_ != null) {
        output.WriteRawTag(18);
        output.WriteMessage(QueryArgs);
      }
      if (header_ != null) {
        output.WriteRawTag(26);
        output.WriteMessage(Header);
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
      if (QueryType.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(QueryType);
      }
      if (queryArgs_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(QueryArgs);
      }
      if (header_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(Header);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(WorkflowQuery other) {
      if (other == null) {
        return;
      }
      if (other.QueryType.Length != 0) {
        QueryType = other.QueryType;
      }
      if (other.queryArgs_ != null) {
        if (queryArgs_ == null) {
          QueryArgs = new global::Temporalio.Api.Common.V1.Payloads();
        }
        QueryArgs.MergeFrom(other.QueryArgs);
      }
      if (other.header_ != null) {
        if (header_ == null) {
          Header = new global::Temporalio.Api.Common.V1.Header();
        }
        Header.MergeFrom(other.Header);
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
            QueryType = input.ReadString();
            break;
          }
          case 18: {
            if (queryArgs_ == null) {
              QueryArgs = new global::Temporalio.Api.Common.V1.Payloads();
            }
            input.ReadMessage(QueryArgs);
            break;
          }
          case 26: {
            if (header_ == null) {
              Header = new global::Temporalio.Api.Common.V1.Header();
            }
            input.ReadMessage(Header);
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
            QueryType = input.ReadString();
            break;
          }
          case 18: {
            if (queryArgs_ == null) {
              QueryArgs = new global::Temporalio.Api.Common.V1.Payloads();
            }
            input.ReadMessage(QueryArgs);
            break;
          }
          case 26: {
            if (header_ == null) {
              Header = new global::Temporalio.Api.Common.V1.Header();
            }
            input.ReadMessage(Header);
            break;
          }
        }
      }
    }
    #endif

  }

  /// <summary>
  /// Answer to a `WorkflowQuery`
  /// </summary>
  [global::System.Diagnostics.DebuggerDisplayAttribute("{ToString(),nq}")]
  public sealed partial class WorkflowQueryResult : pb::IMessage<WorkflowQueryResult>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<WorkflowQueryResult> _parser = new pb::MessageParser<WorkflowQueryResult>(() => new WorkflowQueryResult());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<WorkflowQueryResult> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Temporalio.Api.Query.V1.MessageReflection.Descriptor.MessageTypes[1]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowQueryResult() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowQueryResult(WorkflowQueryResult other) : this() {
      resultType_ = other.resultType_;
      answer_ = other.answer_ != null ? other.answer_.Clone() : null;
      errorMessage_ = other.errorMessage_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public WorkflowQueryResult Clone() {
      return new WorkflowQueryResult(this);
    }

    /// <summary>Field number for the "result_type" field.</summary>
    public const int ResultTypeFieldNumber = 1;
    private global::Temporalio.Api.Enums.V1.QueryResultType resultType_ = global::Temporalio.Api.Enums.V1.QueryResultType.Unspecified;
    /// <summary>
    /// Did the query succeed or fail?
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Api.Enums.V1.QueryResultType ResultType {
      get { return resultType_; }
      set {
        resultType_ = value;
      }
    }

    /// <summary>Field number for the "answer" field.</summary>
    public const int AnswerFieldNumber = 2;
    private global::Temporalio.Api.Common.V1.Payloads answer_;
    /// <summary>
    /// Set when the query succeeds with the results
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Api.Common.V1.Payloads Answer {
      get { return answer_; }
      set {
        answer_ = value;
      }
    }

    /// <summary>Field number for the "error_message" field.</summary>
    public const int ErrorMessageFieldNumber = 3;
    private string errorMessage_ = "";
    /// <summary>
    /// Mutually exclusive with `answer`. Set when the query fails.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string ErrorMessage {
      get { return errorMessage_; }
      set {
        errorMessage_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as WorkflowQueryResult);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(WorkflowQueryResult other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (ResultType != other.ResultType) return false;
      if (!object.Equals(Answer, other.Answer)) return false;
      if (ErrorMessage != other.ErrorMessage) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (ResultType != global::Temporalio.Api.Enums.V1.QueryResultType.Unspecified) hash ^= ResultType.GetHashCode();
      if (answer_ != null) hash ^= Answer.GetHashCode();
      if (ErrorMessage.Length != 0) hash ^= ErrorMessage.GetHashCode();
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
      if (ResultType != global::Temporalio.Api.Enums.V1.QueryResultType.Unspecified) {
        output.WriteRawTag(8);
        output.WriteEnum((int) ResultType);
      }
      if (answer_ != null) {
        output.WriteRawTag(18);
        output.WriteMessage(Answer);
      }
      if (ErrorMessage.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(ErrorMessage);
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
      if (ResultType != global::Temporalio.Api.Enums.V1.QueryResultType.Unspecified) {
        output.WriteRawTag(8);
        output.WriteEnum((int) ResultType);
      }
      if (answer_ != null) {
        output.WriteRawTag(18);
        output.WriteMessage(Answer);
      }
      if (ErrorMessage.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(ErrorMessage);
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
      if (ResultType != global::Temporalio.Api.Enums.V1.QueryResultType.Unspecified) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) ResultType);
      }
      if (answer_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(Answer);
      }
      if (ErrorMessage.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(ErrorMessage);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(WorkflowQueryResult other) {
      if (other == null) {
        return;
      }
      if (other.ResultType != global::Temporalio.Api.Enums.V1.QueryResultType.Unspecified) {
        ResultType = other.ResultType;
      }
      if (other.answer_ != null) {
        if (answer_ == null) {
          Answer = new global::Temporalio.Api.Common.V1.Payloads();
        }
        Answer.MergeFrom(other.Answer);
      }
      if (other.ErrorMessage.Length != 0) {
        ErrorMessage = other.ErrorMessage;
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
            ResultType = (global::Temporalio.Api.Enums.V1.QueryResultType) input.ReadEnum();
            break;
          }
          case 18: {
            if (answer_ == null) {
              Answer = new global::Temporalio.Api.Common.V1.Payloads();
            }
            input.ReadMessage(Answer);
            break;
          }
          case 26: {
            ErrorMessage = input.ReadString();
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
            ResultType = (global::Temporalio.Api.Enums.V1.QueryResultType) input.ReadEnum();
            break;
          }
          case 18: {
            if (answer_ == null) {
              Answer = new global::Temporalio.Api.Common.V1.Payloads();
            }
            input.ReadMessage(Answer);
            break;
          }
          case 26: {
            ErrorMessage = input.ReadString();
            break;
          }
        }
      }
    }
    #endif

  }

  [global::System.Diagnostics.DebuggerDisplayAttribute("{ToString(),nq}")]
  public sealed partial class QueryRejected : pb::IMessage<QueryRejected>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<QueryRejected> _parser = new pb::MessageParser<QueryRejected>(() => new QueryRejected());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<QueryRejected> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Temporalio.Api.Query.V1.MessageReflection.Descriptor.MessageTypes[2]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public QueryRejected() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public QueryRejected(QueryRejected other) : this() {
      status_ = other.status_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public QueryRejected Clone() {
      return new QueryRejected(this);
    }

    /// <summary>Field number for the "status" field.</summary>
    public const int StatusFieldNumber = 1;
    private global::Temporalio.Api.Enums.V1.WorkflowExecutionStatus status_ = global::Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Unspecified;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Temporalio.Api.Enums.V1.WorkflowExecutionStatus Status {
      get { return status_; }
      set {
        status_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as QueryRejected);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(QueryRejected other) {
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
      if (Status != global::Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Unspecified) hash ^= Status.GetHashCode();
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
      if (Status != global::Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Unspecified) {
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
      if (Status != global::Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Unspecified) {
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
      if (Status != global::Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Unspecified) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) Status);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(QueryRejected other) {
      if (other == null) {
        return;
      }
      if (other.Status != global::Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Unspecified) {
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
            Status = (global::Temporalio.Api.Enums.V1.WorkflowExecutionStatus) input.ReadEnum();
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
            Status = (global::Temporalio.Api.Enums.V1.WorkflowExecutionStatus) input.ReadEnum();
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
