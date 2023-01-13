using System;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge.Interop
{
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

    internal partial struct Runtime
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
        public ByteArrayRef identity;

        [NativeTypeName("const struct ClientTlsOptions *")]
        public ClientTlsOptions* tls_options;

        [NativeTypeName("const struct ClientRetryOptions *")]
        public ClientRetryOptions* retry_options;
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
    internal unsafe delegate void ClientRpcCallCallback(void* user_data, [NativeTypeName("const struct ByteArray *")] ByteArray* success, [NativeTypeName("const struct ByteArray *")] ByteArray* fail);

    internal unsafe partial struct RuntimeOrFail
    {
        [NativeTypeName("struct Runtime *")]
        public Runtime* runtime;

        [NativeTypeName("const struct ByteArray *")]
        public ByteArray* fail;
    }

    internal partial struct OpenTelemetryOptions
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef url;

        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef headers;

        [NativeTypeName("uint32_t")]
        public uint metric_periodicity_millis;
    }

    internal partial struct TracingOptions
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef filter;

        [NativeTypeName("struct OpenTelemetryOptions")]
        public OpenTelemetryOptions opentelemetry;
    }

    internal partial struct LoggingOptions
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef filter;

        [NativeTypeName("bool")]
        public byte forward;
    }

    internal partial struct PrometheusOptions
    {
        [NativeTypeName("struct ByteArrayRef")]
        public ByteArrayRef bind_address;
    }

    internal unsafe partial struct MetricsOptions
    {
        [NativeTypeName("const struct OpenTelemetryOptions *")]
        public OpenTelemetryOptions* opentelemetry;

        [NativeTypeName("const struct PrometheusOptions *")]
        public PrometheusOptions* prometheus;
    }

    internal unsafe partial struct TelemetryOptions
    {
        [NativeTypeName("const struct TracingOptions *")]
        public TracingOptions* tracing;

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

    internal unsafe partial struct TemporaliteOptions
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
        public static extern void client_rpc_call([NativeTypeName("struct Client *")] Client* client, [NativeTypeName("const struct RpcCallOptions *")] RpcCallOptions* options, void* user_data, [NativeTypeName("ClientRpcCallCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct RuntimeOrFail")]
        public static extern RuntimeOrFail runtime_new([NativeTypeName("const struct RuntimeOptions *")] RuntimeOptions* options);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void runtime_free([NativeTypeName("struct Runtime *")] Runtime* runtime);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void byte_array_free([NativeTypeName("struct Runtime *")] Runtime* runtime, [NativeTypeName("const struct ByteArray *")] ByteArray* bytes);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void ephemeral_server_start_temporalite([NativeTypeName("struct Runtime *")] Runtime* runtime, [NativeTypeName("const struct TemporaliteOptions *")] TemporaliteOptions* options, void* user_data, [NativeTypeName("EphemeralServerStartCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void ephemeral_server_start_test_server([NativeTypeName("struct Runtime *")] Runtime* runtime, [NativeTypeName("const struct TestServerOptions *")] TestServerOptions* options, void* user_data, [NativeTypeName("EphemeralServerStartCallback")] IntPtr callback);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void ephemeral_server_free([NativeTypeName("struct EphemeralServer *")] EphemeralServer* server);

        [DllImport("temporal_sdk_bridge", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void ephemeral_server_shutdown([NativeTypeName("struct EphemeralServer *")] EphemeralServer* server, void* user_data, [NativeTypeName("EphemeralServerShutdownCallback")] IntPtr callback);
    }
}
