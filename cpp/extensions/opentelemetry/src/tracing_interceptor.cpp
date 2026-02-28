#include "temporalio/extensions/opentelemetry/tracing_interceptor.h"

#include <stdexcept>
#include <utility>

namespace temporalio::extensions::opentelemetry {

// -- Internal interceptor implementations --

namespace {

/// Workflow inbound interceptor that creates spans for workflow operations.
class TracingWorkflowInbound
    : public worker::interceptors::WorkflowInboundInterceptor {
public:
    TracingWorkflowInbound(
        TracingInterceptor* root,
        worker::interceptors::WorkflowInboundInterceptor* next)
        : worker::interceptors::WorkflowInboundInterceptor(next),
          root_(root) {}

    void init(
        worker::interceptors::WorkflowOutboundInterceptor* outbound) override {
        // Forward to next; a full implementation would also wrap outbound
        // to inject trace context on outgoing calls (delay, schedule
        // activity, start child workflow, etc.)
        next().init(outbound);
    }

    coro::Task<std::any> execute_workflow_async(
        worker::interceptors::ExecuteWorkflowInput input) override {
        // TODO(otel): Create a span for "RunWorkflow:<type>"
        // - Extract parent context from headers
        // - Create span with workflow tags
        // - On completion, record result/exception and end span
        co_return co_await next().execute_workflow_async(std::move(input));
    }

    coro::Task<void> handle_signal_async(
        worker::interceptors::HandleSignalInput input) override {
        // TODO(otel): Create a span for "HandleSignal:<name>"
        co_await next().handle_signal_async(std::move(input));
    }

    std::any handle_query(
        worker::interceptors::HandleQueryInput input) override {
        // TODO(otel): Create a span for "HandleQuery:<name>"
        return next().handle_query(std::move(input));
    }

    void validate_update(
        worker::interceptors::HandleUpdateInput input) override {
        // TODO(otel): Create a span for "ValidateUpdate:<name>"
        next().validate_update(std::move(input));
    }

    coro::Task<std::any> handle_update_async(
        worker::interceptors::HandleUpdateInput input) override {
        // TODO(otel): Create a span for "HandleUpdate:<name>"
        co_return co_await next().handle_update_async(std::move(input));
    }

private:
    TracingInterceptor* root_;
};

/// Activity inbound interceptor that creates spans for activity execution.
class TracingActivityInbound
    : public worker::interceptors::ActivityInboundInterceptor {
public:
    TracingActivityInbound(
        TracingInterceptor* root,
        worker::interceptors::ActivityInboundInterceptor* next)
        : worker::interceptors::ActivityInboundInterceptor(next),
          root_(root) {}

    void init(
        worker::interceptors::ActivityOutboundInterceptor* outbound) override {
        // Forward to next; a full implementation would wrap outbound
        // to inject trace context on heartbeat calls.
        next().init(outbound);
    }

    coro::Task<std::any> execute_activity_async(
        worker::interceptors::ExecuteActivityInput input) override {
        // TODO(otel): Create a span for "RunActivity:<name>"
        // - Extract parent context from headers
        // - Create span with activity tags (workflow ID, run ID, activity ID)
        // - On completion, record result/exception and end span
        co_return co_await next().execute_activity_async(std::move(input));
    }

private:
    TracingInterceptor* root_;
};

}  // namespace

// -- TracingInterceptor --

TracingInterceptor::TracingInterceptor()
    : TracingInterceptor(TracingInterceptorOptions{}) {}

TracingInterceptor::TracingInterceptor(TracingInterceptorOptions options)
    : options_(std::move(options)) {}

TracingInterceptor::~TracingInterceptor() = default;

worker::interceptors::WorkflowInboundInterceptor*
TracingInterceptor::intercept_workflow(
    worker::interceptors::WorkflowInboundInterceptor* next) {
    auto interceptor =
        std::make_unique<TracingWorkflowInbound>(this, next);
    auto* raw = interceptor.get();
    workflow_interceptors_.push_back(std::move(interceptor));
    return raw;
}

worker::interceptors::ActivityInboundInterceptor*
TracingInterceptor::intercept_activity(
    worker::interceptors::ActivityInboundInterceptor* next) {
    auto interceptor =
        std::make_unique<TracingActivityInbound>(this, next);
    auto* raw = interceptor.get();
    activity_interceptors_.push_back(std::move(interceptor));
    return raw;
}

std::unordered_map<std::string, std::string>
TracingInterceptor::inject_context(
    std::unordered_map<std::string, std::string> headers) const {
    // TODO(otel): Use OpenTelemetry TextMapPropagator to inject
    // the current span context into the headers map.
    // This requires:
    // 1. Get the current OTel context
    // 2. Create a carrier map
    // 3. Inject using the propagator
    // 4. Serialize the carrier as JSON into headers[header_key]
    return headers;
}

bool TracingInterceptor::extract_context(
    const std::unordered_map<std::string, std::string>& headers) const {
    // TODO(otel): Use OpenTelemetry TextMapPropagator to extract
    // trace context from the headers map.
    // This requires:
    // 1. Look up headers[header_key]
    // 2. Deserialize the JSON carrier
    // 3. Extract using the propagator
    // 4. Set as current context
    auto it = headers.find(options_.header_key);
    if (it == headers.end()) {
        return false;
    }
    // Placeholder: context extraction not yet implemented
    return true;
}

}  // namespace temporalio::extensions::opentelemetry
