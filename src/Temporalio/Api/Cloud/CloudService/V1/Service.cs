// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: temporal/api/cloud/cloudservice/v1/service.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021, 8981
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Temporalio.Api.Cloud.CloudService.V1 {

  /// <summary>Holder for reflection information generated from temporal/api/cloud/cloudservice/v1/service.proto</summary>
  public static partial class ServiceReflection {

    #region Descriptor
    /// <summary>File descriptor for temporal/api/cloud/cloudservice/v1/service.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static ServiceReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CjB0ZW1wb3JhbC9hcGkvY2xvdWQvY2xvdWRzZXJ2aWNlL3YxL3NlcnZpY2Uu",
            "cHJvdG8SInRlbXBvcmFsLmFwaS5jbG91ZC5jbG91ZHNlcnZpY2UudjEaOXRl",
            "bXBvcmFsL2FwaS9jbG91ZC9jbG91ZHNlcnZpY2UvdjEvcmVxdWVzdF9yZXNw",
            "b25zZS5wcm90bxocZ29vZ2xlL2FwaS9hbm5vdGF0aW9ucy5wcm90bzLOLgoM",
            "Q2xvdWRTZXJ2aWNlEosBCghHZXRVc2VycxIzLnRlbXBvcmFsLmFwaS5jbG91",
            "ZC5jbG91ZHNlcnZpY2UudjEuR2V0VXNlcnNSZXF1ZXN0GjQudGVtcG9yYWwu",
            "YXBpLmNsb3VkLmNsb3Vkc2VydmljZS52MS5HZXRVc2Vyc1Jlc3BvbnNlIhSC",
            "0+STAg4SDC9jbG91ZC91c2VycxKSAQoHR2V0VXNlchIyLnRlbXBvcmFsLmFw",
            "aS5jbG91ZC5jbG91ZHNlcnZpY2UudjEuR2V0VXNlclJlcXVlc3QaMy50ZW1w",
            "b3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNlLnYxLkdldFVzZXJSZXNwb25z",
            "ZSIegtPkkwIYEhYvY2xvdWQvdXNlcnMve3VzZXJfaWR9EpQBCgpDcmVhdGVV",
            "c2VyEjUudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2VydmljZS52MS5DcmVh",
            "dGVVc2VyUmVxdWVzdBo2LnRlbXBvcmFsLmFwaS5jbG91ZC5jbG91ZHNlcnZp",
            "Y2UudjEuQ3JlYXRlVXNlclJlc3BvbnNlIheC0+STAhEiDC9jbG91ZC91c2Vy",
            "czoBKhKeAQoKVXBkYXRlVXNlchI1LnRlbXBvcmFsLmFwaS5jbG91ZC5jbG91",
            "ZHNlcnZpY2UudjEuVXBkYXRlVXNlclJlcXVlc3QaNi50ZW1wb3JhbC5hcGku",
            "Y2xvdWQuY2xvdWRzZXJ2aWNlLnYxLlVwZGF0ZVVzZXJSZXNwb25zZSIhgtPk",
            "kwIbIhYvY2xvdWQvdXNlcnMve3VzZXJfaWR9OgEqEpsBCgpEZWxldGVVc2Vy",
            "EjUudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2VydmljZS52MS5EZWxldGVV",
            "c2VyUmVxdWVzdBo2LnRlbXBvcmFsLmFwaS5jbG91ZC5jbG91ZHNlcnZpY2Uu",
            "djEuRGVsZXRlVXNlclJlc3BvbnNlIh6C0+STAhgqFi9jbG91ZC91c2Vycy97",
            "dXNlcl9pZH0S4AEKFlNldFVzZXJOYW1lc3BhY2VBY2Nlc3MSQS50ZW1wb3Jh",
            "bC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNlLnYxLlNldFVzZXJOYW1lc3BhY2VB",
            "Y2Nlc3NSZXF1ZXN0GkIudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2Vydmlj",
            "ZS52MS5TZXRVc2VyTmFtZXNwYWNlQWNjZXNzUmVzcG9uc2UiP4LT5JMCOSI0",
            "L2Nsb3VkL25hbWVzcGFjZXMve25hbWVzcGFjZX0vdXNlcnMve3VzZXJfaWR9",
            "L2FjY2VzczoBKhLAAQoRR2V0QXN5bmNPcGVyYXRpb24SPC50ZW1wb3JhbC5h",
            "cGkuY2xvdWQuY2xvdWRzZXJ2aWNlLnYxLkdldEFzeW5jT3BlcmF0aW9uUmVx",
            "dWVzdBo9LnRlbXBvcmFsLmFwaS5jbG91ZC5jbG91ZHNlcnZpY2UudjEuR2V0",
            "QXN5bmNPcGVyYXRpb25SZXNwb25zZSIugtPkkwIoEiYvY2xvdWQvb3BlcmF0",
            "aW9ucy97YXN5bmNfb3BlcmF0aW9uX2lkfRKoAQoPQ3JlYXRlTmFtZXNwYWNl",
            "EjoudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2VydmljZS52MS5DcmVhdGVO",
            "YW1lc3BhY2VSZXF1ZXN0GjsudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2Vy",
            "dmljZS52MS5DcmVhdGVOYW1lc3BhY2VSZXNwb25zZSIcgtPkkwIWIhEvY2xv",
            "dWQvbmFtZXNwYWNlczoBKhKfAQoNR2V0TmFtZXNwYWNlcxI4LnRlbXBvcmFs",
            "LmFwaS5jbG91ZC5jbG91ZHNlcnZpY2UudjEuR2V0TmFtZXNwYWNlc1JlcXVl",
            "c3QaOS50ZW1wb3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNlLnYxLkdldE5h",
            "bWVzcGFjZXNSZXNwb25zZSIZgtPkkwITEhEvY2xvdWQvbmFtZXNwYWNlcxKo",
            "AQoMR2V0TmFtZXNwYWNlEjcudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2Vy",
            "dmljZS52MS5HZXROYW1lc3BhY2VSZXF1ZXN0GjgudGVtcG9yYWwuYXBpLmNs",
            "b3VkLmNsb3Vkc2VydmljZS52MS5HZXROYW1lc3BhY2VSZXNwb25zZSIlgtPk",
            "kwIfEh0vY2xvdWQvbmFtZXNwYWNlcy97bmFtZXNwYWNlfRK0AQoPVXBkYXRl",
            "TmFtZXNwYWNlEjoudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2VydmljZS52",
            "MS5VcGRhdGVOYW1lc3BhY2VSZXF1ZXN0GjsudGVtcG9yYWwuYXBpLmNsb3Vk",
            "LmNsb3Vkc2VydmljZS52MS5VcGRhdGVOYW1lc3BhY2VSZXNwb25zZSIogtPk",
            "kwIiIh0vY2xvdWQvbmFtZXNwYWNlcy97bmFtZXNwYWNlfToBKhL3AQobUmVu",
            "YW1lQ3VzdG9tU2VhcmNoQXR0cmlidXRlEkYudGVtcG9yYWwuYXBpLmNsb3Vk",
            "LmNsb3Vkc2VydmljZS52MS5SZW5hbWVDdXN0b21TZWFyY2hBdHRyaWJ1dGVS",
            "ZXF1ZXN0GkcudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2VydmljZS52MS5S",
            "ZW5hbWVDdXN0b21TZWFyY2hBdHRyaWJ1dGVSZXNwb25zZSJHgtPkkwJBIjwv",
            "Y2xvdWQvbmFtZXNwYWNlcy97bmFtZXNwYWNlfS9yZW5hbWUtY3VzdG9tLXNl",
            "YXJjaC1hdHRyaWJ1dGU6ASoSsQEKD0RlbGV0ZU5hbWVzcGFjZRI6LnRlbXBv",
            "cmFsLmFwaS5jbG91ZC5jbG91ZHNlcnZpY2UudjEuRGVsZXRlTmFtZXNwYWNl",
            "UmVxdWVzdBo7LnRlbXBvcmFsLmFwaS5jbG91ZC5jbG91ZHNlcnZpY2UudjEu",
            "RGVsZXRlTmFtZXNwYWNlUmVzcG9uc2UiJYLT5JMCHyodL2Nsb3VkL25hbWVz",
            "cGFjZXMve25hbWVzcGFjZX0S3AEKF0ZhaWxvdmVyTmFtZXNwYWNlUmVnaW9u",
            "EkIudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2VydmljZS52MS5GYWlsb3Zl",
            "ck5hbWVzcGFjZVJlZ2lvblJlcXVlc3QaQy50ZW1wb3JhbC5hcGkuY2xvdWQu",
            "Y2xvdWRzZXJ2aWNlLnYxLkZhaWxvdmVyTmFtZXNwYWNlUmVnaW9uUmVzcG9u",
            "c2UiOILT5JMCMiItL2Nsb3VkL25hbWVzcGFjZXMve25hbWVzcGFjZX0vZmFp",
            "bG92ZXItcmVnaW9uOgEqEsgBChJBZGROYW1lc3BhY2VSZWdpb24SPS50ZW1w",
            "b3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNlLnYxLkFkZE5hbWVzcGFjZVJl",
            "Z2lvblJlcXVlc3QaPi50ZW1wb3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNl",
            "LnYxLkFkZE5hbWVzcGFjZVJlZ2lvblJlc3BvbnNlIjOC0+STAi0iKC9jbG91",
            "ZC9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L2FkZC1yZWdpb246ASoSkwEKCkdl",
            "dFJlZ2lvbnMSNS50ZW1wb3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNlLnYx",
            "LkdldFJlZ2lvbnNSZXF1ZXN0GjYudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vk",
            "c2VydmljZS52MS5HZXRSZWdpb25zUmVzcG9uc2UiFoLT5JMCEBIOL2Nsb3Vk",
            "L3JlZ2lvbnMSmQEKCUdldFJlZ2lvbhI0LnRlbXBvcmFsLmFwaS5jbG91ZC5j",
            "bG91ZHNlcnZpY2UudjEuR2V0UmVnaW9uUmVxdWVzdBo1LnRlbXBvcmFsLmFw",
            "aS5jbG91ZC5jbG91ZHNlcnZpY2UudjEuR2V0UmVnaW9uUmVzcG9uc2UiH4LT",
            "5JMCGRIXL2Nsb3VkL3JlZ2lvbnMve3JlZ2lvbn0SlAEKCkdldEFwaUtleXMS",
            "NS50ZW1wb3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNlLnYxLkdldEFwaUtl",
            "eXNSZXF1ZXN0GjYudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2VydmljZS52",
            "MS5HZXRBcGlLZXlzUmVzcG9uc2UiF4LT5JMCERIPL2Nsb3VkL2FwaS1rZXlz",
            "EpoBCglHZXRBcGlLZXkSNC50ZW1wb3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2",
            "aWNlLnYxLkdldEFwaUtleVJlcXVlc3QaNS50ZW1wb3JhbC5hcGkuY2xvdWQu",
            "Y2xvdWRzZXJ2aWNlLnYxLkdldEFwaUtleVJlc3BvbnNlIiCC0+STAhoSGC9j",
            "bG91ZC9hcGkta2V5cy97a2V5X2lkfRKdAQoMQ3JlYXRlQXBpS2V5EjcudGVt",
            "cG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2VydmljZS52MS5DcmVhdGVBcGlLZXlS",
            "ZXF1ZXN0GjgudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2VydmljZS52MS5D",
            "cmVhdGVBcGlLZXlSZXNwb25zZSIagtPkkwIUIg8vY2xvdWQvYXBpLWtleXM6",
            "ASoSpgEKDFVwZGF0ZUFwaUtleRI3LnRlbXBvcmFsLmFwaS5jbG91ZC5jbG91",
            "ZHNlcnZpY2UudjEuVXBkYXRlQXBpS2V5UmVxdWVzdBo4LnRlbXBvcmFsLmFw",
            "aS5jbG91ZC5jbG91ZHNlcnZpY2UudjEuVXBkYXRlQXBpS2V5UmVzcG9uc2Ui",
            "I4LT5JMCHSIYL2Nsb3VkL2FwaS1rZXlzL3trZXlfaWR9OgEqEqMBCgxEZWxl",
            "dGVBcGlLZXkSNy50ZW1wb3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNlLnYx",
            "LkRlbGV0ZUFwaUtleVJlcXVlc3QaOC50ZW1wb3JhbC5hcGkuY2xvdWQuY2xv",
            "dWRzZXJ2aWNlLnYxLkRlbGV0ZUFwaUtleVJlc3BvbnNlIiCC0+STAhoqGC9j",
            "bG91ZC9hcGkta2V5cy97a2V5X2lkfRKgAQoNR2V0VXNlckdyb3VwcxI4LnRl",
            "bXBvcmFsLmFwaS5jbG91ZC5jbG91ZHNlcnZpY2UudjEuR2V0VXNlckdyb3Vw",
            "c1JlcXVlc3QaOS50ZW1wb3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNlLnYx",
            "LkdldFVzZXJHcm91cHNSZXNwb25zZSIagtPkkwIUEhIvY2xvdWQvdXNlci1n",
            "cm91cHMSqAEKDEdldFVzZXJHcm91cBI3LnRlbXBvcmFsLmFwaS5jbG91ZC5j",
            "bG91ZHNlcnZpY2UudjEuR2V0VXNlckdyb3VwUmVxdWVzdBo4LnRlbXBvcmFs",
            "LmFwaS5jbG91ZC5jbG91ZHNlcnZpY2UudjEuR2V0VXNlckdyb3VwUmVzcG9u",
            "c2UiJYLT5JMCHxIdL2Nsb3VkL3VzZXItZ3JvdXBzL3tncm91cF9pZH0SqQEK",
            "D0NyZWF0ZVVzZXJHcm91cBI6LnRlbXBvcmFsLmFwaS5jbG91ZC5jbG91ZHNl",
            "cnZpY2UudjEuQ3JlYXRlVXNlckdyb3VwUmVxdWVzdBo7LnRlbXBvcmFsLmFw",
            "aS5jbG91ZC5jbG91ZHNlcnZpY2UudjEuQ3JlYXRlVXNlckdyb3VwUmVzcG9u",
            "c2UiHYLT5JMCFyISL2Nsb3VkL3VzZXItZ3JvdXBzOgEqErQBCg9VcGRhdGVV",
            "c2VyR3JvdXASOi50ZW1wb3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNlLnYx",
            "LlVwZGF0ZVVzZXJHcm91cFJlcXVlc3QaOy50ZW1wb3JhbC5hcGkuY2xvdWQu",
            "Y2xvdWRzZXJ2aWNlLnYxLlVwZGF0ZVVzZXJHcm91cFJlc3BvbnNlIiiC0+ST",
            "AiIiHS9jbG91ZC91c2VyLWdyb3Vwcy97Z3JvdXBfaWR9OgEqErEBCg9EZWxl",
            "dGVVc2VyR3JvdXASOi50ZW1wb3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNl",
            "LnYxLkRlbGV0ZVVzZXJHcm91cFJlcXVlc3QaOy50ZW1wb3JhbC5hcGkuY2xv",
            "dWQuY2xvdWRzZXJ2aWNlLnYxLkRlbGV0ZVVzZXJHcm91cFJlc3BvbnNlIiWC",
            "0+STAh8qHS9jbG91ZC91c2VyLWdyb3Vwcy97Z3JvdXBfaWR9EvYBChtTZXRV",
            "c2VyR3JvdXBOYW1lc3BhY2VBY2Nlc3MSRi50ZW1wb3JhbC5hcGkuY2xvdWQu",
            "Y2xvdWRzZXJ2aWNlLnYxLlNldFVzZXJHcm91cE5hbWVzcGFjZUFjY2Vzc1Jl",
            "cXVlc3QaRy50ZW1wb3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNlLnYxLlNl",
            "dFVzZXJHcm91cE5hbWVzcGFjZUFjY2Vzc1Jlc3BvbnNlIkaC0+STAkAiOy9j",
            "bG91ZC9uYW1lc3BhY2VzL3tuYW1lc3BhY2V9L3VzZXItZ3JvdXBzL3tncm91",
            "cF9pZH0vYWNjZXNzOgEqEr0BChRDcmVhdGVTZXJ2aWNlQWNjb3VudBI/LnRl",
            "bXBvcmFsLmFwaS5jbG91ZC5jbG91ZHNlcnZpY2UudjEuQ3JlYXRlU2Vydmlj",
            "ZUFjY291bnRSZXF1ZXN0GkAudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2Vy",
            "dmljZS52MS5DcmVhdGVTZXJ2aWNlQWNjb3VudFJlc3BvbnNlIiKC0+STAhwi",
            "Fy9jbG91ZC9zZXJ2aWNlLWFjY291bnRzOgEqEsYBChFHZXRTZXJ2aWNlQWNj",
            "b3VudBI8LnRlbXBvcmFsLmFwaS5jbG91ZC5jbG91ZHNlcnZpY2UudjEuR2V0",
            "U2VydmljZUFjY291bnRSZXF1ZXN0Gj0udGVtcG9yYWwuYXBpLmNsb3VkLmNs",
            "b3Vkc2VydmljZS52MS5HZXRTZXJ2aWNlQWNjb3VudFJlc3BvbnNlIjSC0+ST",
            "Ai4SLC9jbG91ZC9zZXJ2aWNlLWFjY291bnRzL3tzZXJ2aWNlX2FjY291bnRf",
            "aWR9ErQBChJHZXRTZXJ2aWNlQWNjb3VudHMSPS50ZW1wb3JhbC5hcGkuY2xv",
            "dWQuY2xvdWRzZXJ2aWNlLnYxLkdldFNlcnZpY2VBY2NvdW50c1JlcXVlc3Qa",
            "Pi50ZW1wb3JhbC5hcGkuY2xvdWQuY2xvdWRzZXJ2aWNlLnYxLkdldFNlcnZp",
            "Y2VBY2NvdW50c1Jlc3BvbnNlIh+C0+STAhkSFy9jbG91ZC9zZXJ2aWNlLWFj",
            "Y291bnRzEtIBChRVcGRhdGVTZXJ2aWNlQWNjb3VudBI/LnRlbXBvcmFsLmFw",
            "aS5jbG91ZC5jbG91ZHNlcnZpY2UudjEuVXBkYXRlU2VydmljZUFjY291bnRS",
            "ZXF1ZXN0GkAudGVtcG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2VydmljZS52MS5V",
            "cGRhdGVTZXJ2aWNlQWNjb3VudFJlc3BvbnNlIjeC0+STAjEiLC9jbG91ZC9z",
            "ZXJ2aWNlLWFjY291bnRzL3tzZXJ2aWNlX2FjY291bnRfaWR9OgEqEs8BChRE",
            "ZWxldGVTZXJ2aWNlQWNjb3VudBI/LnRlbXBvcmFsLmFwaS5jbG91ZC5jbG91",
            "ZHNlcnZpY2UudjEuRGVsZXRlU2VydmljZUFjY291bnRSZXF1ZXN0GkAudGVt",
            "cG9yYWwuYXBpLmNsb3VkLmNsb3Vkc2VydmljZS52MS5EZWxldGVTZXJ2aWNl",
            "QWNjb3VudFJlc3BvbnNlIjSC0+STAi4qLC9jbG91ZC9zZXJ2aWNlLWFjY291",
            "bnRzL3tzZXJ2aWNlX2FjY291bnRfaWR9QsABCiVpby50ZW1wb3JhbC5hcGku",
            "Y2xvdWQuY2xvdWRzZXJ2aWNlLnYxQgxTZXJ2aWNlUHJvdG9QAVo1Z28udGVt",
            "cG9yYWwuaW8vYXBpL2Nsb3VkL2Nsb3Vkc2VydmljZS92MTtjbG91ZHNlcnZp",
            "Y2WqAiRUZW1wb3JhbGlvLkFwaS5DbG91ZC5DbG91ZFNlcnZpY2UuVjHqAihU",
            "ZW1wb3JhbGlvOjpBcGk6OkNsb3VkOjpDbG91ZFNlcnZpY2U6OlYxYgZwcm90",
            "bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Temporalio.Api.Cloud.CloudService.V1.RequestResponseReflection.Descriptor, global::Temporalio.Api.Dependencies.Google.Api.AnnotationsReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, null));
    }
    #endregion

  }
}

#endregion Designer generated code
