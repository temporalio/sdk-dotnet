// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: dependencies/gogoproto/gogo.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Gogoproto {

  /// <summary>Holder for reflection information generated from dependencies/gogoproto/gogo.proto</summary>
  public static partial class GogoReflection {

    #region Descriptor
    /// <summary>File descriptor for dependencies/gogoproto/gogo.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static GogoReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CiFkZXBlbmRlbmNpZXMvZ29nb3Byb3RvL2dvZ28ucHJvdG8SCWdvZ29wcm90",
            "bxogZ29vZ2xlL3Byb3RvYnVmL2Rlc2NyaXB0b3IucHJvdG86OwoTZ29wcm90",
            "b19lbnVtX3ByZWZpeBIcLmdvb2dsZS5wcm90b2J1Zi5FbnVtT3B0aW9ucxix",
            "5AMgASgIOj0KFWdvcHJvdG9fZW51bV9zdHJpbmdlchIcLmdvb2dsZS5wcm90",
            "b2J1Zi5FbnVtT3B0aW9ucxjF5AMgASgIOjUKDWVudW1fc3RyaW5nZXISHC5n",
            "b29nbGUucHJvdG9idWYuRW51bU9wdGlvbnMYxuQDIAEoCDo3Cg9lbnVtX2N1",
            "c3RvbW5hbWUSHC5nb29nbGUucHJvdG9idWYuRW51bU9wdGlvbnMYx+QDIAEo",
            "CTowCghlbnVtZGVjbBIcLmdvb2dsZS5wcm90b2J1Zi5FbnVtT3B0aW9ucxjI",
            "5AMgASgIOkEKFGVudW12YWx1ZV9jdXN0b21uYW1lEiEuZ29vZ2xlLnByb3Rv",
            "YnVmLkVudW1WYWx1ZU9wdGlvbnMY0YMEIAEoCTo7ChNnb3Byb3RvX2dldHRl",
            "cnNfYWxsEhwuZ29vZ2xlLnByb3RvYnVmLkZpbGVPcHRpb25zGJnsAyABKAg6",
            "PwoXZ29wcm90b19lbnVtX3ByZWZpeF9hbGwSHC5nb29nbGUucHJvdG9idWYu",
            "RmlsZU9wdGlvbnMYmuwDIAEoCDo8ChRnb3Byb3RvX3N0cmluZ2VyX2FsbBIc",
            "Lmdvb2dsZS5wcm90b2J1Zi5GaWxlT3B0aW9ucxib7AMgASgIOjkKEXZlcmJv",
            "c2VfZXF1YWxfYWxsEhwuZ29vZ2xlLnByb3RvYnVmLkZpbGVPcHRpb25zGJzs",
            "AyABKAg6MAoIZmFjZV9hbGwSHC5nb29nbGUucHJvdG9idWYuRmlsZU9wdGlv",
            "bnMYnewDIAEoCDo0Cgxnb3N0cmluZ19hbGwSHC5nb29nbGUucHJvdG9idWYu",
            "RmlsZU9wdGlvbnMYnuwDIAEoCDo0Cgxwb3B1bGF0ZV9hbGwSHC5nb29nbGUu",
            "cHJvdG9idWYuRmlsZU9wdGlvbnMYn+wDIAEoCDo0CgxzdHJpbmdlcl9hbGwS",
            "HC5nb29nbGUucHJvdG9idWYuRmlsZU9wdGlvbnMYoOwDIAEoCDozCgtvbmx5",
            "b25lX2FsbBIcLmdvb2dsZS5wcm90b2J1Zi5GaWxlT3B0aW9ucxih7AMgASgI",
            "OjEKCWVxdWFsX2FsbBIcLmdvb2dsZS5wcm90b2J1Zi5GaWxlT3B0aW9ucxil",
            "7AMgASgIOjcKD2Rlc2NyaXB0aW9uX2FsbBIcLmdvb2dsZS5wcm90b2J1Zi5G",
            "aWxlT3B0aW9ucxim7AMgASgIOjMKC3Rlc3RnZW5fYWxsEhwuZ29vZ2xlLnBy",
            "b3RvYnVmLkZpbGVPcHRpb25zGKfsAyABKAg6NAoMYmVuY2hnZW5fYWxsEhwu",
            "Z29vZ2xlLnByb3RvYnVmLkZpbGVPcHRpb25zGKjsAyABKAg6NQoNbWFyc2hh",
            "bGVyX2FsbBIcLmdvb2dsZS5wcm90b2J1Zi5GaWxlT3B0aW9ucxip7AMgASgI",
            "OjcKD3VubWFyc2hhbGVyX2FsbBIcLmdvb2dsZS5wcm90b2J1Zi5GaWxlT3B0",
            "aW9ucxiq7AMgASgIOjwKFHN0YWJsZV9tYXJzaGFsZXJfYWxsEhwuZ29vZ2xl",
            "LnByb3RvYnVmLkZpbGVPcHRpb25zGKvsAyABKAg6MQoJc2l6ZXJfYWxsEhwu",
            "Z29vZ2xlLnByb3RvYnVmLkZpbGVPcHRpb25zGKzsAyABKAg6QQoZZ29wcm90",
            "b19lbnVtX3N0cmluZ2VyX2FsbBIcLmdvb2dsZS5wcm90b2J1Zi5GaWxlT3B0",
            "aW9ucxit7AMgASgIOjkKEWVudW1fc3RyaW5nZXJfYWxsEhwuZ29vZ2xlLnBy",
            "b3RvYnVmLkZpbGVPcHRpb25zGK7sAyABKAg6PAoUdW5zYWZlX21hcnNoYWxl",
            "cl9hbGwSHC5nb29nbGUucHJvdG9idWYuRmlsZU9wdGlvbnMYr+wDIAEoCDo+",
            "ChZ1bnNhZmVfdW5tYXJzaGFsZXJfYWxsEhwuZ29vZ2xlLnByb3RvYnVmLkZp",
            "bGVPcHRpb25zGLDsAyABKAg6QgoaZ29wcm90b19leHRlbnNpb25zX21hcF9h",
            "bGwSHC5nb29nbGUucHJvdG9idWYuRmlsZU9wdGlvbnMYsewDIAEoCDpAChhn",
            "b3Byb3RvX3VucmVjb2duaXplZF9hbGwSHC5nb29nbGUucHJvdG9idWYuRmls",
            "ZU9wdGlvbnMYsuwDIAEoCDo4ChBnb2dvcHJvdG9faW1wb3J0EhwuZ29vZ2xl",
            "LnByb3RvYnVmLkZpbGVPcHRpb25zGLPsAyABKAg6NgoOcHJvdG9zaXplcl9h",
            "bGwSHC5nb29nbGUucHJvdG9idWYuRmlsZU9wdGlvbnMYtOwDIAEoCDozCgtj",
            "b21wYXJlX2FsbBIcLmdvb2dsZS5wcm90b2J1Zi5GaWxlT3B0aW9ucxi17AMg",
            "ASgIOjQKDHR5cGVkZWNsX2FsbBIcLmdvb2dsZS5wcm90b2J1Zi5GaWxlT3B0",
            "aW9ucxi27AMgASgIOjQKDGVudW1kZWNsX2FsbBIcLmdvb2dsZS5wcm90b2J1",
            "Zi5GaWxlT3B0aW9ucxi37AMgASgIOjwKFGdvcHJvdG9fcmVnaXN0cmF0aW9u",
            "EhwuZ29vZ2xlLnByb3RvYnVmLkZpbGVPcHRpb25zGLjsAyABKAg6NwoPbWVz",
            "c2FnZW5hbWVfYWxsEhwuZ29vZ2xlLnByb3RvYnVmLkZpbGVPcHRpb25zGLns",
            "AyABKAg6PQoVZ29wcm90b19zaXplY2FjaGVfYWxsEhwuZ29vZ2xlLnByb3Rv",
            "YnVmLkZpbGVPcHRpb25zGLrsAyABKAg6OwoTZ29wcm90b191bmtleWVkX2Fs",
            "bBIcLmdvb2dsZS5wcm90b2J1Zi5GaWxlT3B0aW9ucxi77AMgASgIOjoKD2dv",
            "cHJvdG9fZ2V0dGVycxIfLmdvb2dsZS5wcm90b2J1Zi5NZXNzYWdlT3B0aW9u",
            "cxiB9AMgASgIOjsKEGdvcHJvdG9fc3RyaW5nZXISHy5nb29nbGUucHJvdG9i",
            "dWYuTWVzc2FnZU9wdGlvbnMYg/QDIAEoCDo4Cg12ZXJib3NlX2VxdWFsEh8u",
            "Z29vZ2xlLnByb3RvYnVmLk1lc3NhZ2VPcHRpb25zGIT0AyABKAg6LwoEZmFj",
            "ZRIfLmdvb2dsZS5wcm90b2J1Zi5NZXNzYWdlT3B0aW9ucxiF9AMgASgIOjMK",
            "CGdvc3RyaW5nEh8uZ29vZ2xlLnByb3RvYnVmLk1lc3NhZ2VPcHRpb25zGIb0",
            "AyABKAg6MwoIcG9wdWxhdGUSHy5nb29nbGUucHJvdG9idWYuTWVzc2FnZU9w",
            "dGlvbnMYh/QDIAEoCDozCghzdHJpbmdlchIfLmdvb2dsZS5wcm90b2J1Zi5N",
            "ZXNzYWdlT3B0aW9ucxjAiwQgASgIOjIKB29ubHlvbmUSHy5nb29nbGUucHJv",
            "dG9idWYuTWVzc2FnZU9wdGlvbnMYifQDIAEoCDowCgVlcXVhbBIfLmdvb2ds",
            "ZS5wcm90b2J1Zi5NZXNzYWdlT3B0aW9ucxiN9AMgASgIOjYKC2Rlc2NyaXB0",
            "aW9uEh8uZ29vZ2xlLnByb3RvYnVmLk1lc3NhZ2VPcHRpb25zGI70AyABKAg6",
            "MgoHdGVzdGdlbhIfLmdvb2dsZS5wcm90b2J1Zi5NZXNzYWdlT3B0aW9ucxiP",
            "9AMgASgIOjMKCGJlbmNoZ2VuEh8uZ29vZ2xlLnByb3RvYnVmLk1lc3NhZ2VP",
            "cHRpb25zGJD0AyABKAg6NAoJbWFyc2hhbGVyEh8uZ29vZ2xlLnByb3RvYnVm",
            "Lk1lc3NhZ2VPcHRpb25zGJH0AyABKAg6NgoLdW5tYXJzaGFsZXISHy5nb29n",
            "bGUucHJvdG9idWYuTWVzc2FnZU9wdGlvbnMYkvQDIAEoCDo7ChBzdGFibGVf",
            "bWFyc2hhbGVyEh8uZ29vZ2xlLnByb3RvYnVmLk1lc3NhZ2VPcHRpb25zGJP0",
            "AyABKAg6MAoFc2l6ZXISHy5nb29nbGUucHJvdG9idWYuTWVzc2FnZU9wdGlv",
            "bnMYlPQDIAEoCDo7ChB1bnNhZmVfbWFyc2hhbGVyEh8uZ29vZ2xlLnByb3Rv",
            "YnVmLk1lc3NhZ2VPcHRpb25zGJf0AyABKAg6PQoSdW5zYWZlX3VubWFyc2hh",
            "bGVyEh8uZ29vZ2xlLnByb3RvYnVmLk1lc3NhZ2VPcHRpb25zGJj0AyABKAg6",
            "QQoWZ29wcm90b19leHRlbnNpb25zX21hcBIfLmdvb2dsZS5wcm90b2J1Zi5N",
            "ZXNzYWdlT3B0aW9ucxiZ9AMgASgIOj8KFGdvcHJvdG9fdW5yZWNvZ25pemVk",
            "Eh8uZ29vZ2xlLnByb3RvYnVmLk1lc3NhZ2VPcHRpb25zGJr0AyABKAg6NQoK",
            "cHJvdG9zaXplchIfLmdvb2dsZS5wcm90b2J1Zi5NZXNzYWdlT3B0aW9ucxic",
            "9AMgASgIOjIKB2NvbXBhcmUSHy5nb29nbGUucHJvdG9idWYuTWVzc2FnZU9w",
            "dGlvbnMYnfQDIAEoCDozCgh0eXBlZGVjbBIfLmdvb2dsZS5wcm90b2J1Zi5N",
            "ZXNzYWdlT3B0aW9ucxie9AMgASgIOjYKC21lc3NhZ2VuYW1lEh8uZ29vZ2xl",
            "LnByb3RvYnVmLk1lc3NhZ2VPcHRpb25zGKH0AyABKAg6PAoRZ29wcm90b19z",
            "aXplY2FjaGUSHy5nb29nbGUucHJvdG9idWYuTWVzc2FnZU9wdGlvbnMYovQD",
            "IAEoCDo6Cg9nb3Byb3RvX3Vua2V5ZWQSHy5nb29nbGUucHJvdG9idWYuTWVz",
            "c2FnZU9wdGlvbnMYo/QDIAEoCDoxCghudWxsYWJsZRIdLmdvb2dsZS5wcm90",
            "b2J1Zi5GaWVsZE9wdGlvbnMY6fsDIAEoCDouCgVlbWJlZBIdLmdvb2dsZS5w",
            "cm90b2J1Zi5GaWVsZE9wdGlvbnMY6vsDIAEoCDozCgpjdXN0b210eXBlEh0u",
            "Z29vZ2xlLnByb3RvYnVmLkZpZWxkT3B0aW9ucxjr+wMgASgJOjMKCmN1c3Rv",
            "bW5hbWUSHS5nb29nbGUucHJvdG9idWYuRmllbGRPcHRpb25zGOz7AyABKAk6",
            "MAoHanNvbnRhZxIdLmdvb2dsZS5wcm90b2J1Zi5GaWVsZE9wdGlvbnMY7fsD",
            "IAEoCToxCghtb3JldGFncxIdLmdvb2dsZS5wcm90b2J1Zi5GaWVsZE9wdGlv",
            "bnMY7vsDIAEoCToxCghjYXN0dHlwZRIdLmdvb2dsZS5wcm90b2J1Zi5GaWVs",
            "ZE9wdGlvbnMY7/sDIAEoCTowCgdjYXN0a2V5Eh0uZ29vZ2xlLnByb3RvYnVm",
            "LkZpZWxkT3B0aW9ucxjw+wMgASgJOjIKCWNhc3R2YWx1ZRIdLmdvb2dsZS5w",
            "cm90b2J1Zi5GaWVsZE9wdGlvbnMY8fsDIAEoCTowCgdzdGR0aW1lEh0uZ29v",
            "Z2xlLnByb3RvYnVmLkZpZWxkT3B0aW9ucxjy+wMgASgIOjQKC3N0ZGR1cmF0",
            "aW9uEh0uZ29vZ2xlLnByb3RvYnVmLkZpZWxkT3B0aW9ucxjz+wMgASgIOjMK",
            "CndrdHBvaW50ZXISHS5nb29nbGUucHJvdG9idWYuRmllbGRPcHRpb25zGPT7",
            "AyABKAhCJFoiZ2l0aHViLmNvbS9nb2dvL3Byb3RvYnVmL2dvZ29wcm90bw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Google.Protobuf.Reflection.DescriptorReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, new pb::Extension[] { GogoExtensions.GoprotoEnumPrefix, GogoExtensions.GoprotoEnumStringer, GogoExtensions.EnumStringer, GogoExtensions.EnumCustomname, GogoExtensions.Enumdecl, GogoExtensions.EnumvalueCustomname, GogoExtensions.GoprotoGettersAll, GogoExtensions.GoprotoEnumPrefixAll, GogoExtensions.GoprotoStringerAll, GogoExtensions.VerboseEqualAll, GogoExtensions.FaceAll, GogoExtensions.GostringAll, GogoExtensions.PopulateAll, GogoExtensions.StringerAll, GogoExtensions.OnlyoneAll, GogoExtensions.EqualAll, GogoExtensions.DescriptionAll, GogoExtensions.TestgenAll, GogoExtensions.BenchgenAll, GogoExtensions.MarshalerAll, GogoExtensions.UnmarshalerAll, GogoExtensions.StableMarshalerAll, GogoExtensions.SizerAll, GogoExtensions.GoprotoEnumStringerAll, GogoExtensions.EnumStringerAll, GogoExtensions.UnsafeMarshalerAll, GogoExtensions.UnsafeUnmarshalerAll, GogoExtensions.GoprotoExtensionsMapAll, GogoExtensions.GoprotoUnrecognizedAll, GogoExtensions.GogoprotoImport, GogoExtensions.ProtosizerAll, GogoExtensions.CompareAll, GogoExtensions.TypedeclAll, GogoExtensions.EnumdeclAll, GogoExtensions.GoprotoRegistration, GogoExtensions.MessagenameAll, GogoExtensions.GoprotoSizecacheAll, GogoExtensions.GoprotoUnkeyedAll, GogoExtensions.GoprotoGetters, GogoExtensions.GoprotoStringer, GogoExtensions.VerboseEqual, GogoExtensions.Face, GogoExtensions.Gostring, GogoExtensions.Populate, GogoExtensions.Stringer, GogoExtensions.Onlyone, GogoExtensions.Equal, GogoExtensions.Description, GogoExtensions.Testgen, GogoExtensions.Benchgen, GogoExtensions.Marshaler, GogoExtensions.Unmarshaler, GogoExtensions.StableMarshaler, GogoExtensions.Sizer, GogoExtensions.UnsafeMarshaler, GogoExtensions.UnsafeUnmarshaler, GogoExtensions.GoprotoExtensionsMap, GogoExtensions.GoprotoUnrecognized, GogoExtensions.Protosizer, GogoExtensions.Compare, GogoExtensions.Typedecl, GogoExtensions.Messagename, GogoExtensions.GoprotoSizecache, GogoExtensions.GoprotoUnkeyed, GogoExtensions.Nullable, GogoExtensions.Embed, GogoExtensions.Customtype, GogoExtensions.Customname, GogoExtensions.Jsontag, GogoExtensions.Moretags, GogoExtensions.Casttype, GogoExtensions.Castkey, GogoExtensions.Castvalue, GogoExtensions.Stdtime, GogoExtensions.Stdduration, GogoExtensions.Wktpointer }, null));
    }
    #endregion

  }
  /// <summary>Holder for extension identifiers generated from the top level of dependencies/gogoproto/gogo.proto</summary>
  public static partial class GogoExtensions {
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.EnumOptions, bool> GoprotoEnumPrefix =
      new pb::Extension<global::Google.Protobuf.Reflection.EnumOptions, bool>(62001, pb::FieldCodec.ForBool(496008, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.EnumOptions, bool> GoprotoEnumStringer =
      new pb::Extension<global::Google.Protobuf.Reflection.EnumOptions, bool>(62021, pb::FieldCodec.ForBool(496168, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.EnumOptions, bool> EnumStringer =
      new pb::Extension<global::Google.Protobuf.Reflection.EnumOptions, bool>(62022, pb::FieldCodec.ForBool(496176, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.EnumOptions, string> EnumCustomname =
      new pb::Extension<global::Google.Protobuf.Reflection.EnumOptions, string>(62023, pb::FieldCodec.ForString(496186, ""));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.EnumOptions, bool> Enumdecl =
      new pb::Extension<global::Google.Protobuf.Reflection.EnumOptions, bool>(62024, pb::FieldCodec.ForBool(496192, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.EnumValueOptions, string> EnumvalueCustomname =
      new pb::Extension<global::Google.Protobuf.Reflection.EnumValueOptions, string>(66001, pb::FieldCodec.ForString(528010, ""));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> GoprotoGettersAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63001, pb::FieldCodec.ForBool(504008, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> GoprotoEnumPrefixAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63002, pb::FieldCodec.ForBool(504016, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> GoprotoStringerAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63003, pb::FieldCodec.ForBool(504024, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> VerboseEqualAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63004, pb::FieldCodec.ForBool(504032, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> FaceAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63005, pb::FieldCodec.ForBool(504040, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> GostringAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63006, pb::FieldCodec.ForBool(504048, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> PopulateAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63007, pb::FieldCodec.ForBool(504056, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> StringerAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63008, pb::FieldCodec.ForBool(504064, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> OnlyoneAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63009, pb::FieldCodec.ForBool(504072, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> EqualAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63013, pb::FieldCodec.ForBool(504104, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> DescriptionAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63014, pb::FieldCodec.ForBool(504112, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> TestgenAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63015, pb::FieldCodec.ForBool(504120, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> BenchgenAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63016, pb::FieldCodec.ForBool(504128, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> MarshalerAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63017, pb::FieldCodec.ForBool(504136, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> UnmarshalerAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63018, pb::FieldCodec.ForBool(504144, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> StableMarshalerAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63019, pb::FieldCodec.ForBool(504152, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> SizerAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63020, pb::FieldCodec.ForBool(504160, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> GoprotoEnumStringerAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63021, pb::FieldCodec.ForBool(504168, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> EnumStringerAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63022, pb::FieldCodec.ForBool(504176, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> UnsafeMarshalerAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63023, pb::FieldCodec.ForBool(504184, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> UnsafeUnmarshalerAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63024, pb::FieldCodec.ForBool(504192, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> GoprotoExtensionsMapAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63025, pb::FieldCodec.ForBool(504200, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> GoprotoUnrecognizedAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63026, pb::FieldCodec.ForBool(504208, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> GogoprotoImport =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63027, pb::FieldCodec.ForBool(504216, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> ProtosizerAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63028, pb::FieldCodec.ForBool(504224, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> CompareAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63029, pb::FieldCodec.ForBool(504232, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> TypedeclAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63030, pb::FieldCodec.ForBool(504240, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> EnumdeclAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63031, pb::FieldCodec.ForBool(504248, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> GoprotoRegistration =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63032, pb::FieldCodec.ForBool(504256, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> MessagenameAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63033, pb::FieldCodec.ForBool(504264, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> GoprotoSizecacheAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63034, pb::FieldCodec.ForBool(504272, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool> GoprotoUnkeyedAll =
      new pb::Extension<global::Google.Protobuf.Reflection.FileOptions, bool>(63035, pb::FieldCodec.ForBool(504280, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> GoprotoGetters =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64001, pb::FieldCodec.ForBool(512008, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> GoprotoStringer =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64003, pb::FieldCodec.ForBool(512024, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> VerboseEqual =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64004, pb::FieldCodec.ForBool(512032, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Face =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64005, pb::FieldCodec.ForBool(512040, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Gostring =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64006, pb::FieldCodec.ForBool(512048, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Populate =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64007, pb::FieldCodec.ForBool(512056, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Stringer =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(67008, pb::FieldCodec.ForBool(536064, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Onlyone =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64009, pb::FieldCodec.ForBool(512072, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Equal =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64013, pb::FieldCodec.ForBool(512104, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Description =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64014, pb::FieldCodec.ForBool(512112, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Testgen =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64015, pb::FieldCodec.ForBool(512120, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Benchgen =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64016, pb::FieldCodec.ForBool(512128, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Marshaler =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64017, pb::FieldCodec.ForBool(512136, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Unmarshaler =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64018, pb::FieldCodec.ForBool(512144, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> StableMarshaler =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64019, pb::FieldCodec.ForBool(512152, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Sizer =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64020, pb::FieldCodec.ForBool(512160, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> UnsafeMarshaler =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64023, pb::FieldCodec.ForBool(512184, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> UnsafeUnmarshaler =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64024, pb::FieldCodec.ForBool(512192, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> GoprotoExtensionsMap =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64025, pb::FieldCodec.ForBool(512200, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> GoprotoUnrecognized =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64026, pb::FieldCodec.ForBool(512208, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Protosizer =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64028, pb::FieldCodec.ForBool(512224, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Compare =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64029, pb::FieldCodec.ForBool(512232, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Typedecl =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64030, pb::FieldCodec.ForBool(512240, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> Messagename =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64033, pb::FieldCodec.ForBool(512264, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> GoprotoSizecache =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64034, pb::FieldCodec.ForBool(512272, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool> GoprotoUnkeyed =
      new pb::Extension<global::Google.Protobuf.Reflection.MessageOptions, bool>(64035, pb::FieldCodec.ForBool(512280, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, bool> Nullable =
      new pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, bool>(65001, pb::FieldCodec.ForBool(520008, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, bool> Embed =
      new pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, bool>(65002, pb::FieldCodec.ForBool(520016, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string> Customtype =
      new pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string>(65003, pb::FieldCodec.ForString(520026, ""));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string> Customname =
      new pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string>(65004, pb::FieldCodec.ForString(520034, ""));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string> Jsontag =
      new pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string>(65005, pb::FieldCodec.ForString(520042, ""));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string> Moretags =
      new pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string>(65006, pb::FieldCodec.ForString(520050, ""));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string> Casttype =
      new pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string>(65007, pb::FieldCodec.ForString(520058, ""));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string> Castkey =
      new pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string>(65008, pb::FieldCodec.ForString(520066, ""));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string> Castvalue =
      new pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, string>(65009, pb::FieldCodec.ForString(520074, ""));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, bool> Stdtime =
      new pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, bool>(65010, pb::FieldCodec.ForBool(520080, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, bool> Stdduration =
      new pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, bool>(65011, pb::FieldCodec.ForBool(520088, false));
    public static readonly pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, bool> Wktpointer =
      new pb::Extension<global::Google.Protobuf.Reflection.FieldOptions, bool>(65012, pb::FieldCodec.ForBool(520096, false));
  }

}

#endregion Designer generated code
