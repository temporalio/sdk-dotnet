use std::{any::Any, sync::Arc, time::Duration};

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
pub struct MetricOptions {
    name: ByteArrayRef,
    description: ByteArrayRef,
    unit: ByteArrayRef,
    kind: MetricKind,
}

#[repr(C)]
pub enum MetricKind {
    CounterInteger = 1,
    HistogramInteger,
    HistogramFloat,
    HistogramDuration,
    GaugeInteger,
    GaugeFloat,
}

pub enum Metric {
    CounterInteger(Arc<dyn metrics::Counter>),
    HistogramInteger(Arc<dyn metrics::Histogram>),
    HistogramFloat(Arc<dyn metrics::HistogramF64>),
    HistogramDuration(Arc<dyn metrics::HistogramDuration>),
    GaugeInteger(Arc<dyn metrics::Gauge>),
    GaugeFloat(Arc<dyn metrics::GaugeF64>),
}

#[no_mangle]
pub extern "C" fn metric_new(
    meter: *const MetricMeter,
    options: *const MetricOptions,
) -> *mut Metric {
    let meter = unsafe { &*meter };
    let options = unsafe { &*options };
    Box::into_raw(Box::new(match options.kind {
        MetricKind::CounterInteger => {
            Metric::CounterInteger(meter.core.inner.counter(options.into()))
        }
        MetricKind::HistogramInteger => {
            Metric::HistogramInteger(meter.core.inner.histogram(options.into()))
        }
        MetricKind::HistogramFloat => {
            Metric::HistogramFloat(meter.core.inner.histogram_f64(options.into()))
        }
        MetricKind::HistogramDuration => {
            Metric::HistogramDuration(meter.core.inner.histogram_duration(options.into()))
        }
        MetricKind::GaugeInteger => Metric::GaugeInteger(meter.core.inner.gauge(options.into())),
        MetricKind::GaugeFloat => Metric::GaugeFloat(meter.core.inner.gauge_f64(options.into())),
    }))
}

#[no_mangle]
pub extern "C" fn metric_free(metric: *mut Metric) {
    unsafe {
        let _ = Box::from_raw(metric);
    }
}

#[no_mangle]
pub extern "C" fn metric_record_integer(
    metric: *const Metric,
    value: u64,
    attrs: *const MetricAttributes,
) {
    let metric = unsafe { &*metric };
    let attrs = unsafe { &*attrs };
    match metric {
        Metric::CounterInteger(counter) => counter.add(value, &attrs.core),
        Metric::HistogramInteger(histogram) => histogram.record(value, &attrs.core),
        Metric::GaugeInteger(gauge) => gauge.record(value, &attrs.core),
        _ => panic!("Not an integer type"),
    }
}

#[no_mangle]
pub extern "C" fn metric_record_float(
    metric: *const Metric,
    value: f64,
    attrs: *const MetricAttributes,
) {
    let metric = unsafe { &*metric };
    let attrs = unsafe { &*attrs };
    match metric {
        Metric::HistogramFloat(histogram) => histogram.record(value, &attrs.core),
        Metric::GaugeFloat(gauge) => gauge.record(value, &attrs.core),
        _ => panic!("Not a float type"),
    }
}

#[no_mangle]
pub extern "C" fn metric_record_duration(
    metric: *const Metric,
    value_ms: u64,
    attrs: *const MetricAttributes,
) {
    let metric = unsafe { &*metric };
    let attrs = unsafe { &*attrs };
    match metric {
        Metric::HistogramDuration(histogram) => {
            histogram.record(Duration::from_millis(value_ms), &attrs.core)
        }
        _ => panic!("Not a duration type"),
    }
}

impl From<&MetricOptions> for metrics::MetricParameters {
    fn from(options: &MetricOptions) -> Self {
        metrics::MetricParametersBuilder::default()
            .name(options.name.to_string())
            .description(options.description.to_string())
            .unit(options.unit.to_string())
            .build()
            .unwrap()
    }
}

type CustomMetricMeterMetricNewCallback = unsafe extern "C" fn(
    name: ByteArrayRef,
    description: ByteArrayRef,
    unit: ByteArrayRef,
    kind: MetricKind,
) -> *const libc::c_void;

type CustomMetricMeterMetricFreeCallback = unsafe extern "C" fn(metric: *const libc::c_void);

type CustomMetricMeterMetricRecordIntegerCallback =
    unsafe extern "C" fn(metric: *const libc::c_void, value: u64, attributes: *const libc::c_void);

type CustomMetricMeterMetricRecordFloatCallback =
    unsafe extern "C" fn(metric: *const libc::c_void, value: f64, attributes: *const libc::c_void);

