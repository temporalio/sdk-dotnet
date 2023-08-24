use std::sync::Arc;

use temporal_sdk_core_api::telemetry::metrics;

use crate::{runtime::Runtime, ByteArrayRef};

pub struct MetricMeter {
    core: metrics::TemporalMeter,
}

#[no_mangle]
pub extern "C" fn metric_meter_new(runtime: *mut Runtime) -> *mut MetricMeter {
    let runtime = unsafe { &mut *runtime };
    if let Some(core) = runtime.core.telemetry().get_metric_meter() {
        Box::into_raw(Box::new(MetricMeter { core }))
    } else {
        std::ptr::null_mut()
    }
}

#[no_mangle]
pub extern "C" fn metric_meter_free(meter: *mut MetricMeter) {
    unsafe {
        let _ = Box::from_raw(meter);
    }
}

pub struct MetricAttributes {
    core: metrics::MetricAttributes,
}

#[repr(C)]
pub struct MetricAttribute {
    key: ByteArrayRef,
    value: MetricAttributeValue,
    value_type: MetricAttributeValueType,
}

#[repr(C)]
pub enum MetricAttributeValueType {
    String = 1,
    Int,
    Float,
    Bool,
}

#[repr(C)]
pub union MetricAttributeValue {
    string_value: std::mem::ManuallyDrop<ByteArrayRef>,
    int_value: i64,
    float_value: f64,
    bool_value: bool,
}

#[no_mangle]
pub extern "C" fn metric_attributes_new(
    meter: *const MetricMeter,
    attrs: *const MetricAttribute,
    size: libc::size_t,
) -> *mut MetricAttributes {
    let meter = unsafe { &*meter };
    let mut new_attrs = meter
        .core
        .inner
        .new_attributes(meter.core.default_attribs.clone());
    metric_attributes_append(&mut new_attrs, attrs, size);
    Box::into_raw(Box::new(MetricAttributes { core: new_attrs }))
}

#[no_mangle]
pub extern "C" fn metric_attributes_new_append(
    orig: *const MetricAttributes,
    attrs: *const MetricAttribute,
    size: libc::size_t,
) -> *mut MetricAttributes {
    let orig = unsafe { &*orig };
    let mut new_attrs = orig.core.clone();
    metric_attributes_append(&mut new_attrs, attrs, size);
    Box::into_raw(Box::new(MetricAttributes { core: new_attrs }))
}

#[no_mangle]
pub extern "C" fn metric_attributes_free(attrs: *mut MetricAttributes) {
    unsafe {
        let _ = Box::from_raw(attrs);
    }
}

fn metric_attributes_append(
    orig: &mut metrics::MetricAttributes,
    attrs: *const MetricAttribute,
    size: libc::size_t,
) {
    let attrs = unsafe { std::slice::from_raw_parts(attrs, size) };
    orig.add_new_attrs(attrs.iter().map(metric_attribute_to_key_value));
}

fn metric_attribute_to_key_value(attr: &MetricAttribute) -> metrics::MetricKeyValue {
    metrics::MetricKeyValue {
        key: attr.key.to_string(),
        value: match attr.value_type {
            MetricAttributeValueType::String => {
                metrics::MetricValue::String(unsafe { attr.value.string_value.to_string() })
            }
            MetricAttributeValueType::Int => {
                metrics::MetricValue::Int(unsafe { attr.value.int_value })
            }
            MetricAttributeValueType::Float => {
                metrics::MetricValue::Float(unsafe { attr.value.float_value })
            }
            MetricAttributeValueType::Bool => {
                metrics::MetricValue::Bool(unsafe { attr.value.bool_value })
            }
        },
    }
}

#[repr(C)]
pub struct MetricIntegerOptions {
    name: ByteArrayRef,
    description: ByteArrayRef,
    unit: ByteArrayRef,
    kind: MetricIntegerKind,
}

#[repr(C)]
pub enum MetricIntegerKind {
    Counter = 1,
    Histogram,
    Gauge,
}

pub enum MetricInteger {
    Counter(Arc<dyn metrics::Counter>),
    Histogram(Arc<dyn metrics::Histogram>),
    Gauge(Arc<dyn metrics::Gauge>),
}

#[no_mangle]
pub extern "C" fn metric_integer_new(
    meter: *const MetricMeter,
    options: *const MetricIntegerOptions,
) -> *mut MetricInteger {
    let meter = unsafe { &*meter };
    let options = unsafe { &*options };
    Box::into_raw(Box::new(match options.kind {
        MetricIntegerKind::Counter => {
            MetricInteger::Counter(meter.core.inner.counter(options.into()))
        }
        MetricIntegerKind::Histogram => {
            MetricInteger::Histogram(meter.core.inner.histogram(options.into()))
        }
        MetricIntegerKind::Gauge => MetricInteger::Gauge(meter.core.inner.gauge(options.into())),
    }))
}

#[no_mangle]
pub extern "C" fn metric_integer_free(metric: *mut MetricInteger) {
    unsafe {
        let _ = Box::from_raw(metric);
    }
}

#[no_mangle]
pub extern "C" fn metric_integer_record(
    metric: *const MetricInteger,
    value: u64,
    attrs: *const MetricAttributes,
) {
    let metric = unsafe { &*metric };
    let attrs = unsafe { &*attrs };
    match metric {
        MetricInteger::Counter(counter) => counter.add(value, &attrs.core),
        MetricInteger::Histogram(histogram) => histogram.record(value, &attrs.core),
        MetricInteger::Gauge(gauge) => gauge.record(value, &attrs.core),
    }
}

impl From<&MetricIntegerOptions> for metrics::MetricParameters {
    fn from(options: &MetricIntegerOptions) -> Self {
        metrics::MetricParametersBuilder::default()
            .name(options.name.to_string())
            .description(options.description.to_string())
            .unit(options.unit.to_string())
            .build()
            .unwrap()
    }
}
