// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/workflowservice/v1/service.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.WorkflowService.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/workflowservice/v1/service.proto</summary>
  public static partial class ServiceReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/workflowservice/v1/service.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static ServiceReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Ci10ZW1wb3JhbC9hcGkvd29ya2Zsb3dzZXJ2aWNlL3YxL3NlcnZpY2UucHJv",
            "dG8SH3RlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEaNnRlbXBvcmFs",
            "L2FwaS93b3JrZmxvd3NlcnZpY2UvdjEvcmVxdWVzdF9yZXNwb25zZS5wcm90",
            "bxocZ29vZ2xlL2FwaS9hbm5vdGF0aW9ucy5wcm90bzL6pQEKD1dvcmtmbG93",
            "U2VydmljZRLDAQoRUmVnaXN0ZXJOYW1lc3BhY2USOS50ZW1wb3JhbC5hcGku",
            "d29ya2Zsb3dzZXJ2aWNlLnYxLlJlZ2lzdGVyTmFtZXNwYWNlUmVxdWVzdBo6",
            "LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuUmVnaXN0ZXJOYW1l",
            "c3BhY2VSZXNwb25zZSI3gtPkkwIxIhMvY2x1c3Rlci9uYW1lc3BhY2VzOgEq",
            "WhciEi9hcGkvdjEvbmFtZXNwYWNlczoBKhLVAQoRRGVzY3JpYmVOYW1lc3Bh",
            "Y2USOS50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkRlc2NyaWJl",
            "TmFtZXNwYWNlUmVxdWVzdBo6LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZp",
            "Y2UudjEuRGVzY3JpYmVOYW1lc3BhY2VSZXNwb25zZSJJgtPkkwJDEh8vY2x1",
            "c3Rlci9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9WiASHi9hcGkvdjEvbmFtZXNw",
            "YWNlcy97bmFtZXNwYWNlfRK0AQoOTGlzdE5hbWVzcGFjZXMSNi50ZW1wb3Jh",
            "bC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkxpc3ROYW1lc3BhY2VzUmVxdWVz",
            "dBo3LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuTGlzdE5hbWVz",
            "cGFjZXNSZXNwb25zZSIxgtPkkwIrEhMvY2x1c3Rlci9uYW1lc3BhY2VzWhQS",
            "Ei9hcGkvdjEvbmFtZXNwYWNlcxLjAQoPVXBkYXRlTmFtZXNwYWNlEjcudGVt",
            "cG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5VcGRhdGVOYW1lc3BhY2VS",
            "ZXF1ZXN0GjgudGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5VcGRh",
            "dGVOYW1lc3BhY2VSZXNwb25zZSJdgtPkkwJXIiYvY2x1c3Rlci9uYW1lc3Bh",
            "Y2VzL3tuYW1lc3BhY2V9L3VwZGF0ZToBKloqIiUvYXBpL3YxL25hbWVzcGFj",
            "ZXMve25hbWVzcGFjZX0vdXBkYXRlOgEqEo8BChJEZXByZWNhdGVOYW1lc3Bh",
            "Y2USOi50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkRlcHJlY2F0",
            "ZU5hbWVzcGFjZVJlcXVlc3QaOy50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2",
            "aWNlLnYxLkRlcHJlY2F0ZU5hbWVzcGFjZVJlc3BvbnNlIgASkgIKFlN0YXJ0",
            "V29ya2Zsb3dFeGVjdXRpb24SPi50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2",
            "aWNlLnYxLlN0YXJ0V29ya2Zsb3dFeGVjdXRpb25SZXF1ZXN0Gj8udGVtcG9y",
            "YWwuYXBpLndvcmtmbG93c2VydmljZS52MS5TdGFydFdvcmtmbG93RXhlY3V0",
            "aW9uUmVzcG9uc2Uid4LT5JMCcSIvL25hbWVzcGFjZXMve25hbWVzcGFjZX0v",
            "d29ya2Zsb3dzL3t3b3JrZmxvd19pZH06ASpaOyI2L2FwaS92MS9uYW1lc3Bh",
            "Y2VzL3tuYW1lc3BhY2V9L3dvcmtmbG93cy97d29ya2Zsb3dfaWR9OgEqEqUC",
            "ChVFeGVjdXRlTXVsdGlPcGVyYXRpb24SPS50ZW1wb3JhbC5hcGkud29ya2Zs",
            "b3dzZXJ2aWNlLnYxLkV4ZWN1dGVNdWx0aU9wZXJhdGlvblJlcXVlc3QaPi50",
            "ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkV4ZWN1dGVNdWx0aU9w",
            "ZXJhdGlvblJlc3BvbnNlIowBgtPkkwKFASI5L25hbWVzcGFjZXMve25hbWVz",
            "cGFjZX0vd29ya2Zsb3dzL2V4ZWN1dGUtbXVsdGktb3BlcmF0aW9uOgEqWkUi",
            "QC9hcGkvdjEvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS93b3JrZmxvd3MvZXhl",
            "Y3V0ZS1tdWx0aS1vcGVyYXRpb246ASoSwQIKG0dldFdvcmtmbG93RXhlY3V0",
            "aW9uSGlzdG9yeRJDLnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEu",
            "R2V0V29ya2Zsb3dFeGVjdXRpb25IaXN0b3J5UmVxdWVzdBpELnRlbXBvcmFs",
            "LmFwaS53b3JrZmxvd3NlcnZpY2UudjEuR2V0V29ya2Zsb3dFeGVjdXRpb25I",
            "aXN0b3J5UmVzcG9uc2UilgGC0+STAo8BEkEvbmFtZXNwYWNlcy97bmFtZXNw",
            "YWNlfS93b3JrZmxvd3Mve2V4ZWN1dGlvbi53b3JrZmxvd19pZH0vaGlzdG9y",
            "eVpKEkgvYXBpL3YxL25hbWVzcGFjZXMve25hbWVzcGFjZX0vd29ya2Zsb3dz",
            "L3tleGVjdXRpb24ud29ya2Zsb3dfaWR9L2hpc3RvcnkS5gIKIkdldFdvcmtm",
            "bG93RXhlY3V0aW9uSGlzdG9yeVJldmVyc2USSi50ZW1wb3JhbC5hcGkud29y",
            "a2Zsb3dzZXJ2aWNlLnYxLkdldFdvcmtmbG93RXhlY3V0aW9uSGlzdG9yeVJl",
            "dmVyc2VSZXF1ZXN0GksudGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52",
            "MS5HZXRXb3JrZmxvd0V4ZWN1dGlvbkhpc3RvcnlSZXZlcnNlUmVzcG9uc2Ui",
            "pgGC0+STAp8BEkkvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS93b3JrZmxvd3Mv",
            "e2V4ZWN1dGlvbi53b3JrZmxvd19pZH0vaGlzdG9yeS1yZXZlcnNlWlISUC9h",
            "cGkvdjEvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS93b3JrZmxvd3Mve2V4ZWN1",
            "dGlvbi53b3JrZmxvd19pZH0vaGlzdG9yeS1yZXZlcnNlEpgBChVQb2xsV29y",
            "a2Zsb3dUYXNrUXVldWUSPS50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNl",
            "LnYxLlBvbGxXb3JrZmxvd1Rhc2tRdWV1ZVJlcXVlc3QaPi50ZW1wb3JhbC5h",
            "cGkud29ya2Zsb3dzZXJ2aWNlLnYxLlBvbGxXb3JrZmxvd1Rhc2tRdWV1ZVJl",
            "c3BvbnNlIgASrQEKHFJlc3BvbmRXb3JrZmxvd1Rhc2tDb21wbGV0ZWQSRC50",
            "ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlJlc3BvbmRXb3JrZmxv",
            "d1Rhc2tDb21wbGV0ZWRSZXF1ZXN0GkUudGVtcG9yYWwuYXBpLndvcmtmbG93",
            "c2VydmljZS52MS5SZXNwb25kV29ya2Zsb3dUYXNrQ29tcGxldGVkUmVzcG9u",
            "c2UiABKkAQoZUmVzcG9uZFdvcmtmbG93VGFza0ZhaWxlZBJBLnRlbXBvcmFs",
            "LmFwaS53b3JrZmxvd3NlcnZpY2UudjEuUmVzcG9uZFdvcmtmbG93VGFza0Zh",
            "aWxlZFJlcXVlc3QaQi50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYx",
            "LlJlc3BvbmRXb3JrZmxvd1Rhc2tGYWlsZWRSZXNwb25zZSIAEpgBChVQb2xs",
            "QWN0aXZpdHlUYXNrUXVldWUSPS50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2",
            "aWNlLnYxLlBvbGxBY3Rpdml0eVRhc2tRdWV1ZVJlcXVlc3QaPi50ZW1wb3Jh",
            "bC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlBvbGxBY3Rpdml0eVRhc2tRdWV1",
            "ZVJlc3BvbnNlIgASmwIKG1JlY29yZEFjdGl2aXR5VGFza0hlYXJ0YmVhdBJD",
            "LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuUmVjb3JkQWN0aXZp",
            "dHlUYXNrSGVhcnRiZWF0UmVxdWVzdBpELnRlbXBvcmFsLmFwaS53b3JrZmxv",
            "d3NlcnZpY2UudjEuUmVjb3JkQWN0aXZpdHlUYXNrSGVhcnRiZWF0UmVzcG9u",
            "c2UicYLT5JMCayIsL25hbWVzcGFjZXMve25hbWVzcGFjZX0vYWN0aXZpdGll",
            "cy9oZWFydGJlYXQ6ASpaOCIzL2FwaS92MS9uYW1lc3BhY2VzL3tuYW1lc3Bh",
            "Y2V9L2FjdGl2aXRpZXMvaGVhcnRiZWF0OgEqErMCCh9SZWNvcmRBY3Rpdml0",
            "eVRhc2tIZWFydGJlYXRCeUlkEkcudGVtcG9yYWwuYXBpLndvcmtmbG93c2Vy",
            "dmljZS52MS5SZWNvcmRBY3Rpdml0eVRhc2tIZWFydGJlYXRCeUlkUmVxdWVz",
            "dBpILnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuUmVjb3JkQWN0",
            "aXZpdHlUYXNrSGVhcnRiZWF0QnlJZFJlc3BvbnNlIn2C0+STAnciMi9uYW1l",
            "c3BhY2VzL3tuYW1lc3BhY2V9L2FjdGl2aXRpZXMvaGVhcnRiZWF0LWJ5LWlk",
            "OgEqWj4iOS9hcGkvdjEvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS9hY3Rpdml0",
            "aWVzL2hlYXJ0YmVhdC1ieS1pZDoBKhKcAgocUmVzcG9uZEFjdGl2aXR5VGFz",
            "a0NvbXBsZXRlZBJELnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEu",
            "UmVzcG9uZEFjdGl2aXR5VGFza0NvbXBsZXRlZFJlcXVlc3QaRS50ZW1wb3Jh",
            "bC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlJlc3BvbmRBY3Rpdml0eVRhc2tD",
            "b21wbGV0ZWRSZXNwb25zZSJvgtPkkwJpIisvbmFtZXNwYWNlcy97bmFtZXNw",
            "YWNlfS9hY3Rpdml0aWVzL2NvbXBsZXRlOgEqWjciMi9hcGkvdjEvbmFtZXNw",
            "YWNlcy97bmFtZXNwYWNlfS9hY3Rpdml0aWVzL2NvbXBsZXRlOgEqErQCCiBS",
            "ZXNwb25kQWN0aXZpdHlUYXNrQ29tcGxldGVkQnlJZBJILnRlbXBvcmFsLmFw",
            "aS53b3JrZmxvd3NlcnZpY2UudjEuUmVzcG9uZEFjdGl2aXR5VGFza0NvbXBs",
            "ZXRlZEJ5SWRSZXF1ZXN0GkkudGVtcG9yYWwuYXBpLndvcmtmbG93c2Vydmlj",
            "ZS52MS5SZXNwb25kQWN0aXZpdHlUYXNrQ29tcGxldGVkQnlJZFJlc3BvbnNl",
            "InuC0+STAnUiMS9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L2FjdGl2aXRpZXMv",
            "Y29tcGxldGUtYnktaWQ6ASpaPSI4L2FwaS92MS9uYW1lc3BhY2VzL3tuYW1l",
            "c3BhY2V9L2FjdGl2aXRpZXMvY29tcGxldGUtYnktaWQ6ASoSiwIKGVJlc3Bv",
            "bmRBY3Rpdml0eVRhc2tGYWlsZWQSQS50ZW1wb3JhbC5hcGkud29ya2Zsb3dz",
            "ZXJ2aWNlLnYxLlJlc3BvbmRBY3Rpdml0eVRhc2tGYWlsZWRSZXF1ZXN0GkIu",
            "dGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5SZXNwb25kQWN0aXZp",
            "dHlUYXNrRmFpbGVkUmVzcG9uc2UiZ4LT5JMCYSInL25hbWVzcGFjZXMve25h",
            "bWVzcGFjZX0vYWN0aXZpdGllcy9mYWlsOgEqWjMiLi9hcGkvdjEvbmFtZXNw",
            "YWNlcy97bmFtZXNwYWNlfS9hY3Rpdml0aWVzL2ZhaWw6ASoSowIKHVJlc3Bv",
            "bmRBY3Rpdml0eVRhc2tGYWlsZWRCeUlkEkUudGVtcG9yYWwuYXBpLndvcmtm",
            "bG93c2VydmljZS52MS5SZXNwb25kQWN0aXZpdHlUYXNrRmFpbGVkQnlJZFJl",
            "cXVlc3QaRi50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlJlc3Bv",
            "bmRBY3Rpdml0eVRhc2tGYWlsZWRCeUlkUmVzcG9uc2Uic4LT5JMCbSItL25h",
            "bWVzcGFjZXMve25hbWVzcGFjZX0vYWN0aXZpdGllcy9mYWlsLWJ5LWlkOgEq",
            "WjkiNC9hcGkvdjEvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS9hY3Rpdml0aWVz",
            "L2ZhaWwtYnktaWQ6ASoSlQIKG1Jlc3BvbmRBY3Rpdml0eVRhc2tDYW5jZWxl",
            "ZBJDLnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuUmVzcG9uZEFj",
            "dGl2aXR5VGFza0NhbmNlbGVkUmVxdWVzdBpELnRlbXBvcmFsLmFwaS53b3Jr",
            "Zmxvd3NlcnZpY2UudjEuUmVzcG9uZEFjdGl2aXR5VGFza0NhbmNlbGVkUmVz",
            "cG9uc2Uia4LT5JMCZSIpL25hbWVzcGFjZXMve25hbWVzcGFjZX0vYWN0aXZp",
            "dGllcy9jYW5jZWw6ASpaNSIwL2FwaS92MS9uYW1lc3BhY2VzL3tuYW1lc3Bh",
            "Y2V9L2FjdGl2aXRpZXMvY2FuY2VsOgEqEq0CCh9SZXNwb25kQWN0aXZpdHlU",
            "YXNrQ2FuY2VsZWRCeUlkEkcudGVtcG9yYWwuYXBpLndvcmtmbG93c2Vydmlj",
            "ZS52MS5SZXNwb25kQWN0aXZpdHlUYXNrQ2FuY2VsZWRCeUlkUmVxdWVzdBpI",
            "LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuUmVzcG9uZEFjdGl2",
            "aXR5VGFza0NhbmNlbGVkQnlJZFJlc3BvbnNlIneC0+STAnEiLy9uYW1lc3Bh",
            "Y2VzL3tuYW1lc3BhY2V9L2FjdGl2aXRpZXMvY2FuY2VsLWJ5LWlkOgEqWjsi",
            "Ni9hcGkvdjEvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS9hY3Rpdml0aWVzL2Nh",
            "bmNlbC1ieS1pZDoBKhLgAgoeUmVxdWVzdENhbmNlbFdvcmtmbG93RXhlY3V0",
            "aW9uEkYudGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5SZXF1ZXN0",
            "Q2FuY2VsV29ya2Zsb3dFeGVjdXRpb25SZXF1ZXN0GkcudGVtcG9yYWwuYXBp",
            "LndvcmtmbG93c2VydmljZS52MS5SZXF1ZXN0Q2FuY2VsV29ya2Zsb3dFeGVj",
            "dXRpb25SZXNwb25zZSKsAYLT5JMCpQEiSS9uYW1lc3BhY2VzL3tuYW1lc3Bh",
            "Y2V9L3dvcmtmbG93cy97d29ya2Zsb3dfZXhlY3V0aW9uLndvcmtmbG93X2lk",
            "fS9jYW5jZWw6ASpaVSJQL2FwaS92MS9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9",
            "L3dvcmtmbG93cy97d29ya2Zsb3dfZXhlY3V0aW9uLndvcmtmbG93X2lkfS9j",
            "YW5jZWw6ASoS5wIKF1NpZ25hbFdvcmtmbG93RXhlY3V0aW9uEj8udGVtcG9y",
            "YWwuYXBpLndvcmtmbG93c2VydmljZS52MS5TaWduYWxXb3JrZmxvd0V4ZWN1",
            "dGlvblJlcXVlc3QaQC50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYx",
            "LlNpZ25hbFdvcmtmbG93RXhlY3V0aW9uUmVzcG9uc2UiyAGC0+STAsEBIlcv",
            "bmFtZXNwYWNlcy97bmFtZXNwYWNlfS93b3JrZmxvd3Mve3dvcmtmbG93X2V4",
            "ZWN1dGlvbi53b3JrZmxvd19pZH0vc2lnbmFsL3tzaWduYWxfbmFtZX06ASpa",
            "YyJeL2FwaS92MS9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L3dvcmtmbG93cy97",
            "d29ya2Zsb3dfZXhlY3V0aW9uLndvcmtmbG93X2lkfS9zaWduYWwve3NpZ25h",
            "bF9uYW1lfToBKhLyAgogU2lnbmFsV2l0aFN0YXJ0V29ya2Zsb3dFeGVjdXRp",
            "b24SSC50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlNpZ25hbFdp",
            "dGhTdGFydFdvcmtmbG93RXhlY3V0aW9uUmVxdWVzdBpJLnRlbXBvcmFsLmFw",
            "aS53b3JrZmxvd3NlcnZpY2UudjEuU2lnbmFsV2l0aFN0YXJ0V29ya2Zsb3dF",
            "eGVjdXRpb25SZXNwb25zZSK4AYLT5JMCsQEiTy9uYW1lc3BhY2VzL3tuYW1l",
            "c3BhY2V9L3dvcmtmbG93cy97d29ya2Zsb3dfaWR9L3NpZ25hbC13aXRoLXN0",
            "YXJ0L3tzaWduYWxfbmFtZX06ASpaWyJWL2FwaS92MS9uYW1lc3BhY2VzL3tu",
            "YW1lc3BhY2V9L3dvcmtmbG93cy97d29ya2Zsb3dfaWR9L3NpZ25hbC13aXRo",
            "LXN0YXJ0L3tzaWduYWxfbmFtZX06ASoSxgIKFlJlc2V0V29ya2Zsb3dFeGVj",
            "dXRpb24SPi50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlJlc2V0",
            "V29ya2Zsb3dFeGVjdXRpb25SZXF1ZXN0Gj8udGVtcG9yYWwuYXBpLndvcmtm",
            "bG93c2VydmljZS52MS5SZXNldFdvcmtmbG93RXhlY3V0aW9uUmVzcG9uc2Ui",
            "qgGC0+STAqMBIkgvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS93b3JrZmxvd3Mv",
            "e3dvcmtmbG93X2V4ZWN1dGlvbi53b3JrZmxvd19pZH0vcmVzZXQ6ASpaVCJP",
            "L2FwaS92MS9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L3dvcmtmbG93cy97d29y",
            "a2Zsb3dfZXhlY3V0aW9uLndvcmtmbG93X2lkfS9yZXNldDoBKhLaAgoaVGVy",
            "bWluYXRlV29ya2Zsb3dFeGVjdXRpb24SQi50ZW1wb3JhbC5hcGkud29ya2Zs",
            "b3dzZXJ2aWNlLnYxLlRlcm1pbmF0ZVdvcmtmbG93RXhlY3V0aW9uUmVxdWVz",
            "dBpDLnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuVGVybWluYXRl",
            "V29ya2Zsb3dFeGVjdXRpb25SZXNwb25zZSKyAYLT5JMCqwEiTC9uYW1lc3Bh",
            "Y2VzL3tuYW1lc3BhY2V9L3dvcmtmbG93cy97d29ya2Zsb3dfZXhlY3V0aW9u",
            "LndvcmtmbG93X2lkfS90ZXJtaW5hdGU6ASpaWCJTL2FwaS92MS9uYW1lc3Bh",
            "Y2VzL3tuYW1lc3BhY2V9L3dvcmtmbG93cy97d29ya2Zsb3dfZXhlY3V0aW9u",
            "LndvcmtmbG93X2lkfS90ZXJtaW5hdGU6ASoSngEKF0RlbGV0ZVdvcmtmbG93",
            "RXhlY3V0aW9uEj8udGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5E",
            "ZWxldGVXb3JrZmxvd0V4ZWN1dGlvblJlcXVlc3QaQC50ZW1wb3JhbC5hcGku",
            "d29ya2Zsb3dzZXJ2aWNlLnYxLkRlbGV0ZVdvcmtmbG93RXhlY3V0aW9uUmVz",
            "cG9uc2UiABKnAQoaTGlzdE9wZW5Xb3JrZmxvd0V4ZWN1dGlvbnMSQi50ZW1w",
            "b3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkxpc3RPcGVuV29ya2Zsb3dF",
            "eGVjdXRpb25zUmVxdWVzdBpDLnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZp",
            "Y2UudjEuTGlzdE9wZW5Xb3JrZmxvd0V4ZWN1dGlvbnNSZXNwb25zZSIAEq0B",
            "ChxMaXN0Q2xvc2VkV29ya2Zsb3dFeGVjdXRpb25zEkQudGVtcG9yYWwuYXBp",
            "LndvcmtmbG93c2VydmljZS52MS5MaXN0Q2xvc2VkV29ya2Zsb3dFeGVjdXRp",
            "b25zUmVxdWVzdBpFLnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEu",
            "TGlzdENsb3NlZFdvcmtmbG93RXhlY3V0aW9uc1Jlc3BvbnNlIgAS8AEKFkxp",
            "c3RXb3JrZmxvd0V4ZWN1dGlvbnMSPi50ZW1wb3JhbC5hcGkud29ya2Zsb3dz",
            "ZXJ2aWNlLnYxLkxpc3RXb3JrZmxvd0V4ZWN1dGlvbnNSZXF1ZXN0Gj8udGVt",
            "cG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5MaXN0V29ya2Zsb3dFeGVj",
            "dXRpb25zUmVzcG9uc2UiVYLT5JMCTxIhL25hbWVzcGFjZXMve25hbWVzcGFj",
            "ZX0vd29ya2Zsb3dzWioSKC9hcGkvdjEvbmFtZXNwYWNlcy97bmFtZXNwYWNl",
            "fS93b3JrZmxvd3MSmgIKHkxpc3RBcmNoaXZlZFdvcmtmbG93RXhlY3V0aW9u",
            "cxJGLnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuTGlzdEFyY2hp",
            "dmVkV29ya2Zsb3dFeGVjdXRpb25zUmVxdWVzdBpHLnRlbXBvcmFsLmFwaS53",
            "b3JrZmxvd3NlcnZpY2UudjEuTGlzdEFyY2hpdmVkV29ya2Zsb3dFeGVjdXRp",
            "b25zUmVzcG9uc2UiZ4LT5JMCYRIqL25hbWVzcGFjZXMve25hbWVzcGFjZX0v",
            "YXJjaGl2ZWQtd29ya2Zsb3dzWjMSMS9hcGkvdjEvbmFtZXNwYWNlcy97bmFt",
            "ZXNwYWNlfS9hcmNoaXZlZC13b3JrZmxvd3MSmwEKFlNjYW5Xb3JrZmxvd0V4",
            "ZWN1dGlvbnMSPi50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlNj",
            "YW5Xb3JrZmxvd0V4ZWN1dGlvbnNSZXF1ZXN0Gj8udGVtcG9yYWwuYXBpLndv",
            "cmtmbG93c2VydmljZS52MS5TY2FuV29ya2Zsb3dFeGVjdXRpb25zUmVzcG9u",
            "c2UiABL9AQoXQ291bnRXb3JrZmxvd0V4ZWN1dGlvbnMSPy50ZW1wb3JhbC5h",
            "cGkud29ya2Zsb3dzZXJ2aWNlLnYxLkNvdW50V29ya2Zsb3dFeGVjdXRpb25z",
            "UmVxdWVzdBpALnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuQ291",
            "bnRXb3JrZmxvd0V4ZWN1dGlvbnNSZXNwb25zZSJfgtPkkwJZEiYvbmFtZXNw",
            "YWNlcy97bmFtZXNwYWNlfS93b3JrZmxvdy1jb3VudFovEi0vYXBpL3YxL25h",
            "bWVzcGFjZXMve25hbWVzcGFjZX0vd29ya2Zsb3ctY291bnQSkgEKE0dldFNl",
            "YXJjaEF0dHJpYnV0ZXMSOy50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNl",
            "LnYxLkdldFNlYXJjaEF0dHJpYnV0ZXNSZXF1ZXN0GjwudGVtcG9yYWwuYXBp",
            "LndvcmtmbG93c2VydmljZS52MS5HZXRTZWFyY2hBdHRyaWJ1dGVzUmVzcG9u",
            "c2UiABKkAQoZUmVzcG9uZFF1ZXJ5VGFza0NvbXBsZXRlZBJBLnRlbXBvcmFs",
            "LmFwaS53b3JrZmxvd3NlcnZpY2UudjEuUmVzcG9uZFF1ZXJ5VGFza0NvbXBs",
            "ZXRlZFJlcXVlc3QaQi50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYx",
            "LlJlc3BvbmRRdWVyeVRhc2tDb21wbGV0ZWRSZXNwb25zZSIAEpUBChRSZXNl",
            "dFN0aWNreVRhc2tRdWV1ZRI8LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZp",
            "Y2UudjEuUmVzZXRTdGlja3lUYXNrUXVldWVSZXF1ZXN0Gj0udGVtcG9yYWwu",
            "YXBpLndvcmtmbG93c2VydmljZS52MS5SZXNldFN0aWNreVRhc2tRdWV1ZVJl",
            "c3BvbnNlIgASgwEKDlNodXRkb3duV29ya2VyEjYudGVtcG9yYWwuYXBpLndv",
            "cmtmbG93c2VydmljZS52MS5TaHV0ZG93bldvcmtlclJlcXVlc3QaNy50ZW1w",
            "b3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlNodXRkb3duV29ya2VyUmVz",
            "cG9uc2UiABK/AgoNUXVlcnlXb3JrZmxvdxI1LnRlbXBvcmFsLmFwaS53b3Jr",
            "Zmxvd3NlcnZpY2UudjEuUXVlcnlXb3JrZmxvd1JlcXVlc3QaNi50ZW1wb3Jh",
            "bC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlF1ZXJ5V29ya2Zsb3dSZXNwb25z",
            "ZSK+AYLT5JMCtwEiUi9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L3dvcmtmbG93",
            "cy97ZXhlY3V0aW9uLndvcmtmbG93X2lkfS9xdWVyeS97cXVlcnkucXVlcnlf",
            "dHlwZX06ASpaXiJZL2FwaS92MS9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L3dv",
            "cmtmbG93cy97ZXhlY3V0aW9uLndvcmtmbG93X2lkfS9xdWVyeS97cXVlcnku",
            "cXVlcnlfdHlwZX06ASoSqgIKGURlc2NyaWJlV29ya2Zsb3dFeGVjdXRpb24S",
            "QS50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkRlc2NyaWJlV29y",
            "a2Zsb3dFeGVjdXRpb25SZXF1ZXN0GkIudGVtcG9yYWwuYXBpLndvcmtmbG93",
            "c2VydmljZS52MS5EZXNjcmliZVdvcmtmbG93RXhlY3V0aW9uUmVzcG9uc2Ui",
            "hQGC0+STAn8SOS9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L3dvcmtmbG93cy97",
            "ZXhlY3V0aW9uLndvcmtmbG93X2lkfVpCEkAvYXBpL3YxL25hbWVzcGFjZXMv",
            "e25hbWVzcGFjZX0vd29ya2Zsb3dzL3tleGVjdXRpb24ud29ya2Zsb3dfaWR9",
            "EokCChFEZXNjcmliZVRhc2tRdWV1ZRI5LnRlbXBvcmFsLmFwaS53b3JrZmxv",
            "d3NlcnZpY2UudjEuRGVzY3JpYmVUYXNrUXVldWVSZXF1ZXN0GjoudGVtcG9y",
            "YWwuYXBpLndvcmtmbG93c2VydmljZS52MS5EZXNjcmliZVRhc2tRdWV1ZVJl",
            "c3BvbnNlIn2C0+STAncSNS9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L3Rhc2st",
            "cXVldWVzL3t0YXNrX3F1ZXVlLm5hbWV9Wj4SPC9hcGkvdjEvbmFtZXNwYWNl",
            "cy97bmFtZXNwYWNlfS90YXNrLXF1ZXVlcy97dGFza19xdWV1ZS5uYW1lfRKr",
            "AQoOR2V0Q2x1c3RlckluZm8SNi50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2",
            "aWNlLnYxLkdldENsdXN0ZXJJbmZvUmVxdWVzdBo3LnRlbXBvcmFsLmFwaS53",
            "b3JrZmxvd3NlcnZpY2UudjEuR2V0Q2x1c3RlckluZm9SZXNwb25zZSIogtPk",
            "kwIiEggvY2x1c3RlcloWEhQvYXBpL3YxL2NsdXN0ZXItaW5mbxKrAQoNR2V0",
            "U3lzdGVtSW5mbxI1LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEu",
            "R2V0U3lzdGVtSW5mb1JlcXVlc3QaNi50ZW1wb3JhbC5hcGkud29ya2Zsb3dz",
            "ZXJ2aWNlLnYxLkdldFN5c3RlbUluZm9SZXNwb25zZSIrgtPkkwIlEgwvc3lz",
            "dGVtLWluZm9aFRITL2FwaS92MS9zeXN0ZW0taW5mbxKeAQoXTGlzdFRhc2tR",
            "dWV1ZVBhcnRpdGlvbnMSPy50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNl",
            "LnYxLkxpc3RUYXNrUXVldWVQYXJ0aXRpb25zUmVxdWVzdBpALnRlbXBvcmFs",
            "LmFwaS53b3JrZmxvd3NlcnZpY2UudjEuTGlzdFRhc2tRdWV1ZVBhcnRpdGlv",
            "bnNSZXNwb25zZSIAEvoBCg5DcmVhdGVTY2hlZHVsZRI2LnRlbXBvcmFsLmFw",
            "aS53b3JrZmxvd3NlcnZpY2UudjEuQ3JlYXRlU2NoZWR1bGVSZXF1ZXN0Gjcu",
            "dGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5DcmVhdGVTY2hlZHVs",
            "ZVJlc3BvbnNlIneC0+STAnEiLy9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L3Nj",
            "aGVkdWxlcy97c2NoZWR1bGVfaWR9OgEqWjsiNi9hcGkvdjEvbmFtZXNwYWNl",
            "cy97bmFtZXNwYWNlfS9zY2hlZHVsZXMve3NjaGVkdWxlX2lkfToBKhL6AQoQ",
            "RGVzY3JpYmVTY2hlZHVsZRI4LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZp",
            "Y2UudjEuRGVzY3JpYmVTY2hlZHVsZVJlcXVlc3QaOS50ZW1wb3JhbC5hcGku",
            "d29ya2Zsb3dzZXJ2aWNlLnYxLkRlc2NyaWJlU2NoZWR1bGVSZXNwb25zZSJx",
            "gtPkkwJrEi8vbmFtZXNwYWNlcy97bmFtZXNwYWNlfS9zY2hlZHVsZXMve3Nj",
            "aGVkdWxlX2lkfVo4EjYvYXBpL3YxL25hbWVzcGFjZXMve25hbWVzcGFjZX0v",
            "c2NoZWR1bGVzL3tzY2hlZHVsZV9pZH0SiQIKDlVwZGF0ZVNjaGVkdWxlEjYu",
            "dGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5VcGRhdGVTY2hlZHVs",
            "ZVJlcXVlc3QaNy50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlVw",
            "ZGF0ZVNjaGVkdWxlUmVzcG9uc2UihQGC0+STAn8iNi9uYW1lc3BhY2VzL3tu",
            "YW1lc3BhY2V9L3NjaGVkdWxlcy97c2NoZWR1bGVfaWR9L3VwZGF0ZToBKlpC",
            "Ij0vYXBpL3YxL25hbWVzcGFjZXMve25hbWVzcGFjZX0vc2NoZWR1bGVzL3tz",
            "Y2hlZHVsZV9pZH0vdXBkYXRlOgEqEoQCCg1QYXRjaFNjaGVkdWxlEjUudGVt",
            "cG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5QYXRjaFNjaGVkdWxlUmVx",
            "dWVzdBo2LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuUGF0Y2hT",
            "Y2hlZHVsZVJlc3BvbnNlIoMBgtPkkwJ9IjUvbmFtZXNwYWNlcy97bmFtZXNw",
            "YWNlfS9zY2hlZHVsZXMve3NjaGVkdWxlX2lkfS9wYXRjaDoBKlpBIjwvYXBp",
            "L3YxL25hbWVzcGFjZXMve25hbWVzcGFjZX0vc2NoZWR1bGVzL3tzY2hlZHVs",
            "ZV9pZH0vcGF0Y2g6ASoStQIKGUxpc3RTY2hlZHVsZU1hdGNoaW5nVGltZXMS",
            "QS50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkxpc3RTY2hlZHVs",
            "ZU1hdGNoaW5nVGltZXNSZXF1ZXN0GkIudGVtcG9yYWwuYXBpLndvcmtmbG93",
            "c2VydmljZS52MS5MaXN0U2NoZWR1bGVNYXRjaGluZ1RpbWVzUmVzcG9uc2Ui",
            "kAGC0+STAokBEj4vbmFtZXNwYWNlcy97bmFtZXNwYWNlfS9zY2hlZHVsZXMv",
            "e3NjaGVkdWxlX2lkfS9tYXRjaGluZy10aW1lc1pHEkUvYXBpL3YxL25hbWVz",
            "cGFjZXMve25hbWVzcGFjZX0vc2NoZWR1bGVzL3tzY2hlZHVsZV9pZH0vbWF0",
            "Y2hpbmctdGltZXMS9AEKDkRlbGV0ZVNjaGVkdWxlEjYudGVtcG9yYWwuYXBp",
            "LndvcmtmbG93c2VydmljZS52MS5EZWxldGVTY2hlZHVsZVJlcXVlc3QaNy50",
            "ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkRlbGV0ZVNjaGVkdWxl",
            "UmVzcG9uc2UicYLT5JMCayovL25hbWVzcGFjZXMve25hbWVzcGFjZX0vc2No",
            "ZWR1bGVzL3tzY2hlZHVsZV9pZH1aOCo2L2FwaS92MS9uYW1lc3BhY2VzL3tu",
            "YW1lc3BhY2V9L3NjaGVkdWxlcy97c2NoZWR1bGVfaWR9EtUBCg1MaXN0U2No",
            "ZWR1bGVzEjUudGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5MaXN0",
            "U2NoZWR1bGVzUmVxdWVzdBo2LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZp",
            "Y2UudjEuTGlzdFNjaGVkdWxlc1Jlc3BvbnNlIlWC0+STAk8SIS9uYW1lc3Bh",
            "Y2VzL3tuYW1lc3BhY2V9L3NjaGVkdWxlc1oqEigvYXBpL3YxL25hbWVzcGFj",
            "ZXMve25hbWVzcGFjZX0vc2NoZWR1bGVzErkBCiBVcGRhdGVXb3JrZXJCdWls",
            "ZElkQ29tcGF0aWJpbGl0eRJILnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZp",
            "Y2UudjEuVXBkYXRlV29ya2VyQnVpbGRJZENvbXBhdGliaWxpdHlSZXF1ZXN0",
            "GkkudGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5VcGRhdGVXb3Jr",
            "ZXJCdWlsZElkQ29tcGF0aWJpbGl0eVJlc3BvbnNlIgAS4QIKHUdldFdvcmtl",
            "ckJ1aWxkSWRDb21wYXRpYmlsaXR5EkUudGVtcG9yYWwuYXBpLndvcmtmbG93",
            "c2VydmljZS52MS5HZXRXb3JrZXJCdWlsZElkQ29tcGF0aWJpbGl0eVJlcXVl",
            "c3QaRi50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkdldFdvcmtl",
            "ckJ1aWxkSWRDb21wYXRpYmlsaXR5UmVzcG9uc2UisAGC0+STAqkBEk4vbmFt",
            "ZXNwYWNlcy97bmFtZXNwYWNlfS90YXNrLXF1ZXVlcy97dGFza19xdWV1ZX0v",
            "d29ya2VyLWJ1aWxkLWlkLWNvbXBhdGliaWxpdHlaVxJVL2FwaS92MS9uYW1l",
            "c3BhY2VzL3tuYW1lc3BhY2V9L3Rhc2stcXVldWVzL3t0YXNrX3F1ZXVlfS93",
            "b3JrZXItYnVpbGQtaWQtY29tcGF0aWJpbGl0eRKqAQobVXBkYXRlV29ya2Vy",
            "VmVyc2lvbmluZ1J1bGVzEkMudGVtcG9yYWwuYXBpLndvcmtmbG93c2Vydmlj",
            "ZS52MS5VcGRhdGVXb3JrZXJWZXJzaW9uaW5nUnVsZXNSZXF1ZXN0GkQudGVt",
            "cG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5VcGRhdGVXb3JrZXJWZXJz",
            "aW9uaW5nUnVsZXNSZXNwb25zZSIAEsYCChhHZXRXb3JrZXJWZXJzaW9uaW5n",
            "UnVsZXMSQC50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkdldFdv",
            "cmtlclZlcnNpb25pbmdSdWxlc1JlcXVlc3QaQS50ZW1wb3JhbC5hcGkud29y",
            "a2Zsb3dzZXJ2aWNlLnYxLkdldFdvcmtlclZlcnNpb25pbmdSdWxlc1Jlc3Bv",
            "bnNlIqQBgtPkkwKdARJIL25hbWVzcGFjZXMve25hbWVzcGFjZX0vdGFzay1x",
            "dWV1ZXMve3Rhc2tfcXVldWV9L3dvcmtlci12ZXJzaW9uaW5nLXJ1bGVzWlES",
            "Ty9hcGkvdjEvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS90YXNrLXF1ZXVlcy97",
            "dGFza19xdWV1ZX0vd29ya2VyLXZlcnNpb25pbmctcnVsZXMSlwIKGUdldFdv",
            "cmtlclRhc2tSZWFjaGFiaWxpdHkSQS50ZW1wb3JhbC5hcGkud29ya2Zsb3dz",
            "ZXJ2aWNlLnYxLkdldFdvcmtlclRhc2tSZWFjaGFiaWxpdHlSZXF1ZXN0GkIu",
            "dGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5HZXRXb3JrZXJUYXNr",
            "UmVhY2hhYmlsaXR5UmVzcG9uc2Uic4LT5JMCbRIwL25hbWVzcGFjZXMve25h",
            "bWVzcGFjZX0vd29ya2VyLXRhc2stcmVhY2hhYmlsaXR5WjkSNy9hcGkvdjEv",
            "bmFtZXNwYWNlcy97bmFtZXNwYWNlfS93b3JrZXItdGFzay1yZWFjaGFiaWxp",
            "dHkSyAIKEkRlc2NyaWJlRGVwbG95bWVudBI6LnRlbXBvcmFsLmFwaS53b3Jr",
            "Zmxvd3NlcnZpY2UudjEuRGVzY3JpYmVEZXBsb3ltZW50UmVxdWVzdBo7LnRl",
            "bXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuRGVzY3JpYmVEZXBsb3lt",
            "ZW50UmVzcG9uc2UiuAGC0+STArEBElIvbmFtZXNwYWNlcy97bmFtZXNwYWNl",
            "fS9kZXBsb3ltZW50cy97ZGVwbG95bWVudC5zZXJpZXNfbmFtZX0ve2RlcGxv",
            "eW1lbnQuYnVpbGRfaWR9WlsSWS9hcGkvdjEvbmFtZXNwYWNlcy97bmFtZXNw",
            "YWNlfS9kZXBsb3ltZW50cy97ZGVwbG95bWVudC5zZXJpZXNfbmFtZX0ve2Rl",
            "cGxveW1lbnQuYnVpbGRfaWR9EsMCCh9EZXNjcmliZVdvcmtlckRlcGxveW1l",
            "bnRWZXJzaW9uEkcudGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5E",
            "ZXNjcmliZVdvcmtlckRlcGxveW1lbnRWZXJzaW9uUmVxdWVzdBpILnRlbXBv",
            "cmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuRGVzY3JpYmVXb3JrZXJEZXBs",
            "b3ltZW50VmVyc2lvblJlc3BvbnNlIowBgtPkkwKFARI8L25hbWVzcGFjZXMv",
            "e25hbWVzcGFjZX0vd29ya2VyLWRlcGxveW1lbnQtdmVyc2lvbnMve3ZlcnNp",
            "b259WkUSQy9hcGkvdjEvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS93b3JrZXIt",
            "ZGVwbG95bWVudC12ZXJzaW9ucy97dmVyc2lvbn0S3wEKD0xpc3REZXBsb3lt",
            "ZW50cxI3LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuTGlzdERl",
            "cGxveW1lbnRzUmVxdWVzdBo4LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZp",
            "Y2UudjEuTGlzdERlcGxveW1lbnRzUmVzcG9uc2UiWYLT5JMCUxIjL25hbWVz",
            "cGFjZXMve25hbWVzcGFjZX0vZGVwbG95bWVudHNaLBIqL2FwaS92MS9uYW1l",
            "c3BhY2VzL3tuYW1lc3BhY2V9L2RlcGxveW1lbnRzEvcCChlHZXREZXBsb3lt",
            "ZW50UmVhY2hhYmlsaXR5EkEudGVtcG9yYWwuYXBpLndvcmtmbG93c2Vydmlj",
            "ZS52MS5HZXREZXBsb3ltZW50UmVhY2hhYmlsaXR5UmVxdWVzdBpCLnRlbXBv",
            "cmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuR2V0RGVwbG95bWVudFJlYWNo",
            "YWJpbGl0eVJlc3BvbnNlItIBgtPkkwLLARJfL25hbWVzcGFjZXMve25hbWVz",
            "cGFjZX0vZGVwbG95bWVudHMve2RlcGxveW1lbnQuc2VyaWVzX25hbWV9L3tk",
            "ZXBsb3ltZW50LmJ1aWxkX2lkfS9yZWFjaGFiaWxpdHlaaBJmL2FwaS92MS9u",
            "YW1lc3BhY2VzL3tuYW1lc3BhY2V9L2RlcGxveW1lbnRzL3tkZXBsb3ltZW50",
            "LnNlcmllc19uYW1lfS97ZGVwbG95bWVudC5idWlsZF9pZH0vcmVhY2hhYmls",
            "aXR5EpkCChRHZXRDdXJyZW50RGVwbG95bWVudBI8LnRlbXBvcmFsLmFwaS53",
            "b3JrZmxvd3NlcnZpY2UudjEuR2V0Q3VycmVudERlcGxveW1lbnRSZXF1ZXN0",
            "Gj0udGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5HZXRDdXJyZW50",
            "RGVwbG95bWVudFJlc3BvbnNlIoMBgtPkkwJ9EjgvbmFtZXNwYWNlcy97bmFt",
            "ZXNwYWNlfS9jdXJyZW50LWRlcGxveW1lbnQve3Nlcmllc19uYW1lfVpBEj8v",
            "YXBpL3YxL25hbWVzcGFjZXMve25hbWVzcGFjZX0vY3VycmVudC1kZXBsb3lt",
            "ZW50L3tzZXJpZXNfbmFtZX0StgIKFFNldEN1cnJlbnREZXBsb3ltZW50Ejwu",
            "dGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5TZXRDdXJyZW50RGVw",
            "bG95bWVudFJlcXVlc3QaPS50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNl",
            "LnYxLlNldEN1cnJlbnREZXBsb3ltZW50UmVzcG9uc2UioAGC0+STApkBIkMv",
            "bmFtZXNwYWNlcy97bmFtZXNwYWNlfS9jdXJyZW50LWRlcGxveW1lbnQve2Rl",
            "cGxveW1lbnQuc2VyaWVzX25hbWV9OgEqWk8iSi9hcGkvdjEvbmFtZXNwYWNl",
            "cy97bmFtZXNwYWNlfS9jdXJyZW50LWRlcGxveW1lbnQve2RlcGxveW1lbnQu",
            "c2VyaWVzX25hbWV9OgEqEvcCCiFTZXRXb3JrZXJEZXBsb3ltZW50Q3VycmVu",
            "dFZlcnNpb24SSS50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlNl",
            "dFdvcmtlckRlcGxveW1lbnRDdXJyZW50VmVyc2lvblJlcXVlc3QaSi50ZW1w",
            "b3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlNldFdvcmtlckRlcGxveW1l",
            "bnRDdXJyZW50VmVyc2lvblJlc3BvbnNlIroBgtPkkwKzASJQL25hbWVzcGFj",
            "ZXMve25hbWVzcGFjZX0vd29ya2VyLWRlcGxveW1lbnRzL3tkZXBsb3ltZW50",
            "X25hbWV9L3NldC1jdXJyZW50LXZlcnNpb246ASpaXCJXL2FwaS92MS9uYW1l",
            "c3BhY2VzL3tuYW1lc3BhY2V9L3dvcmtlci1kZXBsb3ltZW50cy97ZGVwbG95",
            "bWVudF9uYW1lfS9zZXQtY3VycmVudC12ZXJzaW9uOgEqEq4CChhEZXNjcmli",
            "ZVdvcmtlckRlcGxveW1lbnQSQC50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2",
            "aWNlLnYxLkRlc2NyaWJlV29ya2VyRGVwbG95bWVudFJlcXVlc3QaQS50ZW1w",
            "b3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkRlc2NyaWJlV29ya2VyRGVw",
            "bG95bWVudFJlc3BvbnNlIowBgtPkkwKFARI8L25hbWVzcGFjZXMve25hbWVz",
            "cGFjZX0vd29ya2VyLWRlcGxveW1lbnRzL3tkZXBsb3ltZW50X25hbWV9WkUS",
            "Qy9hcGkvdjEvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS93b3JrZXItZGVwbG95",
            "bWVudHMve2RlcGxveW1lbnRfbmFtZX0SqAIKFkRlbGV0ZVdvcmtlckRlcGxv",
            "eW1lbnQSPi50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkRlbGV0",
            "ZVdvcmtlckRlcGxveW1lbnRSZXF1ZXN0Gj8udGVtcG9yYWwuYXBpLndvcmtm",
            "bG93c2VydmljZS52MS5EZWxldGVXb3JrZXJEZXBsb3ltZW50UmVzcG9uc2Ui",
            "jAGC0+STAoUBKjwvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS93b3JrZXItZGVw",
            "bG95bWVudHMve2RlcGxveW1lbnRfbmFtZX1aRSpDL2FwaS92MS9uYW1lc3Bh",
            "Y2VzL3tuYW1lc3BhY2V9L3dvcmtlci1kZXBsb3ltZW50cy97ZGVwbG95bWVu",
            "dF9uYW1lfRK9AgodRGVsZXRlV29ya2VyRGVwbG95bWVudFZlcnNpb24SRS50",
            "ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLkRlbGV0ZVdvcmtlckRl",
            "cGxveW1lbnRWZXJzaW9uUmVxdWVzdBpGLnRlbXBvcmFsLmFwaS53b3JrZmxv",
            "d3NlcnZpY2UudjEuRGVsZXRlV29ya2VyRGVwbG95bWVudFZlcnNpb25SZXNw",
            "b25zZSKMAYLT5JMChQEqPC9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L3dvcmtl",
            "ci1kZXBsb3ltZW50LXZlcnNpb25zL3t2ZXJzaW9ufVpFKkMvYXBpL3YxL25h",
            "bWVzcGFjZXMve25hbWVzcGFjZX0vd29ya2VyLWRlcGxveW1lbnQtdmVyc2lv",
            "bnMve3ZlcnNpb259EvcCCiFTZXRXb3JrZXJEZXBsb3ltZW50UmFtcGluZ1Zl",
            "cnNpb24SSS50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlNldFdv",
            "cmtlckRlcGxveW1lbnRSYW1waW5nVmVyc2lvblJlcXVlc3QaSi50ZW1wb3Jh",
            "bC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlNldFdvcmtlckRlcGxveW1lbnRS",
            "YW1waW5nVmVyc2lvblJlc3BvbnNlIroBgtPkkwKzASJQL25hbWVzcGFjZXMv",
            "e25hbWVzcGFjZX0vd29ya2VyLWRlcGxveW1lbnRzL3tkZXBsb3ltZW50X25h",
            "bWV9L3NldC1yYW1waW5nLXZlcnNpb246ASpaXCJXL2FwaS92MS9uYW1lc3Bh",
            "Y2VzL3tuYW1lc3BhY2V9L3dvcmtlci1kZXBsb3ltZW50cy97ZGVwbG95bWVu",
            "dF9uYW1lfS9zZXQtcmFtcGluZy12ZXJzaW9uOgEqEv8BChVMaXN0V29ya2Vy",
            "RGVwbG95bWVudHMSPS50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYx",
            "Lkxpc3RXb3JrZXJEZXBsb3ltZW50c1JlcXVlc3QaPi50ZW1wb3JhbC5hcGku",
            "d29ya2Zsb3dzZXJ2aWNlLnYxLkxpc3RXb3JrZXJEZXBsb3ltZW50c1Jlc3Bv",
            "bnNlImeC0+STAmESKi9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L3dvcmtlci1k",
            "ZXBsb3ltZW50c1ozEjEvYXBpL3YxL25hbWVzcGFjZXMve25hbWVzcGFjZX0v",
            "d29ya2VyLWRlcGxveW1lbnRzEvsCCiVVcGRhdGVXb3JrZXJEZXBsb3ltZW50",
            "VmVyc2lvbk1ldGFkYXRhEk0udGVtcG9yYWwuYXBpLndvcmtmbG93c2Vydmlj",
            "ZS52MS5VcGRhdGVXb3JrZXJEZXBsb3ltZW50VmVyc2lvbk1ldGFkYXRhUmVx",
            "dWVzdBpOLnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuVXBkYXRl",
            "V29ya2VyRGVwbG95bWVudFZlcnNpb25NZXRhZGF0YVJlc3BvbnNlIrIBgtPk",
            "kwKrASJML25hbWVzcGFjZXMve25hbWVzcGFjZX0vd29ya2VyLWRlcGxveW1l",
            "bnQtdmVyc2lvbnMve3ZlcnNpb259L3VwZGF0ZS1tZXRhZGF0YToBKlpYIlMv",
            "YXBpL3YxL25hbWVzcGFjZXMve25hbWVzcGFjZX0vd29ya2VyLWRlcGxveW1l",
            "bnQtdmVyc2lvbnMve3ZlcnNpb259L3VwZGF0ZS1tZXRhZGF0YToBKhL1AgoX",
            "VXBkYXRlV29ya2Zsb3dFeGVjdXRpb24SPy50ZW1wb3JhbC5hcGkud29ya2Zs",
            "b3dzZXJ2aWNlLnYxLlVwZGF0ZVdvcmtmbG93RXhlY3V0aW9uUmVxdWVzdBpA",
            "LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuVXBkYXRlV29ya2Zs",
            "b3dFeGVjdXRpb25SZXNwb25zZSLWAYLT5JMCzwEiXi9uYW1lc3BhY2VzL3tu",
            "YW1lc3BhY2V9L3dvcmtmbG93cy97d29ya2Zsb3dfZXhlY3V0aW9uLndvcmtm",
            "bG93X2lkfS91cGRhdGUve3JlcXVlc3QuaW5wdXQubmFtZX06ASpaaiJlL2Fw",
            "aS92MS9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L3dvcmtmbG93cy97d29ya2Zs",
            "b3dfZXhlY3V0aW9uLndvcmtmbG93X2lkfS91cGRhdGUve3JlcXVlc3QuaW5w",
            "dXQubmFtZX06ASoSqgEKG1BvbGxXb3JrZmxvd0V4ZWN1dGlvblVwZGF0ZRJD",
            "LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuUG9sbFdvcmtmbG93",
            "RXhlY3V0aW9uVXBkYXRlUmVxdWVzdBpELnRlbXBvcmFsLmFwaS53b3JrZmxv",
            "d3NlcnZpY2UudjEuUG9sbFdvcmtmbG93RXhlY3V0aW9uVXBkYXRlUmVzcG9u",
            "c2UiABKNAgoTU3RhcnRCYXRjaE9wZXJhdGlvbhI7LnRlbXBvcmFsLmFwaS53",
            "b3JrZmxvd3NlcnZpY2UudjEuU3RhcnRCYXRjaE9wZXJhdGlvblJlcXVlc3Qa",
            "PC50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlN0YXJ0QmF0Y2hP",
            "cGVyYXRpb25SZXNwb25zZSJ7gtPkkwJ1IjEvbmFtZXNwYWNlcy97bmFtZXNw",
            "YWNlfS9iYXRjaC1vcGVyYXRpb25zL3tqb2JfaWR9OgEqWj0iOC9hcGkvdjEv",
            "bmFtZXNwYWNlcy97bmFtZXNwYWNlfS9iYXRjaC1vcGVyYXRpb25zL3tqb2Jf",
            "aWR9OgEqEpUCChJTdG9wQmF0Y2hPcGVyYXRpb24SOi50ZW1wb3JhbC5hcGku",
            "d29ya2Zsb3dzZXJ2aWNlLnYxLlN0b3BCYXRjaE9wZXJhdGlvblJlcXVlc3Qa",
            "Oy50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlN0b3BCYXRjaE9w",
            "ZXJhdGlvblJlc3BvbnNlIoUBgtPkkwJ/IjYvbmFtZXNwYWNlcy97bmFtZXNw",
            "YWNlfS9iYXRjaC1vcGVyYXRpb25zL3tqb2JfaWR9L3N0b3A6ASpaQiI9L2Fw",
            "aS92MS9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L2JhdGNoLW9wZXJhdGlvbnMv",
            "e2pvYl9pZH0vc3RvcDoBKhKQAgoWRGVzY3JpYmVCYXRjaE9wZXJhdGlvbhI+",
            "LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuRGVzY3JpYmVCYXRj",
            "aE9wZXJhdGlvblJlcXVlc3QaPy50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2",
            "aWNlLnYxLkRlc2NyaWJlQmF0Y2hPcGVyYXRpb25SZXNwb25zZSJ1gtPkkwJv",
            "EjEvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS9iYXRjaC1vcGVyYXRpb25zL3tq",
            "b2JfaWR9WjoSOC9hcGkvdjEvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS9iYXRj",
            "aC1vcGVyYXRpb25zL3tqb2JfaWR9EvUBChNMaXN0QmF0Y2hPcGVyYXRpb25z",
            "EjsudGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5MaXN0QmF0Y2hP",
            "cGVyYXRpb25zUmVxdWVzdBo8LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZp",
            "Y2UudjEuTGlzdEJhdGNoT3BlcmF0aW9uc1Jlc3BvbnNlImOC0+STAl0SKC9u",
            "YW1lc3BhY2VzL3tuYW1lc3BhY2V9L2JhdGNoLW9wZXJhdGlvbnNaMRIvL2Fw",
            "aS92MS9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L2JhdGNoLW9wZXJhdGlvbnMS",
            "jwEKElBvbGxOZXh1c1Rhc2tRdWV1ZRI6LnRlbXBvcmFsLmFwaS53b3JrZmxv",
            "d3NlcnZpY2UudjEuUG9sbE5leHVzVGFza1F1ZXVlUmVxdWVzdBo7LnRlbXBv",
            "cmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuUG9sbE5leHVzVGFza1F1ZXVl",
            "UmVzcG9uc2UiABKkAQoZUmVzcG9uZE5leHVzVGFza0NvbXBsZXRlZBJBLnRl",
            "bXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuUmVzcG9uZE5leHVzVGFz",
            "a0NvbXBsZXRlZFJlcXVlc3QaQi50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2",
            "aWNlLnYxLlJlc3BvbmROZXh1c1Rhc2tDb21wbGV0ZWRSZXNwb25zZSIAEpsB",
            "ChZSZXNwb25kTmV4dXNUYXNrRmFpbGVkEj4udGVtcG9yYWwuYXBpLndvcmtm",
            "bG93c2VydmljZS52MS5SZXNwb25kTmV4dXNUYXNrRmFpbGVkUmVxdWVzdBo/",
            "LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuUmVzcG9uZE5leHVz",
            "VGFza0ZhaWxlZFJlc3BvbnNlIgASkwIKFVVwZGF0ZUFjdGl2aXR5T3B0aW9u",
            "cxI9LnRlbXBvcmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuVXBkYXRlQWN0",
            "aXZpdHlPcHRpb25zUmVxdWVzdBo+LnRlbXBvcmFsLmFwaS53b3JrZmxvd3Nl",
            "cnZpY2UudjEuVXBkYXRlQWN0aXZpdHlPcHRpb25zUmVzcG9uc2Uie4LT5JMC",
            "dSIxL25hbWVzcGFjZXMve25hbWVzcGFjZX0vYWN0aXZpdGllcy91cGRhdGUt",
            "b3B0aW9uczoBKlo9IjgvYXBpL3YxL25hbWVzcGFjZXMve25hbWVzcGFjZX0v",
            "YWN0aXZpdGllcy91cGRhdGUtb3B0aW9uczoBKhLwAgoeVXBkYXRlV29ya2Zs",
            "b3dFeGVjdXRpb25PcHRpb25zEkYudGVtcG9yYWwuYXBpLndvcmtmbG93c2Vy",
            "dmljZS52MS5VcGRhdGVXb3JrZmxvd0V4ZWN1dGlvbk9wdGlvbnNSZXF1ZXN0",
            "GkcudGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5VcGRhdGVXb3Jr",
            "Zmxvd0V4ZWN1dGlvbk9wdGlvbnNSZXNwb25zZSK8AYLT5JMCtQEiUS9uYW1l",
            "c3BhY2VzL3tuYW1lc3BhY2V9L3dvcmtmbG93cy97d29ya2Zsb3dfZXhlY3V0",
            "aW9uLndvcmtmbG93X2lkfS91cGRhdGUtb3B0aW9uczoBKlpdIlgvYXBpL3Yx",
            "L25hbWVzcGFjZXMve25hbWVzcGFjZX0vd29ya2Zsb3dzL3t3b3JrZmxvd19l",
            "eGVjdXRpb24ud29ya2Zsb3dfaWR9L3VwZGF0ZS1vcHRpb25zOgEqEukBCg1Q",
            "YXVzZUFjdGl2aXR5EjUudGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52",
            "MS5QYXVzZUFjdGl2aXR5UmVxdWVzdBo2LnRlbXBvcmFsLmFwaS53b3JrZmxv",
            "d3NlcnZpY2UudjEuUGF1c2VBY3Rpdml0eVJlc3BvbnNlImmC0+STAmMiKC9u",
            "YW1lc3BhY2VzL3tuYW1lc3BhY2V9L2FjdGl2aXRpZXMvcGF1c2U6ASpaNCIv",
            "L2FwaS92MS9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L2FjdGl2aXRpZXMvcGF1",
            "c2U6ASoS8wEKD1VucGF1c2VBY3Rpdml0eRI3LnRlbXBvcmFsLmFwaS53b3Jr",
            "Zmxvd3NlcnZpY2UudjEuVW5wYXVzZUFjdGl2aXR5UmVxdWVzdBo4LnRlbXBv",
            "cmFsLmFwaS53b3JrZmxvd3NlcnZpY2UudjEuVW5wYXVzZUFjdGl2aXR5UmVz",
            "cG9uc2UibYLT5JMCZyIqL25hbWVzcGFjZXMve25hbWVzcGFjZX0vYWN0aXZp",
            "dGllcy91bnBhdXNlOgEqWjYiMS9hcGkvdjEvbmFtZXNwYWNlcy97bmFtZXNw",
            "YWNlfS9hY3Rpdml0aWVzL3VucGF1c2U6ASoS6QEKDVJlc2V0QWN0aXZpdHkS",
            "NS50ZW1wb3JhbC5hcGkud29ya2Zsb3dzZXJ2aWNlLnYxLlJlc2V0QWN0aXZp",
            "dHlSZXF1ZXN0GjYudGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MS5S",
            "ZXNldEFjdGl2aXR5UmVzcG9uc2UiaYLT5JMCYyIoL25hbWVzcGFjZXMve25h",
            "bWVzcGFjZX0vYWN0aXZpdGllcy9yZXNldDoBKlo0Ii8vYXBpL3YxL25hbWVz",
            "cGFjZXMve25hbWVzcGFjZX0vYWN0aXZpdGllcy9yZXNldDoBKkK2AQoiaW8u",
            "dGVtcG9yYWwuYXBpLndvcmtmbG93c2VydmljZS52MUIMU2VydmljZVByb3Rv",
            "UAFaNWdvLnRlbXBvcmFsLmlvL2FwaS93b3JrZmxvd3NlcnZpY2UvdjE7d29y",
            "a2Zsb3dzZXJ2aWNlqgIhVGVtcG9yYWxpby5BcGkuV29ya2Zsb3dTZXJ2aWNl",
            "LlYx6gIkVGVtcG9yYWxpbzo6QXBpOjpXb3JrZmxvd1NlcnZpY2U6OlYxYgZw",
            "cm90bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Temporalio.Api.WorkflowService.V1.RequestResponseReflection.Descriptor, global::Temporalio.Api.Dependencies.Google.Api.AnnotationsReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, null));
    }
    #endregion

  }
}

#endregion Designer generated code
