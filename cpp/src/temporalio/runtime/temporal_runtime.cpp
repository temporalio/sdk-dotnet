#include <temporalio/common/metric_meter.h>
#include <temporalio/runtime/temporal_runtime.h>

#include <cstdio>
#include <memory>
#include <string>

#include "temporalio/bridge/runtime.h"

namespace temporalio::runtime {

// ── Impl (pimpl) ───────────────────────────────────────────────────────────

struct TemporalRuntime::Impl {
    std::unique_ptr<bridge::Runtime> bridge_runtime;
    std::unique_ptr<common::MetricMeter> metric_meter;

    Impl() : metric_meter(std::make_unique<common::NoopMetricMeter>()) {}
};

// ── Helpers ────────────────────────────────────────────────────────────────

namespace {

/// Default log callback: writes forwarded log messages to stderr.
void default_log_callback(TemporalCoreForwardedLogLevel level,
                          std::string_view target,
                          std::string_view message,
                          uint64_t /*timestamp_millis*/) {
    const char* level_str = "UNKNOWN";
    switch (level) {
        case TemporalCoreForwardedLogLevel::Trace: level_str = "TRACE"; break;
        case TemporalCoreForwardedLogLevel::Debug: level_str = "DEBUG"; break;
        case TemporalCoreForwardedLogLevel::Info:  level_str = "INFO";  break;
        case TemporalCoreForwardedLogLevel::Warn:  level_str = "WARN";  break;
        case TemporalCoreForwardedLogLevel::Error: level_str = "ERROR"; break;
    }
    std::fprintf(stderr, "[%s] %.*s: %.*s\n", level_str,
                 static_cast<int>(target.size()), target.data(),
                 static_cast<int>(message.size()), message.data());
}

/// Build a bridge log callback from the public LogForwardingOptions.
/// If the user provided a callback, wraps it to convert the level enum.
/// Otherwise, returns a default callback that writes to stderr.
decltype(bridge::LogForwardingOptions::callback) make_bridge_log_callback(
    const LogForwardingOptions& fwd_opts) {
    if (fwd_opts.callback) {
        // Wrap the user's callback, converting the level enum
        auto user_cb = fwd_opts.callback;
        return [user_cb](TemporalCoreForwardedLogLevel level,
                         std::string_view target,
                         std::string_view message,
                         uint64_t timestamp_millis) {
            user_cb(static_cast<LogLevel>(level), target, message,
                    timestamp_millis);
        };
    }
    // Default: log to stderr
    return default_log_callback;
}

/// Build a bridge::RuntimeOptions from the public TemporalRuntimeOptions.
bridge::RuntimeOptions to_bridge_options(const TemporalRuntimeOptions& opts) {
    bridge::RuntimeOptions bridge_opts;

    // Log forwarding
    if (opts.telemetry.logging && opts.telemetry.logging->forwarding) {
        auto fwd = std::make_unique<bridge::LogForwardingOptions>();
        fwd->filter = opts.telemetry.logging->forwarding->level;
        fwd->callback = make_bridge_log_callback(
            *opts.telemetry.logging->forwarding);
        bridge_opts.log_forwarding = std::move(fwd);
    }

    // OpenTelemetry
    if (opts.telemetry.metrics && opts.telemetry.metrics->opentelemetry) {
        const auto& otel = *opts.telemetry.metrics->opentelemetry;
        auto otel_opts = std::make_unique<bridge::OpenTelemetryOptions>();
        otel_opts->url = otel.url;
        // Convert headers vector to newline-delimited string
        for (const auto& [key, value] : otel.headers) {
            if (!otel_opts->headers.empty()) {
                otel_opts->headers.push_back('\n');
            }
            otel_opts->headers.append(key);
            otel_opts->headers.push_back('\n');
            otel_opts->headers.append(value);
        }
        if (otel.metric_periodicity) {
            otel_opts->metric_periodicity_millis =
                static_cast<uint32_t>(otel.metric_periodicity->count());
        }
        otel_opts->durations_as_seconds = otel.durations_as_seconds;
        bridge_opts.opentelemetry = std::move(otel_opts);
    }

    // Prometheus
    if (opts.telemetry.metrics && opts.telemetry.metrics->prometheus) {
        const auto& prom = *opts.telemetry.metrics->prometheus;
        auto prom_opts = std::make_unique<bridge::PrometheusOptions>();
        prom_opts->bind_address = prom.bind_address;
        prom_opts->counters_total_suffix = prom.counters_total_suffix;
        prom_opts->unit_suffix = prom.unit_suffix;
        prom_opts->durations_as_seconds = prom.durations_as_seconds;
        bridge_opts.prometheus = std::move(prom_opts);
    }

    // Metric settings
    if (opts.telemetry.metrics) {
        bridge_opts.attach_service_name =
            opts.telemetry.metrics->attach_service_name;
        if (opts.telemetry.metrics->metric_prefix) {
            bridge_opts.metric_prefix = *opts.telemetry.metrics->metric_prefix;
        }
    }

    // Worker heartbeat interval
    if (opts.worker_heartbeat_interval) {
        bridge_opts.worker_heartbeat_interval_millis =
            static_cast<uint64_t>(opts.worker_heartbeat_interval->count());
    }

    return bridge_opts;
}

}  // namespace

// ── TemporalRuntime ────────────────────────────────────────────────────────

TemporalRuntime::TemporalRuntime(TemporalRuntimeOptions options)
    : impl_(std::make_unique<Impl>()), options_(std::move(options)) {
    auto bridge_opts = to_bridge_options(options_);
    impl_->bridge_runtime =
        std::make_unique<bridge::Runtime>(bridge_opts);
}

TemporalRuntime::~TemporalRuntime() = default;

TemporalRuntime::TemporalRuntime(TemporalRuntime&&) noexcept = default;
TemporalRuntime& TemporalRuntime::operator=(TemporalRuntime&&) noexcept =
    default;

namespace {

/// Accessor for the function-local static that backs default_instance().
/// Separated so reset_default() can clear it before static destruction.
std::shared_ptr<TemporalRuntime>& default_instance_ref() {
    static std::shared_ptr<TemporalRuntime> instance = [] {
        return std::make_shared<TemporalRuntime>(TemporalRuntimeOptions{});
    }();
    return instance;
}

}  // namespace

std::shared_ptr<TemporalRuntime> TemporalRuntime::default_instance() {
    return default_instance_ref();
}

void TemporalRuntime::reset_default() {
    default_instance_ref().reset();
}

common::MetricMeter& TemporalRuntime::metric_meter() {
    return *impl_->metric_meter;
}

bridge::Runtime* TemporalRuntime::bridge_runtime() const noexcept {
    return impl_ ? impl_->bridge_runtime.get() : nullptr;
}

} // namespace temporalio::runtime
