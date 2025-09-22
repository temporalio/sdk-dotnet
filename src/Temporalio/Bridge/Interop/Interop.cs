using System;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge.Interop
{
    internal enum TemporalCoreForwardedLogLevel
    {
        Trace = 0,
        Debug,
        Info,
        Warn,
        Error,
    }

    internal enum TemporalCoreMetricAttributeValueType
    {
        String = 1,
        Int,
        Float,
        Bool,
    }

    internal enum TemporalCoreMetricKind
    {
        CounterInteger = 1,
        HistogramInteger,
        HistogramFloat,
        HistogramDuration,
        GaugeInteger,
        GaugeFloat,
    }

    internal enum TemporalCoreOpenTelemetryMetricTemporality
    {
        Cumulative = 1,
        Delta,
    }

    internal enum TemporalCoreOpenTelemetryProtocol
    {
        Grpc = 1,
        Http,
    }

    internal enum TemporalCoreRpcService
    {
        Workflow = 1,
        Operator,
        Cloud,
        Test,
        Health,
    }

    internal enum TemporalCoreSlotKindType
    {
        WorkflowSlotKindType,
        ActivitySlotKindType,
        LocalActivitySlotKindType,
        NexusSlotKindType,
    }

    internal partial struct TemporalCoreCancellationToken
    {
    }

    internal partial struct TemporalCoreClient
    {
    }

    internal partial struct TemporalCoreClientGrpcOverrideRequest
    {
    }

    internal partial struct TemporalCoreEphemeralServer
    {
    }

    internal partial struct TemporalCoreForwardedLog
    {
    }

    internal partial struct TemporalCoreMetric
    {
    }

    internal partial struct TemporalCoreMetricAttributes
    {
    }

    internal partial struct TemporalCoreMetricMeter
    {
    }

    internal partial struct TemporalCoreRandom
    {
    }

    internal partial struct TemporalCoreRuntime
    {
    }

    internal partial struct TemporalCoreWorker
    {
    }

    internal partial struct TemporalCoreWorkerReplayPusher
    {
    }

    internal unsafe partial struct TemporalCoreByteArrayRef
    {
        [NativeTypeName("const uint8_t *")]
        public byte* data;

        [NativeTypeName("size_t")]
        public UIntPtr size;
    }

    internal partial struct TemporalCoreClientTlsOptions
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef server_root_ca_cert;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef domain;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef client_cert;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef client_private_key;
    }

    internal partial struct TemporalCoreClientRetryOptions
    {
        [NativeTypeName("uint64_t")]
        public ulong initial_interval_millis;

        public double randomization_factor;

        public double multiplier;

        [NativeTypeName("uint64_t")]
        public ulong max_interval_millis;

        [NativeTypeName("uint64_t")]
        public ulong max_elapsed_time_millis;

        [NativeTypeName("uintptr_t")]
        public UIntPtr max_retries;
    }

    internal partial struct TemporalCoreClientKeepAliveOptions
    {
        [NativeTypeName("uint64_t")]
        public ulong interval_millis;

        [NativeTypeName("uint64_t")]
        public ulong timeout_millis;
    }

    internal partial struct TemporalCoreClientHttpConnectProxyOptions
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef target_host;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef username;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef password;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreClientGrpcOverrideCallback([NativeTypeName("struct TemporalCoreClientGrpcOverrideRequest *")] TemporalCoreClientGrpcOverrideRequest* request, void* user_data);

    internal unsafe partial struct TemporalCoreClientOptions
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef target_url;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef client_name;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef client_version;

        [NativeTypeName("TemporalCoreMetadataRef")]
        public TemporalCoreByteArrayRef metadata;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef api_key;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef identity;

        [NativeTypeName("const struct TemporalCoreClientTlsOptions *")]
        public TemporalCoreClientTlsOptions* tls_options;

        [NativeTypeName("const struct TemporalCoreClientRetryOptions *")]
        public TemporalCoreClientRetryOptions* retry_options;

        [NativeTypeName("const struct TemporalCoreClientKeepAliveOptions *")]
        public TemporalCoreClientKeepAliveOptions* keep_alive_options;

        [NativeTypeName("const struct TemporalCoreClientHttpConnectProxyOptions *")]
        public TemporalCoreClientHttpConnectProxyOptions* http_connect_proxy_options;

        [NativeTypeName("TemporalCoreClientGrpcOverrideCallback")]
        public IntPtr grpc_override_callback;

        public void* grpc_override_callback_user_data;
    }

    internal unsafe partial struct TemporalCoreByteArray
    {
        [NativeTypeName("const uint8_t *")]
        public byte* data;

        [NativeTypeName("size_t")]
        public UIntPtr size;

        [NativeTypeName("size_t")]
        public UIntPtr cap;

        [NativeTypeName("bool")]
        public byte disable_free;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreClientConnectCallback(void* user_data, [NativeTypeName("struct TemporalCoreClient *")] TemporalCoreClient* success, [NativeTypeName("const struct TemporalCoreByteArray *")] TemporalCoreByteArray* fail);

    internal partial struct TemporalCoreClientGrpcOverrideResponse
    {
        [NativeTypeName("int32_t")]
        public int status_code;

        [NativeTypeName("TemporalCoreMetadataRef")]
        public TemporalCoreByteArrayRef headers;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef success_proto;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef fail_message;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef fail_details;
    }

    internal unsafe partial struct TemporalCoreRpcCallOptions
    {
        [NativeTypeName("enum TemporalCoreRpcService")]
        public TemporalCoreRpcService service;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef rpc;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef req;

        [NativeTypeName("bool")]
        public byte retry;

        [NativeTypeName("TemporalCoreMetadataRef")]
        public TemporalCoreByteArrayRef metadata;

        [NativeTypeName("uint32_t")]
        public uint timeout_millis;

        [NativeTypeName("const struct TemporalCoreCancellationToken *")]
        public TemporalCoreCancellationToken* cancellation_token;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreClientRpcCallCallback(void* user_data, [NativeTypeName("const struct TemporalCoreByteArray *")] TemporalCoreByteArray* success, [NativeTypeName("uint32_t")] uint status_code, [NativeTypeName("const struct TemporalCoreByteArray *")] TemporalCoreByteArray* failure_message, [NativeTypeName("const struct TemporalCoreByteArray *")] TemporalCoreByteArray* failure_details);

    internal unsafe partial struct TemporalCoreClientEnvConfigOrFail
    {
        [NativeTypeName("const struct TemporalCoreByteArray *")]
        public TemporalCoreByteArray* success;

        [NativeTypeName("const struct TemporalCoreByteArray *")]
        public TemporalCoreByteArray* fail;
    }

    internal partial struct TemporalCoreClientEnvConfigLoadOptions
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef path;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef data;

        [NativeTypeName("bool")]
        public byte config_file_strict;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef env_vars;
    }

    internal unsafe partial struct TemporalCoreClientEnvConfigProfileOrFail
    {
        [NativeTypeName("const struct TemporalCoreByteArray *")]
        public TemporalCoreByteArray* success;

        [NativeTypeName("const struct TemporalCoreByteArray *")]
        public TemporalCoreByteArray* fail;
    }

    internal partial struct TemporalCoreClientEnvConfigProfileLoadOptions
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef profile;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef path;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef data;

        [NativeTypeName("bool")]
        public byte disable_file;

        [NativeTypeName("bool")]
        public byte disable_env;

        [NativeTypeName("bool")]
        public byte config_file_strict;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef env_vars;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal partial struct TemporalCoreMetricAttributeValue
    {
        [FieldOffset(0)]
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef string_value;

        [FieldOffset(0)]
        [NativeTypeName("int64_t")]
        public long int_value;

        [FieldOffset(0)]
        public double float_value;

        [FieldOffset(0)]
        [NativeTypeName("bool")]
        public byte bool_value;
    }

    internal partial struct TemporalCoreMetricAttribute
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef key;

        [NativeTypeName("union TemporalCoreMetricAttributeValue")]
        public TemporalCoreMetricAttributeValue value;

        [NativeTypeName("enum TemporalCoreMetricAttributeValueType")]
        public TemporalCoreMetricAttributeValueType value_type;
    }

    internal partial struct TemporalCoreMetricOptions
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef name;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef description;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef unit;

        [NativeTypeName("enum TemporalCoreMetricKind")]
        public TemporalCoreMetricKind kind;
    }

    internal unsafe partial struct TemporalCoreRuntimeOrFail
    {
        [NativeTypeName("struct TemporalCoreRuntime *")]
        public TemporalCoreRuntime* runtime;

        [NativeTypeName("const struct TemporalCoreByteArray *")]
        public TemporalCoreByteArray* fail;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreForwardedLogCallback([NativeTypeName("enum TemporalCoreForwardedLogLevel")] TemporalCoreForwardedLogLevel level, [NativeTypeName("const struct TemporalCoreForwardedLog *")] TemporalCoreForwardedLog* log);

    internal partial struct TemporalCoreLoggingOptions
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef filter;

        [NativeTypeName("TemporalCoreForwardedLogCallback")]
        public IntPtr forward_to;
    }

    internal partial struct TemporalCoreOpenTelemetryOptions
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef url;

        [NativeTypeName("TemporalCoreMetadataRef")]
        public TemporalCoreByteArrayRef headers;

        [NativeTypeName("uint32_t")]
        public uint metric_periodicity_millis;

        [NativeTypeName("enum TemporalCoreOpenTelemetryMetricTemporality")]
        public TemporalCoreOpenTelemetryMetricTemporality metric_temporality;

        [NativeTypeName("bool")]
        public byte durations_as_seconds;

        [NativeTypeName("enum TemporalCoreOpenTelemetryProtocol")]
        public TemporalCoreOpenTelemetryProtocol protocol;

        [NativeTypeName("TemporalCoreMetadataRef")]
        public TemporalCoreByteArrayRef histogram_bucket_overrides;
    }

    internal partial struct TemporalCorePrometheusOptions
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef bind_address;

        [NativeTypeName("bool")]
        public byte counters_total_suffix;

        [NativeTypeName("bool")]
        public byte unit_suffix;

        [NativeTypeName("bool")]
        public byte durations_as_seconds;

        [NativeTypeName("TemporalCoreMetadataRef")]
        public TemporalCoreByteArrayRef histogram_bucket_overrides;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("const void *")]
    internal unsafe delegate void* TemporalCoreCustomMetricMeterMetricNewCallback([NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef name, [NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef description, [NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef unit, [NativeTypeName("enum TemporalCoreMetricKind")] TemporalCoreMetricKind kind);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreCustomMetricMeterMetricFreeCallback([NativeTypeName("const void *")] void* metric);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreCustomMetricMeterMetricRecordIntegerCallback([NativeTypeName("const void *")] void* metric, [NativeTypeName("uint64_t")] ulong value, [NativeTypeName("const void *")] void* attributes);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreCustomMetricMeterMetricRecordFloatCallback([NativeTypeName("const void *")] void* metric, double value, [NativeTypeName("const void *")] void* attributes);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreCustomMetricMeterMetricRecordDurationCallback([NativeTypeName("const void *")] void* metric, [NativeTypeName("uint64_t")] ulong value_ms, [NativeTypeName("const void *")] void* attributes);

    internal unsafe partial struct TemporalCoreCustomMetricAttributeValueString
    {
        [NativeTypeName("const uint8_t *")]
        public byte* data;

        [NativeTypeName("size_t")]
        public UIntPtr size;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal partial struct TemporalCoreCustomMetricAttributeValue
    {
        [FieldOffset(0)]
        [NativeTypeName("struct TemporalCoreCustomMetricAttributeValueString")]
        public TemporalCoreCustomMetricAttributeValueString string_value;

        [FieldOffset(0)]
        [NativeTypeName("int64_t")]
        public long int_value;

        [FieldOffset(0)]
        public double float_value;

        [FieldOffset(0)]
        [NativeTypeName("bool")]
        public byte bool_value;
    }

    internal partial struct TemporalCoreCustomMetricAttribute
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef key;

        [NativeTypeName("union TemporalCoreCustomMetricAttributeValue")]
        public TemporalCoreCustomMetricAttributeValue value;

        [NativeTypeName("enum TemporalCoreMetricAttributeValueType")]
        public TemporalCoreMetricAttributeValueType value_type;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("const void *")]
    internal unsafe delegate void* TemporalCoreCustomMetricMeterAttributesNewCallback([NativeTypeName("const void *")] void* append_from, [NativeTypeName("const struct TemporalCoreCustomMetricAttribute *")] TemporalCoreCustomMetricAttribute* attributes, [NativeTypeName("size_t")] UIntPtr attributes_size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreCustomMetricMeterAttributesFreeCallback([NativeTypeName("const void *")] void* attributes);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreCustomMetricMeterMeterFreeCallback([NativeTypeName("const struct TemporalCoreCustomMetricMeter *")] TemporalCoreCustomMetricMeter* meter);

    internal partial struct TemporalCoreCustomMetricMeter
    {
        [NativeTypeName("TemporalCoreCustomMetricMeterMetricNewCallback")]
        public IntPtr metric_new;

        [NativeTypeName("TemporalCoreCustomMetricMeterMetricFreeCallback")]
        public IntPtr metric_free;

        [NativeTypeName("TemporalCoreCustomMetricMeterMetricRecordIntegerCallback")]
        public IntPtr metric_record_integer;

        [NativeTypeName("TemporalCoreCustomMetricMeterMetricRecordFloatCallback")]
        public IntPtr metric_record_float;

        [NativeTypeName("TemporalCoreCustomMetricMeterMetricRecordDurationCallback")]
        public IntPtr metric_record_duration;

        [NativeTypeName("TemporalCoreCustomMetricMeterAttributesNewCallback")]
        public IntPtr attributes_new;

        [NativeTypeName("TemporalCoreCustomMetricMeterAttributesFreeCallback")]
        public IntPtr attributes_free;

        [NativeTypeName("TemporalCoreCustomMetricMeterMeterFreeCallback")]
        public IntPtr meter_free;
    }

    internal unsafe partial struct TemporalCoreMetricsOptions
    {
        [NativeTypeName("const struct TemporalCoreOpenTelemetryOptions *")]
        public TemporalCoreOpenTelemetryOptions* opentelemetry;

        [NativeTypeName("const struct TemporalCorePrometheusOptions *")]
        public TemporalCorePrometheusOptions* prometheus;

        [NativeTypeName("const struct TemporalCoreCustomMetricMeter *")]
        public TemporalCoreCustomMetricMeter* custom_meter;

        [NativeTypeName("bool")]
        public byte attach_service_name;

        [NativeTypeName("TemporalCoreMetadataRef")]
        public TemporalCoreByteArrayRef global_tags;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef metric_prefix;
    }

    internal unsafe partial struct TemporalCoreTelemetryOptions
    {
        [NativeTypeName("const struct TemporalCoreLoggingOptions *")]
        public TemporalCoreLoggingOptions* logging;

        [NativeTypeName("const struct TemporalCoreMetricsOptions *")]
        public TemporalCoreMetricsOptions* metrics;
    }

    internal unsafe partial struct TemporalCoreRuntimeOptions
    {
        [NativeTypeName("const struct TemporalCoreTelemetryOptions *")]
        public TemporalCoreTelemetryOptions* telemetry;
    }

    internal partial struct TemporalCoreTestServerOptions
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef existing_path;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef sdk_name;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef sdk_version;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef download_version;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef download_dest_dir;

        [NativeTypeName("uint16_t")]
        public ushort port;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef extra_args;

        [NativeTypeName("uint64_t")]
        public ulong download_ttl_seconds;
    }

    internal unsafe partial struct TemporalCoreDevServerOptions
    {
        [NativeTypeName("const struct TemporalCoreTestServerOptions *")]
        public TemporalCoreTestServerOptions* test_server;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef namespace_;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef ip;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef database_filename;

        [NativeTypeName("bool")]
        public byte ui;

        [NativeTypeName("uint16_t")]
        public ushort ui_port;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef log_format;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef log_level;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreEphemeralServerStartCallback(void* user_data, [NativeTypeName("struct TemporalCoreEphemeralServer *")] TemporalCoreEphemeralServer* success, [NativeTypeName("const struct TemporalCoreByteArray *")] TemporalCoreByteArray* success_target, [NativeTypeName("const struct TemporalCoreByteArray *")] TemporalCoreByteArray* fail);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreEphemeralServerShutdownCallback(void* user_data, [NativeTypeName("const struct TemporalCoreByteArray *")] TemporalCoreByteArray* fail);

    internal unsafe partial struct TemporalCoreWorkerOrFail
    {
        [NativeTypeName("struct TemporalCoreWorker *")]
        public TemporalCoreWorker* worker;

        [NativeTypeName("const struct TemporalCoreByteArray *")]
        public TemporalCoreByteArray* fail;
    }

    internal partial struct TemporalCoreWorkerVersioningNone
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef build_id;
    }

    internal partial struct TemporalCoreWorkerDeploymentVersion
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef deployment_name;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef build_id;
    }

    internal partial struct TemporalCoreWorkerDeploymentOptions
    {
        [NativeTypeName("struct TemporalCoreWorkerDeploymentVersion")]
        public TemporalCoreWorkerDeploymentVersion version;

        [NativeTypeName("bool")]
        public byte use_worker_versioning;

        [NativeTypeName("int32_t")]
        public int default_versioning_behavior;
    }

    internal partial struct TemporalCoreLegacyBuildIdBasedStrategy
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef build_id;
    }

    internal enum TemporalCoreWorkerVersioningStrategy_Tag
    {
        None,
        DeploymentBased,
        LegacyBuildIdBased,
    }

    internal unsafe partial struct TemporalCoreWorkerVersioningStrategy
    {
        public TemporalCoreWorkerVersioningStrategy_Tag tag;

        [NativeTypeName("__AnonymousRecord_temporal-sdk-core-c-bridge_L538_C3")]
        public _Anonymous_e__Union Anonymous;

        internal ref TemporalCoreWorkerVersioningNone none
        {
            get
            {
                fixed (_Anonymous_e__Union._Anonymous1_e__Struct* pField = &Anonymous.Anonymous1)
                {
                    return ref pField->none;
                }
            }
        }

        internal ref TemporalCoreWorkerDeploymentOptions deployment_based
        {
            get
            {
                fixed (_Anonymous_e__Union._Anonymous2_e__Struct* pField = &Anonymous.Anonymous2)
                {
                    return ref pField->deployment_based;
                }
            }
        }

        internal ref TemporalCoreLegacyBuildIdBasedStrategy legacy_build_id_based
        {
            get
            {
                fixed (_Anonymous_e__Union._Anonymous3_e__Struct* pField = &Anonymous.Anonymous3)
                {
                    return ref pField->legacy_build_id_based;
                }
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        internal unsafe partial struct _Anonymous_e__Union
        {
            [FieldOffset(0)]
            [NativeTypeName("__AnonymousRecord_temporal-sdk-core-c-bridge_L539_C5")]
            public _Anonymous1_e__Struct Anonymous1;

            [FieldOffset(0)]
            [NativeTypeName("__AnonymousRecord_temporal-sdk-core-c-bridge_L542_C5")]
            public _Anonymous2_e__Struct Anonymous2;

            [FieldOffset(0)]
            [NativeTypeName("__AnonymousRecord_temporal-sdk-core-c-bridge_L545_C5")]
            public _Anonymous3_e__Struct Anonymous3;

            internal partial struct _Anonymous1_e__Struct
            {
                [NativeTypeName("struct TemporalCoreWorkerVersioningNone")]
                public TemporalCoreWorkerVersioningNone none;
            }

            internal partial struct _Anonymous2_e__Struct
            {
                [NativeTypeName("struct TemporalCoreWorkerDeploymentOptions")]
                public TemporalCoreWorkerDeploymentOptions deployment_based;
            }

            internal partial struct _Anonymous3_e__Struct
            {
                [NativeTypeName("struct TemporalCoreLegacyBuildIdBasedStrategy")]
                public TemporalCoreLegacyBuildIdBasedStrategy legacy_build_id_based;
            }
        }
    }

    internal partial struct TemporalCoreFixedSizeSlotSupplier
    {
        [NativeTypeName("uintptr_t")]
        public UIntPtr num_slots;
    }

    internal partial struct TemporalCoreResourceBasedTunerOptions
    {
        public double target_memory_usage;

        public double target_cpu_usage;
    }

    internal partial struct TemporalCoreResourceBasedSlotSupplier
    {
        [NativeTypeName("uintptr_t")]
        public UIntPtr minimum_slots;

        [NativeTypeName("uintptr_t")]
        public UIntPtr maximum_slots;

        [NativeTypeName("uint64_t")]
        public ulong ramp_throttle_ms;

        [NativeTypeName("struct TemporalCoreResourceBasedTunerOptions")]
        public TemporalCoreResourceBasedTunerOptions tuner_options;
    }

    internal unsafe partial struct TemporalCoreSlotReserveCtx
    {
        [NativeTypeName("enum TemporalCoreSlotKindType")]
        public TemporalCoreSlotKindType slot_type;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef task_queue;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef worker_identity;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef worker_build_id;

        [NativeTypeName("bool")]
        public byte is_sticky;

        public void* token_src;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreCustomReserveSlotCallback([NativeTypeName("const struct TemporalCoreSlotReserveCtx *")] TemporalCoreSlotReserveCtx* ctx, void* sender);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreCustomCancelReserveCallback(void* token_source);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("uintptr_t")]
    internal unsafe delegate UIntPtr TemporalCoreCustomTryReserveSlotCallback([NativeTypeName("const struct TemporalCoreSlotReserveCtx *")] TemporalCoreSlotReserveCtx* ctx);

    internal enum TemporalCoreSlotInfo_Tag
    {
        WorkflowSlotInfo,
        ActivitySlotInfo,
        LocalActivitySlotInfo,
        NexusSlotInfo,
    }

    internal partial struct TemporalCoreWorkflowSlotInfo_Body
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef workflow_type;

        [NativeTypeName("bool")]
        public byte is_sticky;
    }

    internal partial struct TemporalCoreActivitySlotInfo_Body
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef activity_type;
    }

    internal partial struct TemporalCoreLocalActivitySlotInfo_Body
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef activity_type;
    }

    internal partial struct TemporalCoreNexusSlotInfo_Body
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef operation;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef service;
    }

    internal unsafe partial struct TemporalCoreSlotInfo
    {
        public TemporalCoreSlotInfo_Tag tag;

        [NativeTypeName("__AnonymousRecord_temporal-sdk-core-c-bridge_L613_C3")]
        public _Anonymous_e__Union Anonymous;

        internal ref TemporalCoreWorkflowSlotInfo_Body workflow_slot_info
        {
            get
            {
                fixed (_Anonymous_e__Union* pField = &Anonymous)
                {
                    return ref pField->workflow_slot_info;
                }
            }
        }

        internal ref TemporalCoreActivitySlotInfo_Body activity_slot_info
        {
            get
            {
                fixed (_Anonymous_e__Union* pField = &Anonymous)
                {
                    return ref pField->activity_slot_info;
                }
            }
        }

        internal ref TemporalCoreLocalActivitySlotInfo_Body local_activity_slot_info
        {
            get
            {
                fixed (_Anonymous_e__Union* pField = &Anonymous)
                {
                    return ref pField->local_activity_slot_info;
                }
            }
        }

        internal ref TemporalCoreNexusSlotInfo_Body nexus_slot_info
        {
            get
            {
                fixed (_Anonymous_e__Union* pField = &Anonymous)
                {
                    return ref pField->nexus_slot_info;
                }
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        internal partial struct _Anonymous_e__Union
        {
            [FieldOffset(0)]
            public TemporalCoreWorkflowSlotInfo_Body workflow_slot_info;

            [FieldOffset(0)]
            public TemporalCoreActivitySlotInfo_Body activity_slot_info;

            [FieldOffset(0)]
            public TemporalCoreLocalActivitySlotInfo_Body local_activity_slot_info;

            [FieldOffset(0)]
            public TemporalCoreNexusSlotInfo_Body nexus_slot_info;
        }
    }

    internal partial struct TemporalCoreSlotMarkUsedCtx
    {
        [NativeTypeName("struct TemporalCoreSlotInfo")]
        public TemporalCoreSlotInfo slot_info;

        [NativeTypeName("uintptr_t")]
        public UIntPtr slot_permit;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreCustomMarkSlotUsedCallback([NativeTypeName("const struct TemporalCoreSlotMarkUsedCtx *")] TemporalCoreSlotMarkUsedCtx* ctx);

    internal unsafe partial struct TemporalCoreSlotReleaseCtx
    {
        [NativeTypeName("const struct TemporalCoreSlotInfo *")]
        public TemporalCoreSlotInfo* slot_info;

        [NativeTypeName("uintptr_t")]
        public UIntPtr slot_permit;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreCustomReleaseSlotCallback([NativeTypeName("const struct TemporalCoreSlotReleaseCtx *")] TemporalCoreSlotReleaseCtx* ctx);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreCustomSlotImplFreeCallback([NativeTypeName("const struct TemporalCoreCustomSlotSupplierCallbacks *")] TemporalCoreCustomSlotSupplierCallbacks* userimpl);

    internal partial struct TemporalCoreCustomSlotSupplierCallbacks
    {
        [NativeTypeName("TemporalCoreCustomReserveSlotCallback")]
        public IntPtr reserve;

        [NativeTypeName("TemporalCoreCustomCancelReserveCallback")]
        public IntPtr cancel_reserve;

        [NativeTypeName("TemporalCoreCustomTryReserveSlotCallback")]
        public IntPtr try_reserve;

        [NativeTypeName("TemporalCoreCustomMarkSlotUsedCallback")]
        public IntPtr mark_used;

        [NativeTypeName("TemporalCoreCustomReleaseSlotCallback")]
        public IntPtr release;

        [NativeTypeName("TemporalCoreCustomSlotImplFreeCallback")]
        public IntPtr free;
    }

    internal unsafe partial struct TemporalCoreCustomSlotSupplierCallbacksImpl
    {
        [NativeTypeName("const struct TemporalCoreCustomSlotSupplierCallbacks *")]
        public TemporalCoreCustomSlotSupplierCallbacks* _0;
    }

    internal enum TemporalCoreSlotSupplier_Tag
    {
        FixedSize,
        ResourceBased,
        Custom,
    }

    internal unsafe partial struct TemporalCoreSlotSupplier
    {
        public TemporalCoreSlotSupplier_Tag tag;

        [NativeTypeName("__AnonymousRecord_temporal-sdk-core-c-bridge_L664_C3")]
        public _Anonymous_e__Union Anonymous;

        internal ref TemporalCoreFixedSizeSlotSupplier fixed_size
        {
            get
            {
                fixed (_Anonymous_e__Union._Anonymous1_e__Struct* pField = &Anonymous.Anonymous1)
                {
                    return ref pField->fixed_size;
                }
            }
        }

        internal ref TemporalCoreResourceBasedSlotSupplier resource_based
        {
            get
            {
                fixed (_Anonymous_e__Union._Anonymous2_e__Struct* pField = &Anonymous.Anonymous2)
                {
                    return ref pField->resource_based;
                }
            }
        }

        internal ref TemporalCoreCustomSlotSupplierCallbacksImpl custom
        {
            get
            {
                fixed (_Anonymous_e__Union._Anonymous3_e__Struct* pField = &Anonymous.Anonymous3)
                {
                    return ref pField->custom;
                }
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        internal unsafe partial struct _Anonymous_e__Union
        {
            [FieldOffset(0)]
            [NativeTypeName("__AnonymousRecord_temporal-sdk-core-c-bridge_L665_C5")]
            public _Anonymous1_e__Struct Anonymous1;

            [FieldOffset(0)]
            [NativeTypeName("__AnonymousRecord_temporal-sdk-core-c-bridge_L668_C5")]
            public _Anonymous2_e__Struct Anonymous2;

            [FieldOffset(0)]
            [NativeTypeName("__AnonymousRecord_temporal-sdk-core-c-bridge_L671_C5")]
            public _Anonymous3_e__Struct Anonymous3;

            internal partial struct _Anonymous1_e__Struct
            {
                [NativeTypeName("struct TemporalCoreFixedSizeSlotSupplier")]
                public TemporalCoreFixedSizeSlotSupplier fixed_size;
            }

            internal partial struct _Anonymous2_e__Struct
            {
                [NativeTypeName("struct TemporalCoreResourceBasedSlotSupplier")]
                public TemporalCoreResourceBasedSlotSupplier resource_based;
            }

            internal partial struct _Anonymous3_e__Struct
            {
                [NativeTypeName("struct TemporalCoreCustomSlotSupplierCallbacksImpl")]
                public TemporalCoreCustomSlotSupplierCallbacksImpl custom;
            }
        }
    }

    internal partial struct TemporalCoreTunerHolder
    {
        [NativeTypeName("struct TemporalCoreSlotSupplier")]
        public TemporalCoreSlotSupplier workflow_slot_supplier;

        [NativeTypeName("struct TemporalCoreSlotSupplier")]
        public TemporalCoreSlotSupplier activity_slot_supplier;

        [NativeTypeName("struct TemporalCoreSlotSupplier")]
        public TemporalCoreSlotSupplier local_activity_slot_supplier;

        [NativeTypeName("struct TemporalCoreSlotSupplier")]
        public TemporalCoreSlotSupplier nexus_task_slot_supplier;
    }

    internal partial struct TemporalCorePollerBehaviorSimpleMaximum
    {
        [NativeTypeName("uintptr_t")]
        public UIntPtr simple_maximum;
    }

    internal partial struct TemporalCorePollerBehaviorAutoscaling
    {
        [NativeTypeName("uintptr_t")]
        public UIntPtr minimum;

        [NativeTypeName("uintptr_t")]
        public UIntPtr maximum;

        [NativeTypeName("uintptr_t")]
        public UIntPtr initial;
    }

    internal unsafe partial struct TemporalCorePollerBehavior
    {
        [NativeTypeName("const struct TemporalCorePollerBehaviorSimpleMaximum *")]
        public TemporalCorePollerBehaviorSimpleMaximum* simple_maximum;

        [NativeTypeName("const struct TemporalCorePollerBehaviorAutoscaling *")]
        public TemporalCorePollerBehaviorAutoscaling* autoscaling;
    }

    internal unsafe partial struct TemporalCoreByteArrayRefArray
    {
        [NativeTypeName("const struct TemporalCoreByteArrayRef *")]
        public TemporalCoreByteArrayRef* data;

        [NativeTypeName("size_t")]
        public UIntPtr size;
    }

    internal partial struct TemporalCoreWorkerOptions
    {
        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef namespace_;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef task_queue;

        [NativeTypeName("struct TemporalCoreWorkerVersioningStrategy")]
        public TemporalCoreWorkerVersioningStrategy versioning_strategy;

        [NativeTypeName("struct TemporalCoreByteArrayRef")]
        public TemporalCoreByteArrayRef identity_override;

        [NativeTypeName("uint32_t")]
        public uint max_cached_workflows;

        [NativeTypeName("struct TemporalCoreTunerHolder")]
        public TemporalCoreTunerHolder tuner;

        [NativeTypeName("bool")]
        public byte no_remote_activities;

        [NativeTypeName("uint64_t")]
        public ulong sticky_queue_schedule_to_start_timeout_millis;

        [NativeTypeName("uint64_t")]
        public ulong max_heartbeat_throttle_interval_millis;

        [NativeTypeName("uint64_t")]
        public ulong default_heartbeat_throttle_interval_millis;

        public double max_activities_per_second;

        public double max_task_queue_activities_per_second;

        [NativeTypeName("uint64_t")]
        public ulong graceful_shutdown_period_millis;

        [NativeTypeName("struct TemporalCorePollerBehavior")]
        public TemporalCorePollerBehavior workflow_task_poller_behavior;

        public float nonsticky_to_sticky_poll_ratio;

        [NativeTypeName("struct TemporalCorePollerBehavior")]
        public TemporalCorePollerBehavior activity_task_poller_behavior;

        [NativeTypeName("struct TemporalCorePollerBehavior")]
        public TemporalCorePollerBehavior nexus_task_poller_behavior;

        [NativeTypeName("bool")]
        public byte nondeterminism_as_workflow_fail;

        [NativeTypeName("struct TemporalCoreByteArrayRefArray")]
        public TemporalCoreByteArrayRefArray nondeterminism_as_workflow_fail_for_types;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreWorkerCallback(void* user_data, [NativeTypeName("const struct TemporalCoreByteArray *")] TemporalCoreByteArray* fail);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TemporalCoreWorkerPollCallback(void* user_data, [NativeTypeName("const struct TemporalCoreByteArray *")] TemporalCoreByteArray* success, [NativeTypeName("const struct TemporalCoreByteArray *")] TemporalCoreByteArray* fail);

    internal unsafe partial struct TemporalCoreWorkerReplayerOrFail
    {
        [NativeTypeName("struct TemporalCoreWorker *")]
        public TemporalCoreWorker* worker;

        [NativeTypeName("struct TemporalCoreWorkerReplayPusher *")]
        public TemporalCoreWorkerReplayPusher* worker_replay_pusher;

        [NativeTypeName("const struct TemporalCoreByteArray *")]
        public TemporalCoreByteArray* fail;
    }

    internal unsafe partial struct TemporalCoreWorkerReplayPushResult
    {
        [NativeTypeName("const struct TemporalCoreByteArray *")]
        public TemporalCoreByteArray* fail;
    }

    internal static unsafe partial class Methods
    {
        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreCancellationToken *")]
        public static extern TemporalCoreCancellationToken* temporal_core_cancellation_token_new();

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_cancellation_token_cancel([NativeTypeName("struct TemporalCoreCancellationToken *")] TemporalCoreCancellationToken* token);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_cancellation_token_free([NativeTypeName("struct TemporalCoreCancellationToken *")] TemporalCoreCancellationToken* token);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_client_connect([NativeTypeName("struct TemporalCoreRuntime *")] TemporalCoreRuntime* runtime, [NativeTypeName("const struct TemporalCoreClientOptions *")] TemporalCoreClientOptions* options, void* user_data, [NativeTypeName("TemporalCoreClientConnectCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_client_free([NativeTypeName("struct TemporalCoreClient *")] TemporalCoreClient* client);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_client_update_metadata([NativeTypeName("struct TemporalCoreClient *")] TemporalCoreClient* client, [NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef metadata);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_client_update_api_key([NativeTypeName("struct TemporalCoreClient *")] TemporalCoreClient* client, [NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef api_key);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreByteArrayRef")]
        public static extern TemporalCoreByteArrayRef temporal_core_client_grpc_override_request_service([NativeTypeName("const struct TemporalCoreClientGrpcOverrideRequest *")] TemporalCoreClientGrpcOverrideRequest* req);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreByteArrayRef")]
        public static extern TemporalCoreByteArrayRef temporal_core_client_grpc_override_request_rpc([NativeTypeName("const struct TemporalCoreClientGrpcOverrideRequest *")] TemporalCoreClientGrpcOverrideRequest* req);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("TemporalCoreMetadataRef")]
        public static extern TemporalCoreByteArrayRef temporal_core_client_grpc_override_request_headers([NativeTypeName("const struct TemporalCoreClientGrpcOverrideRequest *")] TemporalCoreClientGrpcOverrideRequest* req);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreByteArrayRef")]
        public static extern TemporalCoreByteArrayRef temporal_core_client_grpc_override_request_proto([NativeTypeName("const struct TemporalCoreClientGrpcOverrideRequest *")] TemporalCoreClientGrpcOverrideRequest* req);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_client_grpc_override_request_respond([NativeTypeName("struct TemporalCoreClientGrpcOverrideRequest *")] TemporalCoreClientGrpcOverrideRequest* req, [NativeTypeName("struct TemporalCoreClientGrpcOverrideResponse")] TemporalCoreClientGrpcOverrideResponse resp);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_client_rpc_call([NativeTypeName("struct TemporalCoreClient *")] TemporalCoreClient* client, [NativeTypeName("const struct TemporalCoreRpcCallOptions *")] TemporalCoreRpcCallOptions* options, void* user_data, [NativeTypeName("TemporalCoreClientRpcCallCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreClientEnvConfigOrFail")]
        public static extern TemporalCoreClientEnvConfigOrFail temporal_core_client_env_config_load([NativeTypeName("const struct TemporalCoreClientEnvConfigLoadOptions *")] TemporalCoreClientEnvConfigLoadOptions* options);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreClientEnvConfigProfileOrFail")]
        public static extern TemporalCoreClientEnvConfigProfileOrFail temporal_core_client_env_config_profile_load([NativeTypeName("const struct TemporalCoreClientEnvConfigProfileLoadOptions *")] TemporalCoreClientEnvConfigProfileLoadOptions* options);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreMetricMeter *")]
        public static extern TemporalCoreMetricMeter* temporal_core_metric_meter_new([NativeTypeName("struct TemporalCoreRuntime *")] TemporalCoreRuntime* runtime);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_metric_meter_free([NativeTypeName("struct TemporalCoreMetricMeter *")] TemporalCoreMetricMeter* meter);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreMetricAttributes *")]
        public static extern TemporalCoreMetricAttributes* temporal_core_metric_attributes_new([NativeTypeName("const struct TemporalCoreMetricMeter *")] TemporalCoreMetricMeter* meter, [NativeTypeName("const struct TemporalCoreMetricAttribute *")] TemporalCoreMetricAttribute* attrs, [NativeTypeName("size_t")] UIntPtr size);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreMetricAttributes *")]
        public static extern TemporalCoreMetricAttributes* temporal_core_metric_attributes_new_append([NativeTypeName("const struct TemporalCoreMetricMeter *")] TemporalCoreMetricMeter* meter, [NativeTypeName("const struct TemporalCoreMetricAttributes *")] TemporalCoreMetricAttributes* orig, [NativeTypeName("const struct TemporalCoreMetricAttribute *")] TemporalCoreMetricAttribute* attrs, [NativeTypeName("size_t")] UIntPtr size);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_metric_attributes_free([NativeTypeName("struct TemporalCoreMetricAttributes *")] TemporalCoreMetricAttributes* attrs);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreMetric *")]
        public static extern TemporalCoreMetric* temporal_core_metric_new([NativeTypeName("const struct TemporalCoreMetricMeter *")] TemporalCoreMetricMeter* meter, [NativeTypeName("const struct TemporalCoreMetricOptions *")] TemporalCoreMetricOptions* options);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_metric_free([NativeTypeName("struct TemporalCoreMetric *")] TemporalCoreMetric* metric);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_metric_record_integer([NativeTypeName("const struct TemporalCoreMetric *")] TemporalCoreMetric* metric, [NativeTypeName("uint64_t")] ulong value, [NativeTypeName("const struct TemporalCoreMetricAttributes *")] TemporalCoreMetricAttributes* attrs);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_metric_record_float([NativeTypeName("const struct TemporalCoreMetric *")] TemporalCoreMetric* metric, double value, [NativeTypeName("const struct TemporalCoreMetricAttributes *")] TemporalCoreMetricAttributes* attrs);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_metric_record_duration([NativeTypeName("const struct TemporalCoreMetric *")] TemporalCoreMetric* metric, [NativeTypeName("uint64_t")] ulong value_ms, [NativeTypeName("const struct TemporalCoreMetricAttributes *")] TemporalCoreMetricAttributes* attrs);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreRandom *")]
        public static extern TemporalCoreRandom* temporal_core_random_new([NativeTypeName("uint64_t")] ulong seed);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_random_free([NativeTypeName("struct TemporalCoreRandom *")] TemporalCoreRandom* random);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("int32_t")]
        public static extern int temporal_core_random_int32_range([NativeTypeName("struct TemporalCoreRandom *")] TemporalCoreRandom* random, [NativeTypeName("int32_t")] int min, [NativeTypeName("int32_t")] int max, [NativeTypeName("bool")] byte max_inclusive);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double temporal_core_random_double_range([NativeTypeName("struct TemporalCoreRandom *")] TemporalCoreRandom* random, double min, double max, [NativeTypeName("bool")] byte max_inclusive);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_random_fill_bytes([NativeTypeName("struct TemporalCoreRandom *")] TemporalCoreRandom* random, [NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef bytes);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreRuntimeOrFail")]
        public static extern TemporalCoreRuntimeOrFail temporal_core_runtime_new([NativeTypeName("const struct TemporalCoreRuntimeOptions *")] TemporalCoreRuntimeOptions* options);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_runtime_free([NativeTypeName("struct TemporalCoreRuntime *")] TemporalCoreRuntime* runtime);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_byte_array_free([NativeTypeName("struct TemporalCoreRuntime *")] TemporalCoreRuntime* runtime, [NativeTypeName("const struct TemporalCoreByteArray *")] TemporalCoreByteArray* bytes);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreByteArrayRef")]
        public static extern TemporalCoreByteArrayRef temporal_core_forwarded_log_target([NativeTypeName("const struct TemporalCoreForwardedLog *")] TemporalCoreForwardedLog* log);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreByteArrayRef")]
        public static extern TemporalCoreByteArrayRef temporal_core_forwarded_log_message([NativeTypeName("const struct TemporalCoreForwardedLog *")] TemporalCoreForwardedLog* log);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("uint64_t")]
        public static extern ulong temporal_core_forwarded_log_timestamp_millis([NativeTypeName("const struct TemporalCoreForwardedLog *")] TemporalCoreForwardedLog* log);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreByteArrayRef")]
        public static extern TemporalCoreByteArrayRef temporal_core_forwarded_log_fields_json([NativeTypeName("const struct TemporalCoreForwardedLog *")] TemporalCoreForwardedLog* log);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_ephemeral_server_start_dev_server([NativeTypeName("struct TemporalCoreRuntime *")] TemporalCoreRuntime* runtime, [NativeTypeName("const struct TemporalCoreDevServerOptions *")] TemporalCoreDevServerOptions* options, void* user_data, [NativeTypeName("TemporalCoreEphemeralServerStartCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_ephemeral_server_start_test_server([NativeTypeName("struct TemporalCoreRuntime *")] TemporalCoreRuntime* runtime, [NativeTypeName("const struct TemporalCoreTestServerOptions *")] TemporalCoreTestServerOptions* options, void* user_data, [NativeTypeName("TemporalCoreEphemeralServerStartCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_ephemeral_server_free([NativeTypeName("struct TemporalCoreEphemeralServer *")] TemporalCoreEphemeralServer* server);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_ephemeral_server_shutdown([NativeTypeName("struct TemporalCoreEphemeralServer *")] TemporalCoreEphemeralServer* server, void* user_data, [NativeTypeName("TemporalCoreEphemeralServerShutdownCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreWorkerOrFail")]
        public static extern TemporalCoreWorkerOrFail temporal_core_worker_new([NativeTypeName("struct TemporalCoreClient *")] TemporalCoreClient* client, [NativeTypeName("const struct TemporalCoreWorkerOptions *")] TemporalCoreWorkerOptions* options);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_free([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_validate([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker, void* user_data, [NativeTypeName("TemporalCoreWorkerCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_replace_client([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker, [NativeTypeName("struct TemporalCoreClient *")] TemporalCoreClient* new_client);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_poll_workflow_activation([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker, void* user_data, [NativeTypeName("TemporalCoreWorkerPollCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_poll_activity_task([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker, void* user_data, [NativeTypeName("TemporalCoreWorkerPollCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_poll_nexus_task([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker, void* user_data, [NativeTypeName("TemporalCoreWorkerPollCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_complete_workflow_activation([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker, [NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef completion, void* user_data, [NativeTypeName("TemporalCoreWorkerCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_complete_activity_task([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker, [NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef completion, void* user_data, [NativeTypeName("TemporalCoreWorkerCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_complete_nexus_task([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker, [NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef completion, void* user_data, [NativeTypeName("TemporalCoreWorkerCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const struct TemporalCoreByteArray *")]
        public static extern TemporalCoreByteArray* temporal_core_worker_record_activity_heartbeat([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker, [NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef heartbeat);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_request_workflow_eviction([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker, [NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef run_id);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_initiate_shutdown([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_finalize_shutdown([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker, void* user_data, [NativeTypeName("TemporalCoreWorkerCallback")] IntPtr callback);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreWorkerReplayerOrFail")]
        public static extern TemporalCoreWorkerReplayerOrFail temporal_core_worker_replayer_new([NativeTypeName("struct TemporalCoreRuntime *")] TemporalCoreRuntime* runtime, [NativeTypeName("const struct TemporalCoreWorkerOptions *")] TemporalCoreWorkerOptions* options);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_worker_replay_pusher_free([NativeTypeName("struct TemporalCoreWorkerReplayPusher *")] TemporalCoreWorkerReplayPusher* worker_replay_pusher);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct TemporalCoreWorkerReplayPushResult")]
        public static extern TemporalCoreWorkerReplayPushResult temporal_core_worker_replay_push([NativeTypeName("struct TemporalCoreWorker *")] TemporalCoreWorker* worker, [NativeTypeName("struct TemporalCoreWorkerReplayPusher *")] TemporalCoreWorkerReplayPusher* worker_replay_pusher, [NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef workflow_id, [NativeTypeName("struct TemporalCoreByteArrayRef")] TemporalCoreByteArrayRef history);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_complete_async_reserve(void* sender, [NativeTypeName("uintptr_t")] UIntPtr permit_id);

        [DllImport("temporal_sdk_core_c_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void temporal_core_set_reserve_cancel_target([NativeTypeName("struct TemporalCoreSlotReserveCtx *")] TemporalCoreSlotReserveCtx* ctx, void* token_ptr);
    }
}
