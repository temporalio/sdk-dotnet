use crate::ByteArray;
use crate::ByteArrayRef;

use std::net::SocketAddr;
use std::str::FromStr;
use std::sync::Arc;
use std::time::Duration;
use temporal_sdk_core::CoreRuntime;
use temporal_sdk_core_api::telemetry::{
    Logger, MetricsExporter, OtelCollectorOptions, TelemetryOptions as CoreTelemetryOptions,
    TelemetryOptionsBuilder, TraceExportConfig, TraceExporter,
};
use url::Url;

#[repr(C)]
pub struct RuntimeOptions {
    telemetry: *const TelemetryOptions,
}

#[repr(C)]
pub struct TelemetryOptions {
    tracing: *const TracingOptions,
    logging: *const LoggingOptions,
    metrics: *const MetricsOptions,
}

#[repr(C)]
pub struct TracingOptions {
    filter: ByteArrayRef,
    opentelemetry: OpenTelemetryOptions,
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
}

#[repr(C)]
pub struct OpenTelemetryOptions {
    url: ByteArrayRef,
    /// Headers are <key1>\n<value1>\n<key2>\n<value2>. Header keys or values
    /// cannot contain a newline within.
    headers: ByteArrayRef,
    metric_periodicity_millis: u32,
}

#[repr(C)]
pub struct PrometheusOptions {
    bind_address: ByteArrayRef,
}

#[derive(Clone)]
pub struct Runtime {
    pub(crate) core: Arc<CoreRuntime>,
}

/// If success or fail are not null, they must be manually freed when done.
/// Runtime is always present, but it should never be used if fail is present,
/// only freed after fail is freed.
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
        Ok(Runtime {
            // TODO(cretz): Options to configure thread pool?
            core: Arc::new(CoreRuntime::new(
                if let Some(v) = unsafe { options.telemetry.as_ref() } {
                    v.try_into()?
                } else {
                    CoreTelemetryOptions::default()
                },
                tokio::runtime::Builder::new_multi_thread(),
            )?),
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
        if let Some(v) = unsafe { options.tracing.as_ref() } {
            build.tracing(TraceExportConfig {
                filter: v.filter.to_string(),
                exporter: TraceExporter::Otel((&v.opentelemetry).try_into()?),
            });
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
        if let Some(v) = unsafe { options.metrics.as_ref() } {
            build.metrics(if let Some(t) = unsafe { v.opentelemetry.as_ref() } {
                if !v.prometheus.is_null() {
                    return Err(anyhow::anyhow!(
                        "Cannot have OpenTelemetry and Prometheus metrics"
                    ));
                }
                MetricsExporter::Otel(t.try_into()?)
            } else if let Some(t) = unsafe { v.prometheus.as_ref() } {
                MetricsExporter::Prometheus(SocketAddr::from_str(t.bind_address.to_str())?)
            } else {
                return Err(anyhow::anyhow!(
                    "Either OpenTelemetry or Prometheus config must be provided"
                ));
            });
        }
        Ok(build.build()?)
    }
}

impl TryFrom<&OpenTelemetryOptions> for OtelCollectorOptions {
    type Error = anyhow::Error;

    fn try_from(options: &OpenTelemetryOptions) -> anyhow::Result<Self> {
        Ok(OtelCollectorOptions {
            url: Url::parse(&options.url.to_str())?,
            headers: options.headers.to_string_map_on_newlines(),
            metric_periodicity: if options.metric_periodicity_millis == 0 {
                None
            } else {
                Some(Duration::from_millis(
                    options.metric_periodicity_millis.into(),
                ))
            },
        })
    }
}
