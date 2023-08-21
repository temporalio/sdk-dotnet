use crate::ByteArray;
use crate::ByteArrayRef;
use crate::MetadataRef;

use std::net::SocketAddr;
use std::str::FromStr;
use std::sync::Arc;
use std::time::Duration;
use temporal_sdk_core::telemetry::{build_otlp_metric_exporter, start_prometheus_metric_exporter};
use temporal_sdk_core::CoreRuntime;
use temporal_sdk_core_api::telemetry::metrics::CoreMeter;
use temporal_sdk_core_api::telemetry::MetricTemporality;
use temporal_sdk_core_api::telemetry::{
    Logger, OtelCollectorOptionsBuilder, PrometheusExporterOptionsBuilder,
    TelemetryOptions as CoreTelemetryOptions, TelemetryOptionsBuilder,
};
use url::Url;

#[repr(C)]
pub struct RuntimeOptions {
    telemetry: *const TelemetryOptions,
}

#[repr(C)]
pub struct TelemetryOptions {
    logging: *const LoggingOptions,
    metrics: *const MetricsOptions,
}

#[repr(C)]
pub struct LoggingOptions {
    filter: ByteArrayRef,
    forward: bool,
}

#[repr(C)]
pub struct MetricsOptions {
    opentelemetry: *const OpenTelemetryOptions,
    prometheus: *const PrometheusOptions,
    attach_service_name: bool,
    global_tags: MetadataRef,
    metric_prefix: ByteArrayRef,
}

#[repr(C)]
pub struct OpenTelemetryOptions {
    url: ByteArrayRef,
    headers: MetadataRef,
    metric_periodicity_millis: u32,
    metric_temporality: OpenTelemetryMetricTemporality,
}

#[repr(C)]
pub enum OpenTelemetryMetricTemporality {
    Cumulative = 1,
    Delta,
}

#[repr(C)]
pub struct PrometheusOptions {
    bind_address: ByteArrayRef,
    counters_total_suffix: bool,
    unit_suffix: bool,
}

#[derive(Clone)]
pub struct Runtime {
    pub(crate) core: Arc<CoreRuntime>,
}

/// If fail is not null, it must be manually freed when done. Runtime is always
/// present, but it should never be used if fail is present, only freed after
/// fail is freed using it.
#[repr(C)]
pub struct RuntimeOrFail {
    runtime: *mut Runtime,
    fail: *const ByteArray,
}

#[no_mangle]
pub extern "C" fn runtime_new(options: *const RuntimeOptions) -> RuntimeOrFail {
    match Runtime::new(unsafe { &*options }) {
        Ok(runtime) => RuntimeOrFail {
            runtime: Box::into_raw(Box::new(runtime)),
            fail: std::ptr::null(),
        },
        Err(err) => {
            // We have to make an empty runtime just for the failure to be
            // freeable
            let mut runtime = Runtime {
                core: Arc::new(
                    CoreRuntime::new(
                        CoreTelemetryOptions::default(),
                        tokio::runtime::Builder::new_current_thread(),
                    )
                    .unwrap(),
                ),
            };
            let fail = runtime.alloc_utf8(&format!("Invalid options: {}", err));
            RuntimeOrFail {
                runtime: Box::into_raw(Box::new(runtime)),
                fail: fail.into_raw(),
            }
        }
    }
}

#[no_mangle]
pub extern "C" fn runtime_free(runtime: *mut Runtime) {
    unsafe {
        let _ = Box::from_raw(runtime);
    }
}

#[no_mangle]
pub extern "C" fn byte_array_free(runtime: *mut Runtime, bytes: *const ByteArray) {
    // Bail if freeing is disabled
    unsafe {
        if bytes.is_null() || (*bytes).disable_free {
            return;
        }
    }
    let bytes = bytes as *mut ByteArray;
    // Return vec back to core before dropping bytes
    let vec = unsafe { Vec::from_raw_parts((*bytes).data as *mut u8, (*bytes).size, (*bytes).cap) };
    // Set to null so the byte dropper doesn't try to free it
    unsafe { (*bytes).data = std::ptr::null_mut() };
    // Return only if runtime is non-null
    if !runtime.is_null() {
        let runtime = unsafe { &mut *runtime };
        runtime.return_buf(vec);
    }
    unsafe {
        let _ = Box::from_raw(bytes);
    }
}

