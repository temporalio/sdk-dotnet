using System;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge.Interop
{
    internal enum ForwardedLogLevel
    {
        Trace = 0,
        Debug,
        Info,
        Warn,
        Error,
    }

    internal enum MetricAttributeValueType
    {
        String = 1,
        Int,
        Float,
        Bool,
    }

    internal enum MetricKind
    {
        CounterInteger = 1,
        HistogramInteger,
        HistogramFloat,
        HistogramDuration,
        GaugeInteger,
        GaugeFloat,
    }

    internal enum OpenTelemetryMetricTemporality
    {
        Cumulative = 1,
        Delta,
    }

    internal enum RpcService
    {
        Workflow = 1,
        Operator,
        Test,
        Health,
    }

    internal partial struct CancellationToken
    {
    }

    internal partial struct Client
    {
    }

    internal partial struct EphemeralServer
    {
    }

    internal partial struct ForwardedLog
    {
    }

    internal partial struct Metric
    {
    }

    internal partial struct MetricAttributes
    {
    }

    internal partial struct MetricMeter
    {
    }

    internal partial struct Random
    {
    }

    internal partial struct Runtime
    {
    }

    internal partial struct Worker
    {
    }

    internal partial struct WorkerReplayPusher
    {
    }

    internal unsafe partial struct ByteArrayRef
    {
        [NativeTypeName("const uint8_t *")]
        public byte* data;

        [NativeTypeName("size_t")]
        public UIntPtr size;
    }

    internal partial struct ClientTlsOptions
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef server_root_ca_cert;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef domain;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef client_cert;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef client_private_key;
    }

    internal partial struct ClientRetryOptions
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

    internal partial struct ClientKeepAliveOptions
    {
        [NativeTypeName("uint64_t")]
        public ulong interval_millis;

        [NativeTypeName("uint64_t")]
        public ulong timeout_millis;
    }

    internal unsafe partial struct ClientOptions
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef target_url;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef client_name;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef client_version;

        [NativeTypeName("MetadataRef")]
        public ByteArrayRef metadata;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef api_key;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef identity;

        [NativeTypeName("const struct ClientTlsOptions *")]
        public ClientTlsOptions* tls_options;

        [NativeTypeName("const struct ClientRetryOptions *")]
        public ClientRetryOptions* retry_options;

        [NativeTypeName("const struct ClientKeepAliveOptions *")]
        public ClientKeepAliveOptions* keep_alive_options;
    }

    internal unsafe partial struct ByteArray
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
    internal unsafe delegate void ClientConnectCallback(void* user_data, [NativeTypeName("struct Client *")] Client* success, [NativeTypeName("const struct ByteArray *")] ByteArray* fail);

    internal unsafe partial struct RpcCallOptions
    {
        [NativeTypeName("enum RpcService")]
        public RpcService service;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef rpc;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef req;

        [NativeTypeName("bool")]
        public byte retry;

        [NativeTypeName("MetadataRef")]
        public ByteArrayRef metadata;

        [NativeTypeName("uint32_t")]
        public uint timeout_millis;

        [NativeTypeName("const struct CancellationToken *")]
        public CancellationToken* cancellation_token;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void ClientRpcCallCallback(void* user_data, [NativeTypeName("const struct ByteArray *")] ByteArray* success, [NativeTypeName("uint32_t")] uint status_code, [NativeTypeName("const struct ByteArray *")] ByteArray* failure_message, [NativeTypeName("const struct ByteArray *")] ByteArray* failure_details);

    [StructLayout(LayoutKind.Explicit)]
    internal partial struct MetricAttributeValue
    {
        [FieldOffset(0)]
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef string_value;

        [FieldOffset(0)]
        [NativeTypeName("int64_t")]
        public long int_value;

        [FieldOffset(0)]
        public double float_value;

        [FieldOffset(0)]
        [NativeTypeName("bool")]
        public byte bool_value;
    }

    internal partial struct MetricAttribute
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef key;

        [NativeTypeName("union MetricAttributeValue")]
        public MetricAttributeValue value;

        [NativeTypeName("enum MetricAttributeValueType")]
        public MetricAttributeValueType value_type;
    }

    internal partial struct MetricOptions
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef name;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef description;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef unit;

        [NativeTypeName("enum MetricKind")]
        public MetricKind kind;
    }

    internal unsafe partial struct RuntimeOrFail
    {
        [NativeTypeName("struct Runtime *")]
        public Runtime* runtime;

        [NativeTypeName("const struct ByteArray *")]
        public ByteArray* fail;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void ForwardedLogCallback([NativeTypeName("enum ForwardedLogLevel")] ForwardedLogLevel level, [NativeTypeName("const struct ForwardedLog *")] ForwardedLog* log);

    internal partial struct LoggingOptions
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef filter;

        [NativeTypeName("ForwardedLogCallback")]
        public IntPtr forward_to;
    }

    internal partial struct OpenTelemetryOptions
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef url;

        [NativeTypeName("MetadataRef")]
        public ByteArrayRef headers;

        [NativeTypeName("uint32_t")]
        public uint metric_periodicity_millis;

        [NativeTypeName("enum OpenTelemetryMetricTemporality")]
        public OpenTelemetryMetricTemporality metric_temporality;

        [NativeTypeName("bool")]
        public byte durations_as_seconds;
    }

    internal partial struct PrometheusOptions
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef bind_address;

        [NativeTypeName("bool")]
        public byte counters_total_suffix;

        [NativeTypeName("bool")]
        public byte unit_suffix;

        [NativeTypeName("bool")]
        public byte durations_as_seconds;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("const void *")]
    internal unsafe delegate void* CustomMetricMeterMetricNewCallback([NativeTypeName("struct ByteArrayRef")] ByteArrayRef name, [NativeTypeName("struct ByteArrayRef")] ByteArrayRef description, [NativeTypeName("struct ByteArrayRef")] ByteArrayRef unit, [NativeTypeName("enum MetricKind")] MetricKind kind);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void CustomMetricMeterMetricFreeCallback([NativeTypeName("const void *")] void* metric);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void CustomMetricMeterMetricRecordIntegerCallback([NativeTypeName("const void *")] void* metric, [NativeTypeName("uint64_t")] ulong value, [NativeTypeName("const void *")] void* attributes);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void CustomMetricMeterMetricRecordFloatCallback([NativeTypeName("const void *")] void* metric, double value, [NativeTypeName("const void *")] void* attributes);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void CustomMetricMeterMetricRecordDurationCallback([NativeTypeName("const void *")] void* metric, [NativeTypeName("uint64_t")] ulong value_ms, [NativeTypeName("const void *")] void* attributes);

    internal unsafe partial struct CustomMetricAttributeValueString
    {
        [NativeTypeName("const uint8_t *")]
        public byte* data;

        [NativeTypeName("size_t")]
        public UIntPtr size;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal partial struct CustomMetricAttributeValue
    {
        [FieldOffset(0)]
        [NativeTypeName("struct CustomMetricAttributeValueString")]
        public CustomMetricAttributeValueString string_value;

        [FieldOffset(0)]
        [NativeTypeName("int64_t")]
        public long int_value;

        [FieldOffset(0)]
        public double float_value;

        [FieldOffset(0)]
        [NativeTypeName("bool")]
        public byte bool_value;
    }

    internal partial struct CustomMetricAttribute
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef key;

        [NativeTypeName("union CustomMetricAttributeValue")]
        public CustomMetricAttributeValue value;

        [NativeTypeName("enum MetricAttributeValueType")]
        public MetricAttributeValueType value_type;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("const void *")]
    internal unsafe delegate void* CustomMetricMeterAttributesNewCallback([NativeTypeName("const void *")] void* append_from, [NativeTypeName("const struct CustomMetricAttribute *")] CustomMetricAttribute* attributes, [NativeTypeName("size_t")] UIntPtr attributes_size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void CustomMetricMeterAttributesFreeCallback([NativeTypeName("const void *")] void* attributes);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void CustomMetricMeterMeterFreeCallback([NativeTypeName("const struct CustomMetricMeter *")] CustomMetricMeter* meter);

    internal partial struct CustomMetricMeter
    {
        [NativeTypeName("CustomMetricMeterMetricNewCallback")]
        public IntPtr metric_new;

        [NativeTypeName("CustomMetricMeterMetricFreeCallback")]
        public IntPtr metric_free;

        [NativeTypeName("CustomMetricMeterMetricRecordIntegerCallback")]
        public IntPtr metric_record_integer;

        [NativeTypeName("CustomMetricMeterMetricRecordFloatCallback")]
        public IntPtr metric_record_float;

        [NativeTypeName("CustomMetricMeterMetricRecordDurationCallback")]
        public IntPtr metric_record_duration;

        [NativeTypeName("CustomMetricMeterAttributesNewCallback")]
        public IntPtr attributes_new;

        [NativeTypeName("CustomMetricMeterAttributesFreeCallback")]
        public IntPtr attributes_free;

        [NativeTypeName("CustomMetricMeterMeterFreeCallback")]
        public IntPtr meter_free;
    }

    internal unsafe partial struct MetricsOptions
    {
        [NativeTypeName("const struct OpenTelemetryOptions *")]
        public OpenTelemetryOptions* opentelemetry;

        [NativeTypeName("const struct PrometheusOptions *")]
        public PrometheusOptions* prometheus;

        [NativeTypeName("const struct CustomMetricMeter *")]
        public CustomMetricMeter* custom_meter;

        [NativeTypeName("bool")]
        public byte attach_service_name;

        [NativeTypeName("MetadataRef")]
        public ByteArrayRef global_tags;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef metric_prefix;
    }

    internal unsafe partial struct TelemetryOptions
    {
        [NativeTypeName("const struct LoggingOptions *")]
        public LoggingOptions* logging;

        [NativeTypeName("const struct MetricsOptions *")]
        public MetricsOptions* metrics;
    }

    internal unsafe partial struct RuntimeOptions
    {
        [NativeTypeName("const struct TelemetryOptions *")]
        public TelemetryOptions* telemetry;
    }

    internal partial struct TestServerOptions
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef existing_path;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef sdk_name;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef sdk_version;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef download_version;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef download_dest_dir;

        [NativeTypeName("uint16_t")]
        public ushort port;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef extra_args;
    }

    internal unsafe partial struct DevServerOptions
    {
        [NativeTypeName("const struct TestServerOptions *")]
        public TestServerOptions* test_server;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef namespace_;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef ip;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef database_filename;

        [NativeTypeName("bool")]
        public byte ui;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef log_format;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef log_level;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void EphemeralServerStartCallback(void* user_data, [NativeTypeName("struct EphemeralServer *")] EphemeralServer* success, [NativeTypeName("const struct ByteArray *")] ByteArray* success_target, [NativeTypeName("const struct ByteArray *")] ByteArray* fail);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void EphemeralServerShutdownCallback(void* user_data, [NativeTypeName("const struct ByteArray *")] ByteArray* fail);

    internal unsafe partial struct WorkerOrFail
    {
        [NativeTypeName("struct Worker *")]
        public Worker* worker;

        [NativeTypeName("const struct ByteArray *")]
        public ByteArray* fail;
    }

    internal unsafe partial struct ByteArrayRefArray
    {
        [NativeTypeName("const struct ByteArrayRef *")]
        public ByteArrayRef* data;

        [NativeTypeName("size_t")]
        public UIntPtr size;
    }

    internal partial struct WorkerOptions
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef namespace_;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef task_queue;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef build_id;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef identity_override;

        [NativeTypeName("uint32_t")]
        public uint max_cached_workflows;

        [NativeTypeName("uint32_t")]
        public uint max_outstanding_workflow_tasks;

        [NativeTypeName("uint32_t")]
        public uint max_outstanding_activities;

        [NativeTypeName("uint32_t")]
        public uint max_outstanding_local_activities;

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

        [NativeTypeName("bool")]
        public byte use_worker_versioning;

        [NativeTypeName("uint32_t")]
        public uint max_concurrent_workflow_task_polls;

        public float nonsticky_to_sticky_poll_ratio;

        [NativeTypeName("uint32_t")]
        public uint max_concurrent_activity_task_polls;

        [NativeTypeName("bool")]
        public byte nondeterminism_as_workflow_fail;

        [NativeTypeName("struct ByteArrayRefArray")]
        public ByteArrayRefArray nondeterminism_as_workflow_fail_for_types;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void WorkerPollCallback(void* user_data, [NativeTypeName("const struct ByteArray *")] ByteArray* success, [NativeTypeName("const struct ByteArray *")] ByteArray* fail);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void WorkerCallback(void* user_data, [NativeTypeName("const struct ByteArray *")] ByteArray* fail);

    internal unsafe partial struct WorkerReplayerOrFail
    {
        [NativeTypeName("struct Worker *")]
        public Worker* worker;

        [NativeTypeName("struct WorkerReplayPusher *")]
        public WorkerReplayPusher* worker_replay_pusher;

        [NativeTypeName("const struct ByteArray *")]
        public ByteArray* fail;
    }

    internal unsafe partial struct WorkerReplayPushResult
    {
        [NativeTypeName("const struct ByteArray *")]
        public ByteArray* fail;
    }

    internal static unsafe partial class Methods
    {
        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct CancellationToken *")]
        public static extern CancellationToken* cancellation_token_new();

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void cancellation_token_cancel([NativeTypeName("struct CancellationToken *")] CancellationToken* token);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void cancellation_token_free([NativeTypeName("struct CancellationToken *")] CancellationToken* token);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void client_connect([NativeTypeName("struct Runtime *")] Runtime* runtime, [NativeTypeName("const struct ClientOptions *")] ClientOptions* options, void* user_data, [NativeTypeName("ClientConnectCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void client_free([NativeTypeName("struct Client *")] Client* client);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void client_update_metadata([NativeTypeName("struct Client *")] Client* client, [NativeTypeName("struct ByteArrayRef")] ByteArrayRef metadata);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void client_update_api_key([NativeTypeName("struct Client *")] Client* client, [NativeTypeName("struct ByteArrayRef")] ByteArrayRef api_key);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void client_rpc_call([NativeTypeName("struct Client *")] Client* client, [NativeTypeName("const struct RpcCallOptions *")] RpcCallOptions* options, void* user_data, [NativeTypeName("ClientRpcCallCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct MetricMeter *")]
        public static extern MetricMeter* metric_meter_new([NativeTypeName("struct Runtime *")] Runtime* runtime);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void metric_meter_free([NativeTypeName("struct MetricMeter *")] MetricMeter* meter);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct MetricAttributes *")]
        public static extern MetricAttributes* metric_attributes_new([NativeTypeName("const struct MetricMeter *")] MetricMeter* meter, [NativeTypeName("const struct MetricAttribute *")] MetricAttribute* attrs, [NativeTypeName("size_t")] UIntPtr size);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct MetricAttributes *")]
        public static extern MetricAttributes* metric_attributes_new_append([NativeTypeName("const struct MetricMeter *")] MetricMeter* meter, [NativeTypeName("const struct MetricAttributes *")] MetricAttributes* orig, [NativeTypeName("const struct MetricAttribute *")] MetricAttribute* attrs, [NativeTypeName("size_t")] UIntPtr size);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void metric_attributes_free([NativeTypeName("struct MetricAttributes *")] MetricAttributes* attrs);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct Metric *")]
        public static extern Metric* metric_new([NativeTypeName("const struct MetricMeter *")] MetricMeter* meter, [NativeTypeName("const struct MetricOptions *")] MetricOptions* options);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void metric_free([NativeTypeName("struct Metric *")] Metric* metric);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void metric_record_integer([NativeTypeName("const struct Metric *")] Metric* metric, [NativeTypeName("uint64_t")] ulong value, [NativeTypeName("const struct MetricAttributes *")] MetricAttributes* attrs);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void metric_record_float([NativeTypeName("const struct Metric *")] Metric* metric, double value, [NativeTypeName("const struct MetricAttributes *")] MetricAttributes* attrs);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void metric_record_duration([NativeTypeName("const struct Metric *")] Metric* metric, [NativeTypeName("uint64_t")] ulong value_ms, [NativeTypeName("const struct MetricAttributes *")] MetricAttributes* attrs);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct Random *")]
        public static extern Random* random_new([NativeTypeName("uint64_t")] ulong seed);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void random_free([NativeTypeName("struct Random *")] Random* random);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("int32_t")]
        public static extern int random_int32_range([NativeTypeName("struct Random *")] Random* random, [NativeTypeName("int32_t")] int min, [NativeTypeName("int32_t")] int max, [NativeTypeName("bool")] byte max_inclusive);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double random_double_range([NativeTypeName("struct Random *")] Random* random, double min, double max, [NativeTypeName("bool")] byte max_inclusive);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void random_fill_bytes([NativeTypeName("struct Random *")] Random* random, [NativeTypeName("struct ByteArrayRef")] ByteArrayRef bytes);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct RuntimeOrFail")]
        public static extern RuntimeOrFail runtime_new([NativeTypeName("const struct RuntimeOptions *")] RuntimeOptions* options);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void runtime_free([NativeTypeName("struct Runtime *")] Runtime* runtime);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void byte_array_free([NativeTypeName("struct Runtime *")] Runtime* runtime, [NativeTypeName("const struct ByteArray *")] ByteArray* bytes);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct ByteArrayRef")]
        public static extern ByteArrayRef forwarded_log_target([NativeTypeName("const struct ForwardedLog *")] ForwardedLog* log);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct ByteArrayRef")]
        public static extern ByteArrayRef forwarded_log_message([NativeTypeName("const struct ForwardedLog *")] ForwardedLog* log);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("uint64_t")]
        public static extern ulong forwarded_log_timestamp_millis([NativeTypeName("const struct ForwardedLog *")] ForwardedLog* log);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct ByteArrayRef")]
        public static extern ByteArrayRef forwarded_log_fields_json([NativeTypeName("const struct ForwardedLog *")] ForwardedLog* log);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void ephemeral_server_start_dev_server([NativeTypeName("struct Runtime *")] Runtime* runtime, [NativeTypeName("const struct DevServerOptions *")] DevServerOptions* options, void* user_data, [NativeTypeName("EphemeralServerStartCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void ephemeral_server_start_test_server([NativeTypeName("struct Runtime *")] Runtime* runtime, [NativeTypeName("const struct TestServerOptions *")] TestServerOptions* options, void* user_data, [NativeTypeName("EphemeralServerStartCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void ephemeral_server_free([NativeTypeName("struct EphemeralServer *")] EphemeralServer* server);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void ephemeral_server_shutdown([NativeTypeName("struct EphemeralServer *")] EphemeralServer* server, void* user_data, [NativeTypeName("EphemeralServerShutdownCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct WorkerOrFail")]
        public static extern WorkerOrFail worker_new([NativeTypeName("struct Client *")] Client* client, [NativeTypeName("const struct WorkerOptions *")] WorkerOptions* options);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void worker_free([NativeTypeName("struct Worker *")] Worker* worker);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void worker_replace_client([NativeTypeName("struct Worker *")] Worker* worker, [NativeTypeName("struct Client *")] Client* new_client);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void worker_poll_workflow_activation([NativeTypeName("struct Worker *")] Worker* worker, void* user_data, [NativeTypeName("WorkerPollCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void worker_poll_activity_task([NativeTypeName("struct Worker *")] Worker* worker, void* user_data, [NativeTypeName("WorkerPollCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void worker_complete_workflow_activation([NativeTypeName("struct Worker *")] Worker* worker, [NativeTypeName("struct ByteArrayRef")] ByteArrayRef completion, void* user_data, [NativeTypeName("WorkerCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void worker_complete_activity_task([NativeTypeName("struct Worker *")] Worker* worker, [NativeTypeName("struct ByteArrayRef")] ByteArrayRef completion, void* user_data, [NativeTypeName("WorkerCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const struct ByteArray *")]
        public static extern ByteArray* worker_record_activity_heartbeat([NativeTypeName("struct Worker *")] Worker* worker, [NativeTypeName("struct ByteArrayRef")] ByteArrayRef heartbeat);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void worker_request_workflow_eviction([NativeTypeName("struct Worker *")] Worker* worker, [NativeTypeName("struct ByteArrayRef")] ByteArrayRef run_id);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void worker_initiate_shutdown([NativeTypeName("struct Worker *")] Worker* worker);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void worker_finalize_shutdown([NativeTypeName("struct Worker *")] Worker* worker, void* user_data, [NativeTypeName("WorkerCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct WorkerReplayerOrFail")]
        public static extern WorkerReplayerOrFail worker_replayer_new([NativeTypeName("struct Runtime *")] Runtime* runtime, [NativeTypeName("const struct WorkerOptions *")] WorkerOptions* options);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void worker_replay_pusher_free([NativeTypeName("struct WorkerReplayPusher *")] WorkerReplayPusher* worker_replay_pusher);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct WorkerReplayPushResult")]
        public static extern WorkerReplayPushResult worker_replay_push([NativeTypeName("struct Worker *")] Worker* worker, [NativeTypeName("struct WorkerReplayPusher *")] WorkerReplayPusher* worker_replay_pusher, [NativeTypeName("struct ByteArrayRef")] ByteArrayRef workflow_id, [NativeTypeName("struct ByteArrayRef")] ByteArrayRef history);
    }
}