type CustomMetricMeterMetricRecordDurationCallback = unsafe extern "C" fn(
    metric: *const libc::c_void,
    value_ms: u64,
    attributes: *const libc::c_void,
);

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
    pub metric_new: CustomMetricMeterMetricNewCallback,
    pub metric_free: CustomMetricMeterMetricFreeCallback,
    pub metric_record_integer: CustomMetricMeterMetricRecordIntegerCallback,
    pub metric_record_float: CustomMetricMeterMetricRecordFloatCallback,
    pub metric_record_duration: CustomMetricMeterMetricRecordDurationCallback,
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
        Arc::new(self.new_metric(params, MetricKind::CounterInteger))
    }

    fn histogram(&self, params: metrics::MetricParameters) -> Arc<dyn metrics::Histogram> {
        Arc::new(self.new_metric(params, MetricKind::HistogramInteger))
    }

    fn histogram_f64(&self, params: metrics::MetricParameters) -> Arc<dyn metrics::HistogramF64> {
        Arc::new(self.new_metric(params, MetricKind::HistogramFloat))
    }

    fn histogram_duration(
        &self,
        params: metrics::MetricParameters,
    ) -> Arc<dyn metrics::HistogramDuration> {
        Arc::new(self.new_metric(params, MetricKind::HistogramDuration))
    }

    fn gauge(&self, params: metrics::MetricParameters) -> Arc<dyn metrics::Gauge> {
        Arc::new(self.new_metric(params, MetricKind::GaugeInteger))
    }

    fn gauge_f64(&self, params: metrics::MetricParameters) -> Arc<dyn metrics::GaugeF64> {
        Arc::new(self.new_metric(params, MetricKind::GaugeFloat))
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
                        .expect("Attributes not CustomMetricAttributes as expected")
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
                        key: ByteArrayRef::from_str(&kv.key),
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

    fn new_metric(&self, params: metrics::MetricParameters, kind: MetricKind) -> CustomMetric {
        unsafe {
            let meter = &*(self.meter_impl.0);
            let metric = (meter.metric_new)(
                ByteArrayRef::from_str(&params.name),
                ByteArrayRef::from_str(&params.description),
                ByteArrayRef::from_str(&params.unit),
                kind,
            );
            CustomMetric {
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

struct CustomMetric {
    meter_impl: Arc<CustomMetricMeterImpl>,
    metric: *const libc::c_void,
}

unsafe impl Send for CustomMetric {}
unsafe impl Sync for CustomMetric {}

impl metrics::Counter for CustomMetric {
    fn add(&self, value: u64, attributes: &metrics::MetricAttributes) {
        unsafe {
            let meter = &*(self.meter_impl.0);
            (meter.metric_record_integer)(
                self.metric,
                value,
                raw_custom_metric_attributes(attributes),
            );
        }
    }
}

impl metrics::Histogram for CustomMetric {
    fn record(&self, value: u64, attributes: &metrics::MetricAttributes) {
        unsafe {
            let meter = &*(self.meter_impl.0);
            (meter.metric_record_integer)(
                self.metric,
                value,
                raw_custom_metric_attributes(attributes),
            );
        }
    }
}

impl metrics::HistogramF64 for CustomMetric {
    fn record(&self, value: f64, attributes: &metrics::MetricAttributes) {
        unsafe {
            let meter = &*(self.meter_impl.0);
            (meter.metric_record_float)(
                self.metric,
                value,
                raw_custom_metric_attributes(attributes),
            );
        }
    }
}

impl metrics::HistogramDuration for CustomMetric {
    fn record(&self, value: Duration, attributes: &metrics::MetricAttributes) {
        unsafe {
            let meter = &*(self.meter_impl.0);
            (meter.metric_record_duration)(
                self.metric,
                value.as_millis().try_into().unwrap_or(u64::MAX),
                raw_custom_metric_attributes(attributes),
            );
        }
    }
}

impl metrics::Gauge for CustomMetric {
    fn record(&self, value: u64, attributes: &metrics::MetricAttributes) {
        unsafe {
            let meter = &*(self.meter_impl.0);
            (meter.metric_record_integer)(
                self.metric,
                value,
                raw_custom_metric_attributes(attributes),
            );
        }
    }
}

impl metrics::GaugeF64 for CustomMetric {
    fn record(&self, value: f64, attributes: &metrics::MetricAttributes) {
        unsafe {
            let meter = &*(self.meter_impl.0);
            (meter.metric_record_float)(
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
            .expect("Attributes not CustomMetricAttributes as expected")
            .attributes
    } else {
        panic!("Unexpected attribute type")
    }
}

impl Drop for CustomMetric {
    fn drop(&mut self) {
        unsafe {
            let meter = &*(self.meter_impl.0);
            (meter.metric_free)(self.metric);
        }
    }
}
