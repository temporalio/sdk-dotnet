using System;
using System.Reflection;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Extension methods for converting high-level options classes to the lower-level interop
    /// structs expected by Core.
    /// </summary>
    internal static class OptionsExtensions
    {
        private static readonly ByteArrayRef ClientName = ByteArrayRef.FromUTF8("temporal-dotnet");
        private static readonly ByteArrayRef ClientVersion = ByteArrayRef.FromUTF8(
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "<unknown>");

        private static readonly ByteArrayRef SdkName = ByteArrayRef.FromUTF8("sdk-dotnet");

        /// <summary>
        /// Convert runtime options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static unsafe Interop.RuntimeOptions ToInteropOptions(
            this Temporalio.Runtime.TemporalRuntimeOptions options,
            Scope scope)
        {
            return new Interop.RuntimeOptions()
            {
                telemetry = scope.Pointer(options.Telemetry.ToInteropOptions(scope)),
            };
        }

        /// <summary>
        /// Convert telemetry options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static unsafe Interop.TelemetryOptions ToInteropOptions(
            this Temporalio.Runtime.TelemetryOptions options,
            Scope scope)
        {
            return new Interop.TelemetryOptions()
            {
                tracing =
                    options.Tracing == null
                        ? null
                        : scope.Pointer(options.Tracing.ToInteropOptions(scope)),
                logging =
                    options.Logging == null
                        ? null
                        : scope.Pointer(options.Logging.ToInteropOptions(scope)),
                metrics =
                    options.Metrics == null
                        ? null
                        : scope.Pointer(options.Metrics.ToInteropOptions(scope)),
            };
        }

        /// <summary>
        /// Convert tracing options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static unsafe Interop.TracingOptions ToInteropOptions(
            this Temporalio.Runtime.TracingOptions options,
            Scope scope)
        {
            if (string.IsNullOrEmpty(options.Filter.FilterString))
            {
                throw new ArgumentException("Tracing filter string is required");
            }

            return new Interop.TracingOptions()
            {
                filter = scope.ByteArray(options.Filter.FilterString),
                opentelemetry = options.OpenTelemetry.ToInteropOptions(scope),
            };
        }

        /// <summary>
        /// Convert OpenTelemetry options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static unsafe Interop.OpenTelemetryOptions ToInteropOptions(
            this Temporalio.Runtime.OpenTelemetryOptions options,
            Scope scope)
        {
            if (options.Url == null)
            {
                throw new ArgumentException("OpenTelemetry URL is required");
            }
            return new Interop.OpenTelemetryOptions()
            {
                url = scope.ByteArray(options.Url.ToString()),
                headers = scope.Metadata(options.Headers),
                metric_periodicity_millis = (uint)(
                    options.MetricsExportInterval == null
                        ? 0
                        : options.MetricsExportInterval.Value.TotalMilliseconds),
            };
        }

        /// <summary>
        /// Convert logging options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static unsafe Interop.LoggingOptions ToInteropOptions(
            this Temporalio.Runtime.LoggingOptions options,
            Scope scope)
        {
            if (string.IsNullOrEmpty(options.Filter.FilterString))
            {
                throw new ArgumentException("Logging filter string is required");
            }
            return new Interop.LoggingOptions()
            {
                filter = scope.ByteArray(options.Filter.FilterString),
                forward = (byte)0,
            };
        }

        /// <summary>
        /// Convert metrics options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static unsafe Interop.MetricsOptions ToInteropOptions(
            this Temporalio.Runtime.MetricsOptions options,
            Scope scope)
        {
            Interop.PrometheusOptions* prometheus = null;
            Interop.OpenTelemetryOptions* openTelemetry = null;
            if (options.Prometheus != null)
            {
                if (options.OpenTelemetry != null)
                {
                    throw new ArgumentException(
                        "Cannot have Prometheus and OpenTelemetry metrics options");
                }
                if (string.IsNullOrEmpty(options.Prometheus.BindAddress))
                {
                    throw new ArgumentException("Prometheus options must have bind address");
                }
                prometheus = scope.Pointer(
                    new Interop.PrometheusOptions()
                    {
                        bind_address = scope.ByteArray(options.Prometheus.BindAddress),
                    });
            }
            else if (options.OpenTelemetry != null)
            {
                openTelemetry = scope.Pointer(options.OpenTelemetry.ToInteropOptions(scope));
            }
            else
            {
                throw new ArgumentException(
                    "Must have either Prometheus or OpenTelemetry metrics options");
            }
            return new Interop.MetricsOptions()
            {
                prometheus = prometheus,
                opentelemetry = openTelemetry,
            };
        }

        /// <summary>
        /// Convert connection options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static unsafe Interop.ClientOptions ToInteropOptions(
            this Temporalio.Client.TemporalConnectionOptions options,
            Scope scope)
        {
            if (string.IsNullOrEmpty(options.TargetHost))
            {
                throw new ArgumentException("TargetHost is required");
            }
            else if (options.TargetHost!.Contains("://"))
            {
                throw new ArgumentException("TargetHost cannot have ://");
            }
            else if (options.Identity == null)
            {
                throw new ArgumentException("Identity missing from options.");
            }
            var scheme = options.Tls == null ? "http" : "https";
            return new Interop.ClientOptions()
            {
                target_url = scope.ByteArray($"{scheme}://{options.TargetHost}"),
                client_name = ClientName.Ref,
                client_version = ClientVersion.Ref,
                metadata = scope.Metadata(options.RpcMetadata),
                identity = scope.ByteArray(options.Identity),
                tls_options =
                    options.Tls == null ? null : scope.Pointer(options.Tls.ToInteropOptions(scope)),
                retry_options =
                    options.RpcRetry == null
                        ? null
                        : scope.Pointer(options.RpcRetry.ToInteropOptions()),
            };
        }

        /// <summary>
        /// Convert TLS options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static Interop.ClientTlsOptions ToInteropOptions(
            this Temporalio.Client.TlsOptions options,
            Scope scope)
        {
            var hasClientCert = options.ClientCert != null && options.ClientCert.Length > 0;
            var hasClientKey =
                options.ClientPrivateKey != null && options.ClientPrivateKey.Length > 0;
            if (hasClientCert != hasClientKey)
            {
                throw new ArgumentException(
                    "Client cert and private key must both be present or neither");
            }
            return new Interop.ClientTlsOptions()
            {
                server_root_ca_cert = scope.ByteArray(options.ServerRootCACert),
                domain = scope.ByteArray(options.Domain),
                client_cert = scope.ByteArray(options.ClientCert),
                client_private_key = scope.ByteArray(options.ClientPrivateKey),
            };
        }

        /// <summary>
        /// Convert retry options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <returns>Converted options.</returns>
        public static Interop.ClientRetryOptions ToInteropOptions(
            this Temporalio.Client.RpcRetryOptions options)
        {
            return new Interop.ClientRetryOptions()
            {
                initial_interval_millis = (ulong)options.InitialInterval.TotalMilliseconds,
                randomization_factor = options.RandomizationFactor,
                multiplier = options.Multiplier,
                max_interval_millis = (ulong)options.MaxInterval.TotalMilliseconds,
                max_elapsed_time_millis = (ulong)(
                    options.MaxElapsedTime == null
                        ? 0
                        : options.MaxElapsedTime.Value.TotalMilliseconds),
                max_retries = (UIntPtr)options.MaxRetries,
            };
        }

        /// <summary>
        /// Convert start local options options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static unsafe Interop.TemporaliteOptions ToInteropOptions(
            this Testing.WorkflowEnvironmentStartLocalOptions options,
            Scope scope)
        {
            // Use TargetHost to get IP + Port
            options.ParseTargetHost(out string? ip, out int? port);
            ip ??= "127.0.0.1";
            return new Interop.TemporaliteOptions()
            {
                test_server = scope.Pointer(
                    new Interop.TestServerOptions()
                    {
                        existing_path = scope.ByteArray(options.Temporalite.ExistingPath),
                        sdk_name = SdkName.Ref,
                        sdk_version = ClientVersion.Ref,
                        download_version = scope.ByteArray(options.Temporalite.DownloadVersion),
                        download_dest_dir = scope.ByteArray(options.DownloadDirectory),
                        port = (ushort)(port ?? 0),
                        extra_args = scope.NewlineDelimited(options.Temporalite.ExtraArgs),
                    }),
                namespace_ = scope.ByteArray(options.Namespace),
                ip = scope.ByteArray(ip),
                database_filename = scope.ByteArray(options.Temporalite.DatabaseFilename),
                ui = (byte)(options.UI ? 1 : 0),
                log_format = scope.ByteArray(options.Temporalite.LogFormat),
                log_level = scope.ByteArray(options.Temporalite.LogLevel),
            };
        }

        /// <summary>
        /// Convert time skipping options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static Interop.TestServerOptions ToInteropOptions(
            this Testing.WorkflowEnvironmentStartTimeSkippingOptions options,
            Scope scope)
        {
            // Use TargetHost to get IP + Port
            options.ParseTargetHost(out string? ip, out int? port);
            if (!string.IsNullOrEmpty(ip) && ip != "127.0.0.1" && ip != "localhost")
            {
                throw new InvalidOperationException(
                    "TargetHost can only specify empty, localhost, or 127.0.0.1 host");
            }
            return new()
            {
                existing_path = scope.ByteArray(options.TestServer.ExistingPath),
                sdk_name = SdkName.Ref,
                sdk_version = ClientVersion.Ref,
                download_version = scope.ByteArray(options.TestServer.DownloadVersion),
                download_dest_dir = scope.ByteArray(options.DownloadDirectory),
                port = (ushort)(port ?? 0),
                extra_args = scope.NewlineDelimited(options.TestServer.ExtraArgs),
            };
        }

        /// <summary>
        /// Convert worker options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <param name="namespace_">Namespace for the worker.</param>
        /// <returns>Converted options.</returns>
        public static Interop.WorkerOptions ToInteropOptions(
            this Temporalio.Worker.TemporalWorkerOptions options,
            Scope scope,
            string namespace_)
        {
            if (options.TaskQueue == null)
            {
                throw new ArgumentException("Task queue must be provided in worker options");
            }
            var buildID = options.BuildID;
            if (buildID == null)
            {
                var entryAssembly = Assembly.GetEntryAssembly() ??
                    throw new ArgumentException("Unable to get assembly manifest ID for build ID");
                buildID = entryAssembly.ManifestModule.ModuleVersionId.ToString();
            }
            // We have to disable remote activities if a user asks _or_ if we are not running an
            // activity worker at all. Otherwise shutdown will not proceed properly.
            var noRemoteActivities = options.LocalActivityWorkerOnly || options.Activities.Count == 0;
            return new()
            {
                namespace_ = scope.ByteArray(namespace_),
                task_queue = scope.ByteArray(options.TaskQueue),
                build_id = scope.ByteArray(buildID),
                identity_override = scope.ByteArray(options.Identity),
                max_cached_workflows = (uint)options.MaxCachedWorkflows,
                max_outstanding_workflow_tasks = (uint)options.MaxConcurrentWorkflowTasks,
                max_outstanding_activities = (uint)options.MaxConcurrentActivities,
                max_outstanding_local_activities = (uint)options.MaxConcurrentLocalActivities,
                no_remote_activities = (byte)(noRemoteActivities ? 1 : 0),
                sticky_queue_schedule_to_start_timeout_millis =
                    (ulong)options.StickyQueueScheduleToStartTimeout.TotalMilliseconds,
                max_heartbeat_throttle_interval_millis =
                    (ulong)options.MaxHeartbeatThrottleInterval.TotalMilliseconds,
                default_heartbeat_throttle_interval_millis =
                    (ulong)options.DefaultHeartbeatThrottleInterval.TotalMilliseconds,
                max_activities_per_second = options.MaxActivitiesPerSecond ?? 0,
                max_task_queue_activities_per_second = options.MaxTaskQueueActivitiesPerSecond ?? 0,
                graceful_shutdown_period_millis =
                    (ulong)options.GracefulShutdownTimeout.TotalMilliseconds,
            };
        }

        /// <summary>
        /// Convert replayer options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static Interop.WorkerOptions ToInteropOptions(
            this Temporalio.Worker.WorkflowReplayerOptions options, Scope scope)
        {
            var buildID = options.BuildID;
            if (buildID == null)
            {
                var entryAssembly = Assembly.GetEntryAssembly() ??
                    throw new ArgumentException("Unable to get assembly manifest ID for build ID");
                buildID = entryAssembly.ManifestModule.ModuleVersionId.ToString();
            }
            return new()
            {
                namespace_ = scope.ByteArray(options.Namespace),
                task_queue = scope.ByteArray(options.TaskQueue),
                build_id = scope.ByteArray(buildID),
                identity_override = scope.ByteArray(options.Identity),
                max_cached_workflows = 1,
                max_outstanding_workflow_tasks = 1,
                max_outstanding_activities = 1,
                max_outstanding_local_activities = 1,
                no_remote_activities = 1,
                sticky_queue_schedule_to_start_timeout_millis = 1000,
                max_heartbeat_throttle_interval_millis = 1000,
                default_heartbeat_throttle_interval_millis = 1000,
                max_activities_per_second = 0,
                max_task_queue_activities_per_second = 0,
                graceful_shutdown_period_millis = 0,
            };
        }
    }
}
