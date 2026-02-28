#pragma once

/// @file metric_meter.h
/// @brief Metric primitives: MetricMeter, counters, histograms, gauges.

#include <temporalio/export.h>

#include <cstdint>
#include <functional>
#include <memory>
#include <optional>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

namespace temporalio::common {

/// Tag map for metric attributes (key -> string or numeric value).
using MetricAttributes =
    std::vector<std::pair<std::string, std::string>>;

/// Counter metric (monotonically increasing).
template <typename T = std::uint64_t>
class MetricCounter {
public:
    virtual ~MetricCounter() = default;

    /// Record a value with optional extra tags.
    virtual void add(T value, const MetricAttributes& tags = {}) = 0;

    /// Name of this metric.
    virtual const std::string& name() const = 0;

    /// Unit of this metric, if any.
    virtual const std::optional<std::string>& unit() const = 0;

    /// Description of this metric, if any.
    virtual const std::optional<std::string>& description() const = 0;
};

/// Histogram metric.
template <typename T = std::uint64_t>
class MetricHistogram {
public:
    virtual ~MetricHistogram() = default;

    /// Record a value with optional extra tags.
    virtual void record(T value, const MetricAttributes& tags = {}) = 0;

    /// Name of this metric.
    virtual const std::string& name() const = 0;

    /// Unit of this metric, if any.
    virtual const std::optional<std::string>& unit() const = 0;

    /// Description of this metric, if any.
    virtual const std::optional<std::string>& description() const = 0;
};

/// Gauge metric (current value).
template <typename T = std::int64_t>
class MetricGauge {
public:
    virtual ~MetricGauge() = default;

    /// Set the gauge value with optional extra tags.
    virtual void set(T value, const MetricAttributes& tags = {}) = 0;

    /// Name of this metric.
    virtual const std::string& name() const = 0;

    /// Unit of this metric, if any.
    virtual const std::optional<std::string>& unit() const = 0;

    /// Description of this metric, if any.
    virtual const std::optional<std::string>& description() const = 0;
};

/// Meter for creating metrics to record values on.
class MetricMeter {
public:
    virtual ~MetricMeter() = default;

    /// Create a new counter.
    virtual std::unique_ptr<MetricCounter<std::uint64_t>> create_counter(
        const std::string& name,
        std::optional<std::string> unit = std::nullopt,
        std::optional<std::string> description = std::nullopt) = 0;

    /// Create a new histogram.
    virtual std::unique_ptr<MetricHistogram<std::uint64_t>> create_histogram(
        const std::string& name,
        std::optional<std::string> unit = std::nullopt,
        std::optional<std::string> description = std::nullopt) = 0;

    /// Create a new gauge.
    virtual std::unique_ptr<MetricGauge<std::int64_t>> create_gauge(
        const std::string& name,
        std::optional<std::string> unit = std::nullopt,
        std::optional<std::string> description = std::nullopt) = 0;

    /// Create a new meter with the given tags appended. All metrics created off
    /// the meter will have these tags.
    virtual std::unique_ptr<MetricMeter> with_tags(
        MetricAttributes tags) = 0;
};

/// No-op metric meter implementation.
class TEMPORALIO_EXPORT NoopMetricMeter : public MetricMeter {
public:
    std::unique_ptr<MetricCounter<std::uint64_t>> create_counter(
        const std::string& name,
        std::optional<std::string> unit = std::nullopt,
        std::optional<std::string> description = std::nullopt) override;

    std::unique_ptr<MetricHistogram<std::uint64_t>> create_histogram(
        const std::string& name,
        std::optional<std::string> unit = std::nullopt,
        std::optional<std::string> description = std::nullopt) override;

    std::unique_ptr<MetricGauge<std::int64_t>> create_gauge(
        const std::string& name,
        std::optional<std::string> unit = std::nullopt,
        std::optional<std::string> description = std::nullopt) override;

    std::unique_ptr<MetricMeter> with_tags(MetricAttributes tags) override;
};

} // namespace temporalio::common
