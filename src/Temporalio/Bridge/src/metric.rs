use std::{any::Any, sync::Arc};

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
    let orig = meter
        .core
        .inner
        .new_attributes(meter.core.default_attribs.clone());
    Box::into_raw(Box::new(metric_attributes_append(
        meter, &orig, attrs, size,
    )))
}

#[no_mangle]
pub extern "C" fn metric_attributes_new_append(
    meter: *const MetricMeter,
    orig: *const MetricAttributes,
    attrs: *const MetricAttribute,
    size: libc::size_t,
) -> *mut MetricAttributes {
    let meter = unsafe { &*meter };
    let orig = unsafe { &*orig };
    Box::into_raw(Box::new(metric_attributes_append(
        meter, &orig.core, attrs, size,
    )))
}

#[no_mangle]
pub extern "C" fn metric_attributes_free(attrs: *mut MetricAttributes) {
    unsafe {
        let _ = Box::from_raw(attrs);
    }
}

fn metric_attributes_append(
    meter: &MetricMeter,
    orig: &metrics::MetricAttributes,
    attrs: *const MetricAttribute,
    size: libc::size_t,
) -> MetricAttributes {
    let attrs = unsafe { std::slice::from_raw_parts(attrs, size) };
    let core = meter.core.inner.extend_attributes(
        orig.clone(),
        metrics::NewAttributes {
            attributes: attrs.iter().map(metric_attribute_to_key_value).collect(),
        },
    );
    MetricAttributes { core }
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

type CustomMetricMeterMetricIntegerNewCallback = unsafe extern "C" fn(
    name: ByteArrayRef,
    description: ByteArrayRef,
    unit: ByteArrayRef,
    kind: MetricIntegerKind,
) -> *const libc::c_void;

type CustomMetricMeterMetricIntegerFreeCallback = unsafe extern "C" fn(metric: *const libc::c_void);

type CustomMetricMeterMetricIntegerUpdateCallback =
    unsafe extern "C" fn(metric: *const libc::c_void, value: u64, attributes: *const libc::c_void);

type CustomMetricMeterAttributesNewCallback = unsafe extern "C" fn(
    append_from: *const libc::c_void,
    attributes: *const CustomMetricAttribute,
    attributes_size: libc::size_t,
) -> *const libc::c_void;

type CustomMetricMeterAttributesFreeCallback =
    unsafe extern "C" fn(attributes: *const libc::c_void);

type CustomMetricMeterMeterFreeCallback = unsafe extern "C" fn(meter: *const CustomMetricMeter);

/// No parameters in the callbacks below should be assumed to live beyond the
/// callbacks unless they are pointers to things that were created lang-side
/// originally. There are no guarantees on which thread these calls may be
/// invoked on.
#[repr(C)]
pub struct CustomMetricMeter {
    pub metric_integer_new: CustomMetricMeterMetricIntegerNewCallback,
    pub metric_integer_free: CustomMetricMeterMetricIntegerFreeCallback,
    pub metric_integer_update: CustomMetricMeterMetricIntegerUpdateCallback,
    pub attributes_new: CustomMetricMeterAttributesNewCallback,
    pub attributes_free: CustomMetricMeterAttributesFreeCallback,
    pub meter_free: CustomMetricMeterMeterFreeCallback,
}

#[repr(C)]
pub struct CustomMetricAttribute {
    pub key: ByteArrayRef,
    pub value: CustomMetricAttributeValue,
    pub value_type: MetricAttributeValueType,
}

#[repr(C)]
pub union CustomMetricAttributeValue {
    string_value: CustomMetricAttributeValueString,
    int_value: i64,
    float_value: f64,
    bool_value: bool,
}

// We create this type because we want it to implement Copy
#[repr(C)]
#[derive(Copy, Clone)]
pub struct CustomMetricAttributeValueString {
    data: *const u8,
    size: libc::size_t,
}

#[derive(Debug)]
pub struct CustomMetricMeterRef {
    meter_impl: Arc<CustomMetricMeterImpl>,
}

unsafe impl Send for CustomMetricMeterRef {}
unsafe impl Sync for CustomMetricMeterRef {}

impl metrics::CoreMeter for CustomMetricMeterRef {
    fn new_attributes(&self, attribs: metrics::NewAttributes) -> metrics::MetricAttributes {
        self.build_attributes(None, attribs)
    }

    fn extend_attributes(
        &self,
        existing: metrics::MetricAttributes,
        attribs: metrics::NewAttributes,
    ) -> metrics::MetricAttributes {
        self.build_attributes(Some(existing), attribs)
    }

    fn counter(&self, params: metrics::MetricParameters) -> Arc<dyn metrics::Counter> {
        Arc::new(self.new_metric_integer(params, MetricIntegerKind::Counter))
    }

    fn histogram(&self, params: metrics::MetricParameters) -> Arc<dyn metrics::Histogram> {
        Arc::new(self.new_metric_integer(params, MetricIntegerKind::Histogram))
    }

    fn gauge(&self, params: metrics::MetricParameters) -> Arc<dyn metrics::Gauge> {
        Arc::new(self.new_metric_integer(params, MetricIntegerKind::Gauge))
    }
}

impl CustomMetricMeterRef {
    pub fn new(meter: *const CustomMetricMeter) -> CustomMetricMeterRef {
        CustomMetricMeterRef {
            meter_impl: Arc::new(CustomMetricMeterImpl(meter)),
        }
    }

    fn build_attributes(
        &self,
        append_from: Option<metrics::MetricAttributes>,
        attribs: metrics::NewAttributes,
    ) -> metrics::MetricAttributes {
        unsafe {
            let meter = &*(self.meter_impl.0);
            let append_from = match append_from {
                Some(metrics::MetricAttributes::Dynamic(v)) => {
                    v.clone()
                        .as_any()
                        .downcast::<CustomMetricAttributes>()
                        .unwrap()
                        .attributes
                }
                _ => std::ptr::null(),
            };
            // Build a set of CustomMetricAttributes with _references_ to the
            // pieces in attribs. We count on both this vec and the attribs vec
            // living beyond the callback invocation.
            let attrs: Vec<CustomMetricAttribute> = attribs
                .attributes
                .iter()
                .map(|kv| {
                    let (value, value_type) = match kv.value {
                        metrics::MetricValue::String(ref v) => (
                            CustomMetricAttributeValue {
                                string_value: CustomMetricAttributeValueString {
                                    data: v.as_ptr(),
                                    size: v.len(),
                                },
                            },
                            MetricAttributeValueType::String,
                        ),
                        metrics::MetricValue::Int(v) => (
                            CustomMetricAttributeValue { int_value: v },
                            MetricAttributeValueType::Int,
                        ),
                        metrics::MetricValue::Float(v) => (
                            CustomMetricAttributeValue { float_value: v },
                            MetricAttributeValueType::Float,
                        ),
                        metrics::MetricValue::Bool(v) => (
                            CustomMetricAttributeValue { bool_value: v },
                            MetricAttributeValueType::Bool,
                        ),
                    };
                    CustomMetricAttribute {
                        key: ByteArrayRef::from_string(&kv.key),
                        value,
                        value_type,
                    }
                })
                .collect();
            let raw_attrs = (meter.attributes_new)(append_from, attrs.as_ptr(), attrs.len());
            // This is just to confirm we don't move the attribute by accident
            // above before the callback is called
            let _ = attribs;
            metrics::MetricAttributes::Dynamic(Arc::new(CustomMetricAttributes {
                meter_impl: self.meter_impl.clone(),
                attributes: raw_attrs,
            }))
        }
    }

    fn new_metric_integer(
        &self,
        params: metrics::MetricParameters,
        kind: MetricIntegerKind,
    ) -> CustomMetricInteger {
        unsafe {
            let meter = &*(self.meter_impl.0);
            let metric = (meter.metric_integer_new)(
                ByteArrayRef::from_str(&params.name),
                ByteArrayRef::from_str(&params.description),
                ByteArrayRef::from_str(&params.unit),
                kind,
            );
            CustomMetricInteger {
                meter_impl: self.meter_impl.clone(),
                metric,
            }
        }
    }
}

// Needed so we can have a drop impl
#[derive(Debug)]
struct CustomMetricMeterImpl(*const CustomMetricMeter);

impl Drop for CustomMetricMeterImpl {
    fn drop(&mut self) {
        unsafe {
            let meter = &*(self.0);
            (meter.meter_free)(self.0);
        }
    }
}

#[derive(Debug)]
struct CustomMetricAttributes {
    meter_impl: Arc<CustomMetricMeterImpl>,
    attributes: *const libc::c_void,
}

unsafe impl Send for CustomMetricAttributes {}
unsafe impl Sync for CustomMetricAttributes {}

impl metrics::CustomMetricAttributes for CustomMetricAttributes {
    fn as_any(self: Arc<Self>) -> Arc<dyn Any + Send + Sync> {
        self as Arc<dyn Any + Send + Sync>
    }
}

impl Drop for CustomMetricAttributes {
    fn drop(&mut self) {
        unsafe {
            let meter = &*(self.meter_impl.0);
            (meter.attributes_free)(self.attributes);
        }
    }
}

struct CustomMetricInteger {
    meter_impl: Arc<CustomMetricMeterImpl>,
    metric: *const libc::c_void,
}

unsafe impl Send for CustomMetricInteger {}
unsafe impl Sync for CustomMetricInteger {}

impl metrics::Counter for CustomMetricInteger {
    fn add(&self, value: u64, attributes: &metrics::MetricAttributes) {
        unsafe {
            let meter = &*(self.meter_impl.0);
            (meter.metric_integer_update)(
                self.metric,
                value,
                raw_custom_metric_attributes(attributes),
            );
        }
    }
}

impl metrics::Histogram for CustomMetricInteger {
    fn record(&self, value: u64, attributes: &metrics::MetricAttributes) {
        unsafe {
            let meter = &*(self.meter_impl.0);
            (meter.metric_integer_update)(
                self.metric,
                value,
                raw_custom_metric_attributes(attributes),
            );
        }
    }
}

impl metrics::Gauge for CustomMetricInteger {
    fn record(&self, value: u64, attributes: &metrics::MetricAttributes) {
        unsafe {
            let meter = &*(self.meter_impl.0);
            (meter.metric_integer_update)(
                self.metric,
                value,
                raw_custom_metric_attributes(attributes),
            );
        }
    }
}

fn raw_custom_metric_attributes(attributes: &metrics::MetricAttributes) -> *const libc::c_void {
    if let metrics::MetricAttributes::Dynamic(v) = attributes {
        v.clone()
            .as_any()
            .downcast::<CustomMetricAttributes>()
            .unwrap()
            .attributes
    } else {
        panic!("Unexpected attribute type")
    }
}

impl Drop for CustomMetricInteger {
    fn drop(&mut self) {
        unsafe {
            let meter = &*(self.meter_impl.0);
            (meter.metric_integer_free)(self.metric);
        }
    }
}
