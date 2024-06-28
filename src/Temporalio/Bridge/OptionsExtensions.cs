using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Temporalio.Exceptions;

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
            Interop.OpenTelemetryMetricTemporality temporality;
            switch (options.MetricTemporality)
            {
                case Temporalio.Runtime.OpenTelemetryMetricTemporality.Cumulative:
                    temporality = Interop.OpenTelemetryMetricTemporality.Cumulative;
                    break;
                case Temporalio.Runtime.OpenTelemetryMetricTemporality.Delta:
                    temporality = Interop.OpenTelemetryMetricTemporality.Delta;
                    break;
                default:
                    throw new ArgumentException("Unrecognized temporality");
            }
            return new Interop.OpenTelemetryOptions()
            {
                url = scope.ByteArray(options.Url.ToString()),
                headers = scope.Metadata(options.Headers),
                metric_periodicity_millis = (uint)(
                    options.MetricsExportInterval == null
                        ? 0
                        : options.MetricsExportInterval.Value.TotalMilliseconds),
                metric_temporality = temporality,
                durations_as_seconds = (byte)(options.UseSecondsForDuration ? 1 : 0),
            };
        }

        /// <summary>
        /// Convert Prometheus options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static unsafe Interop.PrometheusOptions ToInteropOptions(
            this Temporalio.Runtime.PrometheusOptions options,
            Scope scope)
        {
            if (string.IsNullOrEmpty(options.BindAddress))
            {
                throw new ArgumentException("Prometheus options must have bind address");
            }
            return new Interop.PrometheusOptions()
            {
                bind_address = scope.ByteArray(options.BindAddress),
                counters_total_suffix = (byte)(options.HasCounterTotalSuffix ? 1 : 0),
                unit_suffix = (byte)(options.HasUnitSuffix ? 1 : 0),
                durations_as_seconds = (byte)(options.UseSecondsForDuration ? 1 : 0),
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
                // Forward callback is set in the Runtime constructor
                // forward_to = <set-elsewhere>
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
            Interop.CustomMetricMeter* customMeter = null;
            if (options.Prometheus != null)
            {
                if (options.OpenTelemetry != null || options.CustomMetricMeter != null)
                {
                    throw new ArgumentException(
                        "Cannot have Prometheus and OpenTelemetry/CustomMetricMeter metrics options");
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
                if (options.CustomMetricMeter != null)
                {
                    throw new ArgumentException(
                        "Cannot have OpenTelemetry and CustomMetricMeter metrics options");
                }
                openTelemetry = scope.Pointer(options.OpenTelemetry.ToInteropOptions(scope));
            }
            else if (options.CustomMetricMeter != null)
            {
                // This object pins itself in memory and is only freed on the Rust side
                customMeter = new CustomMetricMeter(
                    options.CustomMetricMeter, options.CustomMetricMeterOptions ?? new()).Ptr;
            }
            else
            {
                throw new ArgumentException(
                    "Must have either Prometheus or OpenTelemetry metrics options");
            }
            // WARNING: It is important that nothing after this point throws, because we have
            // allocated a pointer for the custom meter which can only be freed on the Rust side
            return new Interop.MetricsOptions()
            {
                prometheus = prometheus,
                opentelemetry = openTelemetry,
                custom_meter = customMeter,
                attach_service_name = (byte)(options.AttachServiceName ? 1 : 0),
                global_tags = scope.Metadata(options.GlobalTags),
                metric_prefix = scope.ByteArray(options.MetricPrefix),
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
                api_key = scope.ByteArray(options.ApiKey),
                identity = scope.ByteArray(options.Identity),
                tls_options =
                    options.Tls == null ? null : scope.Pointer(options.Tls.ToInteropOptions(scope)),
                retry_options =
                    options.RpcRetry == null
                        ? null
                        : scope.Pointer(options.RpcRetry.ToInteropOptions()),
                keep_alive_options =
                    options.KeepAlive == null
                        ? null
                        : scope.Pointer(options.KeepAlive.ToInteropOptions()),
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
        /// Convert keep alive options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <returns>Converted options.</returns>
        public static Interop.ClientKeepAliveOptions ToInteropOptions(
            this Temporalio.Client.KeepAliveOptions options) =>
            new()
            {
                interval_millis = (ulong)options.Interval.TotalMilliseconds,
                timeout_millis = (ulong)options.Timeout.TotalMilliseconds,
            };

        /// <summary>
        /// Convert start local options options.
        /// </summary>
        /// <param name="options">Options to convert.</param>
        /// <param name="scope">Scope to use.</param>
        /// <returns>Converted options.</returns>
        public static unsafe Interop.DevServerOptions ToInteropOptions(
            this Testing.WorkflowEnvironmentStartLocalOptions options,
            Scope scope)
        {
            // Use TargetHost to get IP + Port
            options.ParseTargetHost(out string? ip, out int? port);
            ip ??= "127.0.0.1";
            return new Interop.DevServerOptions()
            {
                test_server = scope.Pointer(
                    new Interop.TestServerOptions()
                    {
                        existing_path = scope.ByteArray(options.DevServerOptions.ExistingPath),
                        sdk_name = SdkName.Ref,
                        sdk_version = ClientVersion.Ref,
                        download_version = scope.ByteArray(options.DevServerOptions.DownloadVersion),
                        download_dest_dir = scope.ByteArray(options.DownloadDirectory),
                        port = (ushort)(port ?? 0),
                        extra_args = scope.NewlineDelimited(options.DevServerOptions.ExtraArgs),
                    }),
                namespace_ = scope.ByteArray(options.Namespace),
                ip = scope.ByteArray(ip),
                database_filename = scope.ByteArray(options.DevServerOptions.DatabaseFilename),
                ui = (byte)(options.UI ? 1 : 0),
                log_format = scope.ByteArray(options.DevServerOptions.LogFormat),
                log_level = scope.ByteArray(options.DevServerOptions.LogLevel),
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
            var buildId = options.BuildId;
            if (buildId == null)
            {
                if (options.UseWorkerVersioning)
                {
                    throw new ArgumentException("BuildId must be explicitly set when UseWorkerVersioning is true");
                }
                var entryAssembly = Assembly.GetEntryAssembly() ??
                    throw new ArgumentException("Unable to get assembly manifest ID for build ID");
                buildId = entryAssembly.ManifestModule.ModuleVersionId.ToString();
            }
            // We have to disable remote activities if a user asks _or_ if we are not running an
            // activity worker at all. Otherwise shutdown will not proceed properly.
            var noRemoteActivities = options.LocalActivityWorkerOnly || options.Activities.Count == 0;
            var tuner = options.Tuner;
            if (tuner == null)
            {
                var maxWF = options.MaxConcurrentWorkflowTasks ?? 100;
                var maxAct = options.MaxConcurrentActivities ?? 100;
                var maxLocalAct = options.MaxConcurrentLocalActivities ?? 100;
                tuner = Temporalio.Worker.Tuning.WorkerTuner.CreateFixedSize(maxWF, maxAct, maxLocalAct);
            }
            else
            {
                if (options.MaxConcurrentWorkflowTasks.HasValue ||
                    options.MaxConcurrentActivities.HasValue ||
                    options.MaxConcurrentLocalActivities.HasValue)
                {
                    throw new ArgumentException(
                        "Cannot set both Tuner and any of MaxConcurrentWorkflowTasks, " +
                        "MaxConcurrentActivities, or MaxConcurrentLocalActivities.");
                }
            }
            return new()
            {
                namespace_ = scope.ByteArray(namespace_),
                task_queue = scope.ByteArray(options.TaskQueue),
                build_id = scope.ByteArray(buildId),
                identity_override = scope.ByteArray(options.Identity),
                max_cached_workflows = (uint)options.MaxCachedWorkflows,
                tuner = tuner.ToInteropTuner(scope),
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
                use_worker_versioning = (byte)(options.UseWorkerVersioning ? 1 : 0),
                max_concurrent_workflow_task_polls = (uint)options.MaxConcurrentWorkflowTaskPolls,
                nonsticky_to_sticky_poll_ratio = options.NonStickyToStickyPollRatio,
                max_concurrent_activity_task_polls = (uint)options.MaxConcurrentActivityTaskPolls,
                nondeterminism_as_workflow_fail =
                    (byte)(AnyNonDeterminismFailureTypes(options.WorkflowFailureExceptionTypes) ? 1 : 0),
                nondeterminism_as_workflow_fail_for_types = scope.ByteArrayArray(
                    AllNonDeterminismFailureTypeWorkflows(options.Workflows)),
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
            var buildId = options.BuildId;
            if (buildId == null)
            {
                var entryAssembly = Assembly.GetEntryAssembly() ??
                    throw new ArgumentException("Unable to get assembly manifest ID for build ID");
                buildId = entryAssembly.ManifestModule.ModuleVersionId.ToString();
            }
            return new()
            {
                namespace_ = scope.ByteArray(options.Namespace),
                task_queue = scope.ByteArray(options.TaskQueue),
                build_id = scope.ByteArray(buildId),
                identity_override = scope.ByteArray(options.Identity),
                max_cached_workflows = 2,
                tuner = Temporalio.Worker.Tuning.WorkerTuner.CreateFixedSize(2, 1, 1).ToInteropTuner(scope),
                no_remote_activities = 1,
                sticky_queue_schedule_to_start_timeout_millis = 1000,
                max_heartbeat_throttle_interval_millis = 1000,
                default_heartbeat_throttle_interval_millis = 1000,
                max_activities_per_second = 0,
                max_task_queue_activities_per_second = 0,
                graceful_shutdown_period_millis = 0,
                max_concurrent_workflow_task_polls = 1,
                nonsticky_to_sticky_poll_ratio = 1,
                max_concurrent_activity_task_polls = 1,
                nondeterminism_as_workflow_fail =
                    (byte)(AnyNonDeterminismFailureTypes(options.WorkflowFailureExceptionTypes) ? 1 : 0),
                nondeterminism_as_workflow_fail_for_types = scope.ByteArrayArray(
                    AllNonDeterminismFailureTypeWorkflows(options.Workflows)),
            };
        }

        private static Interop.TunerHolder ToInteropTuner(
            this Temporalio.Worker.Tuning.WorkerTuner tuner,
            Scope scope)
        {
            Temporalio.Worker.Tuning.ResourceBasedTunerOptions? lastTunerOptions = null;
            Temporalio.Worker.Tuning.ISlotSupplier[] suppliers =
            {
                tuner.WorkflowTaskSlotSupplier, tuner.ActivityTaskSlotSupplier,
                tuner.LocalActivitySlotSupplier,
            };
            foreach (var supplier in suppliers)
            {
                if (supplier is Temporalio.Worker.Tuning.ResourceBasedSlotSupplier resourceBased)
                {
                    if (lastTunerOptions != null && lastTunerOptions != resourceBased.TunerOptions)
                    {
                        throw new ArgumentException(
                            "All resource-based slot suppliers must have the same ResourceBasedTunerOptions");
                    }

                    lastTunerOptions = resourceBased.TunerOptions;
                }
            }

            return new()
            {
                workflow_slot_supplier =
                    tuner.WorkflowTaskSlotSupplier.ToInteropSlotSupplier(true),
                activity_slot_supplier =
                    tuner.ActivityTaskSlotSupplier.ToInteropSlotSupplier(false),
                local_activity_slot_supplier =
                    tuner.LocalActivitySlotSupplier.ToInteropSlotSupplier(false),
            };
        }

        private static Interop.SlotSupplier ToInteropSlotSupplier(
            this Temporalio.Worker.Tuning.ISlotSupplier supplier,
            bool isWorkflow)
        {
            if (supplier is Temporalio.Worker.Tuning.FixedSizeSlotSupplier fixedSize)
            {
                if (fixedSize.SlotCount < 1)
                {
                    throw new ArgumentException(
                        "FixedSizeSlotSupplier must have at least one slot");
                }
                return new()
                {
                    tag = Interop.SlotSupplier_Tag.FixedSize,
                    fixed_size = new Interop.FixedSizeSlotSupplier()
                    {
                        num_slots = new UIntPtr((uint)fixedSize.SlotCount),
                    },
                };
            }
            else if (supplier is Temporalio.Worker.Tuning.ResourceBasedSlotSupplier resourceBased)
            {
                var defaultMinimum = isWorkflow ? 5u : 1u;
                var defaultThrottle = isWorkflow ? 0 : 50;
                return new()
                {
                    tag = Interop.SlotSupplier_Tag.ResourceBased,
                    resource_based = new Interop.ResourceBasedSlotSupplier()
                    {
                        minimum_slots =
                            new UIntPtr(resourceBased.Options.MinimumSlots ?? defaultMinimum),
                        maximum_slots = new UIntPtr(resourceBased.Options.MaximumSlots ?? 500),
                        ramp_throttle_ms =
                            (ulong)(resourceBased.Options.RampThrottle?.TotalMilliseconds ??
                                    defaultThrottle),
                        tuner_options = new Interop.ResourceBasedTunerOptions()
                        {
                            target_memory_usage = resourceBased.TunerOptions.TargetMemoryUsage,
                            target_cpu_usage = resourceBased.TunerOptions.TargetCpuUsage,
                        },
                    },
                };
            }
            else
            {
                throw new ArgumentException(
                    "ISlotSupplier must be one of the types provided by the library");
            }
        }

        private static bool AnyNonDeterminismFailureTypes(
            IReadOnlyCollection<Type>? types) =>
            types?.Any(t => t.IsAssignableFrom(typeof(WorkflowNondeterminismException))) ?? false;

        private static string[] AllNonDeterminismFailureTypeWorkflows(
            IList<Workflows.WorkflowDefinition> workflows) =>
            workflows.
                Where(w => AnyNonDeterminismFailureTypes(w.FailureExceptionTypes)).
                Select(w =>
                    w.Name ?? throw new ArgumentException("Dynamic workflows cannot trap non-determinism")).
                ToArray();
    }
}