impl Runtime {
    fn new(options: &RuntimeOptions) -> anyhow::Result<Runtime> {
        let mut core = CoreRuntime::new(
            if let Some(v) = unsafe { options.telemetry.as_ref() } {
                v.try_into()?
            } else {
                CoreTelemetryOptions::default()
            },
            tokio::runtime::Builder::new_multi_thread(),
        )?;
        // We late-bind the metrics after core runtime is created since it needs
        // the Tokio handle
        if let Some(v) = unsafe { options.telemetry.as_ref() } {
            if let Some(v) = unsafe { v.metrics.as_ref() } {
                let _guard = core.tokio_handle().enter();
                core.attach_late_init_metrics(v.try_into()?);
            }
        }
        Ok(Runtime {
            core: Arc::new(core),
        })
    }

    fn borrow_buf(&mut self) -> Vec<u8> {
        // We currently do not use a thread-safe byte pool, but if wanted, it
        // can be added here
        Vec::new()
    }

    fn return_buf(&mut self, _vec: Vec<u8>) {
        // We currently do not use a thread-safe byte pool, but if wanted, it
        // can be added here
    }

    pub fn alloc_utf8(&mut self, v: &str) -> ByteArray {
        let mut buf = self.borrow_buf();
        buf.clear();
        buf.extend_from_slice(v.as_bytes());
        ByteArray::from_vec(buf)
    }
}

impl TryFrom<&TelemetryOptions> for CoreTelemetryOptions {
    type Error = anyhow::Error;

    fn try_from(options: &TelemetryOptions) -> anyhow::Result<Self> {
        let mut build = TelemetryOptionsBuilder::default();
        if let Some(v) = unsafe { options.metrics.as_ref() } {
            build.attach_service_name(v.attach_service_name);
        }
        if let Some(v) = unsafe { options.logging.as_ref() } {
            build.logging(if v.forward {
                Logger::Forward {
                    filter: v.filter.to_string(),
                }
            } else {
                Logger::Console {
                    filter: v.filter.to_string(),
                }
            });
        }
        // Note, metrics are late-bound in Runtime::new
        Ok(build.build()?)
    }
}

impl TryFrom<&MetricsOptions> for Arc<dyn CoreMeter> {
    type Error = anyhow::Error;

    fn try_from(options: &MetricsOptions) -> anyhow::Result<Self> {
        if let Some(otel_options) = unsafe { options.opentelemetry.as_ref() } {
            if !options.prometheus.is_null() {
                return Err(anyhow::anyhow!(
                    "Cannot have OpenTelemetry and Prometheus metrics"
                ));
            }
            // Build OTel exporter
            let mut build = OtelCollectorOptionsBuilder::default();
            build
                .url(Url::parse(&otel_options.url.to_str())?)
                .headers(otel_options.headers.to_string_map_on_newlines())
                .metric_temporality(match otel_options.metric_temporality {
                    OpenTelemetryMetricTemporality::Cumulative => MetricTemporality::Cumulative,
                    OpenTelemetryMetricTemporality::Delta => MetricTemporality::Delta,
                })
                .global_tags(options.global_tags.to_string_map_on_newlines());
            if otel_options.metric_periodicity_millis > 0 {
                build.metric_periodicity(Duration::from_millis(
                    otel_options.metric_periodicity_millis.into(),
                ));
            }
            // TODO(cretz): Not supported as user-defined currently
            // if let Some(prefix) = options.metric_prefix.to_option_string() {
            //     build.metric_prefix(&prefix);
            // }
            Ok(Arc::new(build_otlp_metric_exporter(build.build()?)?))
        } else if let Some(prom_options) = unsafe { options.prometheus.as_ref() } {
            // Start prom exporter
            let mut build = PrometheusExporterOptionsBuilder::default();
            build
                .socket_addr(SocketAddr::from_str(prom_options.bind_address.to_str())?)
                .global_tags(options.global_tags.to_string_map_on_newlines())
                .counters_total_suffix(prom_options.counters_total_suffix)
                .unit_suffix(prom_options.unit_suffix);
            // TODO(cretz): Not supported as user-defined currently
            // if let Some(metric_prefix) = options.metric_prefix.to_option_string() {
            //     build.metric_prefix(&metric_prefix);
            // }
            Ok(start_prometheus_metric_exporter(build.build()?)?.meter)
        } else {
            Err(anyhow::anyhow!(
                "Either OpenTelemetry or Prometheus config must be provided"
            ))
        }
    }
}
