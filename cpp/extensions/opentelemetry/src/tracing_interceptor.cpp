#include "temporalio/extensions/opentelemetry/tracing_interceptor.h"

#include <temporalio/client/workflow_handle.h>

#include <map>
#include <stdexcept>
#include <utility>
#include <vector>

#include <opentelemetry/common/attribute_value.h>
#include <opentelemetry/common/key_value_iterable_view.h>
#include <opentelemetry/context/propagation/global_propagator.h>
#include <opentelemetry/context/propagation/text_map_propagator.h>
#include <opentelemetry/context/runtime_context.h>
#include <opentelemetry/nostd/shared_ptr.h>
#include <opentelemetry/nostd/string_view.h>
#include <opentelemetry/trace/provider.h>
#include <opentelemetry/trace/scope.h>
#include <opentelemetry/trace/span.h>
#include <opentelemetry/trace/span_startoptions.h>
#include <opentelemetry/trace/tracer.h>

#include "temporalio/activities/activity.h"
#include "temporalio/activities/activity_context.h"
#include "temporalio/workflows/workflow.h"
#include "temporalio/workflows/workflow_info.h"

namespace temporalio::extensions::opentelemetry {

// -- Helpers --

namespace {

namespace trace_api = ::opentelemetry::trace;
namespace context_api = ::opentelemetry::context;
namespace propagation_api = ::opentelemetry::context::propagation;

/// Helper to add workflow ID tag to span attributes if configured.
void add_workflow_tags(
    std::map<std::string, std::string>& attrs,
    const TracingInterceptorOptions& opts,
    const std::string& workflow_id) {
    if (opts.tag_name_workflow_id.has_value()) {
        attrs[*opts.tag_name_workflow_id] = workflow_id;
    }
}

/// Helper to add workflow ID and update ID tags to span attributes.
void add_update_tags(
    std::map<std::string, std::string>& attrs,
    const TracingInterceptorOptions& opts,
    const std::string& workflow_id) {
    add_workflow_tags(attrs, opts, workflow_id);
}

/// Record an exception on a span and set error status.
void record_exception_on_span(
    const trace_api::Span& span,
    const std::exception& e) {
    // Use const_cast because OTel C++ API AddEvent is non-const
    auto& mutable_span = const_cast<trace_api::Span&>(span);
    mutable_span.SetStatus(trace_api::StatusCode::kError, e.what());
    mutable_span.AddEvent("exception", {
        {"exception.type", "std::exception"},
        {"exception.message", e.what()}
    });
}

/// Record an exception_ptr on a span and set error status.
/// Used in coroutine methods where we catch with `catch (...)`.
void record_exception_on_span_ptr(
    ::opentelemetry::nostd::shared_ptr<trace_api::Span>& span,
    const std::exception_ptr& eptr) {
    if (!span || !eptr) {
        return;
    }
    try {
        std::rethrow_exception(eptr);
    } catch (const std::exception& e) {
        span->SetStatus(trace_api::StatusCode::kError, e.what());
        span->AddEvent("exception", {
            {"exception.type", "std::exception"},
            {"exception.message", std::string(e.what())}
        });
    } catch (...) {
        span->SetStatus(trace_api::StatusCode::kError, "unknown exception");
        span->AddEvent("exception", {
            {"exception.type", "unknown"}
        });
    }
}

/// Build an OTel attribute vector from a string map.
std::vector<std::pair<::opentelemetry::nostd::string_view,
                      ::opentelemetry::common::AttributeValue>>
attrs_from_map(const std::map<std::string, std::string>& m) {
    std::vector<std::pair<::opentelemetry::nostd::string_view,
                          ::opentelemetry::common::AttributeValue>> kv;
    kv.reserve(m.size());
    for (const auto& [key, value] : m) {
        kv.emplace_back(key, value);
    }
    return kv;
}

/// Start a span on the given tracer with string-map attributes.
::opentelemetry::nostd::shared_ptr<trace_api::Span> start_span_with_attrs(
    trace_api::Tracer& tracer,
    const std::string& name,
    const std::map<std::string, std::string>& attrs,
    trace_api::SpanKind kind,
    const context_api::Context* parent_ctx = nullptr) {
    trace_api::StartSpanOptions opts;
    opts.kind = kind;
    if (parent_ctx) {
        opts.parent = *parent_ctx;
    }
    auto kv = attrs_from_map(attrs);
    return tracer.StartSpan(
        name,
        ::opentelemetry::common::KeyValueIterableView<
            std::vector<std::pair<::opentelemetry::nostd::string_view,
                                  ::opentelemetry::common::AttributeValue>>>(kv),
        opts);
}

}  // namespace

// -- Client outbound interceptor implementation --

namespace {

/// Client outbound interceptor that creates spans for client operations
/// and injects trace context into headers.
class TracingClientOutbound
    : public client::interceptors::ClientOutboundInterceptor {
public:
    TracingClientOutbound(
        TracingInterceptor* root,
        std::unique_ptr<client::interceptors::ClientOutboundInterceptor> next_ptr)
        : client::interceptors::ClientOutboundInterceptor(std::move(next_ptr)),
          root_(root) {}

    coro::Task<client::WorkflowHandle> start_workflow(
        client::interceptors::StartWorkflowInput input) override {
        auto span_name = "StartWorkflow:" + input.workflow;
        std::map<std::string, std::string> attrs;
        add_workflow_tags(attrs, root_->options(), input.options.id);

        auto span = start_client_span(span_name, attrs);
        auto scope = trace_api::Tracer::WithActiveSpan(span);
        input.headers = root_->inject_context(std::move(input.headers));

        try {
            auto result = co_await next().start_workflow(std::move(input));
            span->End();
            co_return result;
        } catch (const std::exception& e) {
            record_exception_on_span(*span, e);
            span->End();
            throw;
        }
    }

    coro::Task<void> signal_workflow(
        client::interceptors::SignalWorkflowInput input) override {
        auto span_name = "SignalWorkflow:" + input.signal;
        std::map<std::string, std::string> attrs;
        add_workflow_tags(attrs, root_->options(), input.id);

        auto span = start_client_span(span_name, attrs);
        auto scope = trace_api::Tracer::WithActiveSpan(span);
        input.headers = root_->inject_context(std::move(input.headers));

        try {
            co_await next().signal_workflow(std::move(input));
            span->End();
        } catch (const std::exception& e) {
            record_exception_on_span(*span, e);
            span->End();
            throw;
        }
    }

    coro::Task<std::string> query_workflow(
        client::interceptors::QueryWorkflowInput input) override {
        auto span_name = "QueryWorkflow:" + input.query;
        std::map<std::string, std::string> attrs;
        add_workflow_tags(attrs, root_->options(), input.id);

        auto span = start_client_span(span_name, attrs);
        auto scope = trace_api::Tracer::WithActiveSpan(span);
        input.headers = root_->inject_context(std::move(input.headers));

        try {
            auto result = co_await next().query_workflow(std::move(input));
            span->End();
            co_return result;
        } catch (const std::exception& e) {
            record_exception_on_span(*span, e);
            span->End();
            throw;
        }
    }

    coro::Task<std::string> start_workflow_update(
        client::interceptors::StartWorkflowUpdateInput input) override {
        auto span_name = "UpdateWorkflow:" + input.update;
        std::map<std::string, std::string> attrs;
        add_update_tags(attrs, root_->options(), input.id);

        auto span = start_client_span(span_name, attrs);
        auto scope = trace_api::Tracer::WithActiveSpan(span);
        input.headers = root_->inject_context(std::move(input.headers));

        try {
            auto result =
                co_await next().start_workflow_update(std::move(input));
            span->End();
            co_return result;
        } catch (const std::exception& e) {
            record_exception_on_span(*span, e);
            span->End();
            throw;
        }
    }

    coro::Task<std::string> start_update_with_start_workflow(
        client::interceptors::StartUpdateWithStartWorkflowInput input)
        override {
        auto span_name = "UpdateWithStartWorkflow:" + input.update;
        std::map<std::string, std::string> attrs;

        auto span = start_client_span(span_name, attrs);
        auto scope = trace_api::Tracer::WithActiveSpan(span);
        input.headers = root_->inject_context(std::move(input.headers));

        try {
            auto result = co_await next().start_update_with_start_workflow(
                std::move(input));
            span->End();
            co_return result;
        } catch (const std::exception& e) {
            record_exception_on_span(*span, e);
            span->End();
            throw;
        }
    }

    coro::Task<client::WorkflowExecutionDescription> describe_workflow(
        client::interceptors::DescribeWorkflowInput input) override {
        auto span_name = std::string("DescribeWorkflow");
        std::map<std::string, std::string> attrs;
        add_workflow_tags(attrs, root_->options(), input.id);

        auto span = start_client_span(span_name, attrs);

        try {
            auto result =
                co_await next().describe_workflow(std::move(input));
            span->End();
            co_return result;
        } catch (const std::exception& e) {
            record_exception_on_span(*span, e);
            span->End();
            throw;
        }
    }

    coro::Task<void> cancel_workflow(
        client::interceptors::CancelWorkflowInput input) override {
        auto span_name = std::string("CancelWorkflow");
        std::map<std::string, std::string> attrs;
        add_workflow_tags(attrs, root_->options(), input.id);

        auto span = start_client_span(span_name, attrs);

        try {
            co_await next().cancel_workflow(std::move(input));
            span->End();
        } catch (const std::exception& e) {
            record_exception_on_span(*span, e);
            span->End();
            throw;
        }
    }

    coro::Task<void> terminate_workflow(
        client::interceptors::TerminateWorkflowInput input) override {
        auto span_name = std::string("TerminateWorkflow");
        std::map<std::string, std::string> attrs;
        add_workflow_tags(attrs, root_->options(), input.id);

        auto span = start_client_span(span_name, attrs);

        try {
            co_await next().terminate_workflow(std::move(input));
            span->End();
        } catch (const std::exception& e) {
            record_exception_on_span(*span, e);
            span->End();
            throw;
        }
    }

private:
    /// Start a client span with the given name and attributes.
    ::opentelemetry::nostd::shared_ptr<trace_api::Span> start_client_span(
        const std::string& name,
        const std::map<std::string, std::string>& attrs) {
        trace_api::StartSpanOptions opts;
        opts.kind = trace_api::SpanKind::kClient;

        // Build attributes from the map
        std::vector<std::pair<::opentelemetry::nostd::string_view,
                              ::opentelemetry::common::AttributeValue>> kv_attrs;
        kv_attrs.reserve(attrs.size());
        for (const auto& [key, value] : attrs) {
            kv_attrs.emplace_back(key, value);
        }

        return root_->client_tracer()->StartSpan(
            name,
            ::opentelemetry::common::KeyValueIterableView<
                std::vector<std::pair<::opentelemetry::nostd::string_view,
                                      ::opentelemetry::common::AttributeValue>>>(
                kv_attrs),
            opts);
    }

    TracingInterceptor* root_;
};

}  // namespace

// -- Internal worker interceptor implementations --

namespace {

/// Workflow outbound interceptor that injects trace context on outgoing calls
/// (schedule activity, start child workflow, signal, etc.).
class TracingWorkflowOutbound
    : public worker::interceptors::WorkflowOutboundInterceptor {
public:
    TracingWorkflowOutbound(
        TracingInterceptor* root,
        worker::interceptors::WorkflowOutboundInterceptor* next)
        : worker::interceptors::WorkflowOutboundInterceptor(next),
          root_(root) {}

    coro::Task<std::any> schedule_activity_async(
        worker::interceptors::ScheduleActivityInput input) override {
        auto span = start_outbound_span("StartActivity:" + input.activity);
        auto scope = trace_api::Tracer::WithActiveSpan(span);
        input.headers = root_->inject_context(std::move(input.headers));
        span->End();
        co_return co_await next().schedule_activity_async(std::move(input));
    }

    coro::Task<std::any> schedule_local_activity_async(
        worker::interceptors::ScheduleLocalActivityInput input) override {
        auto span = start_outbound_span("StartActivity:" + input.activity);
        auto scope = trace_api::Tracer::WithActiveSpan(span);
        input.headers = root_->inject_context(std::move(input.headers));
        span->End();
        co_return co_await next().schedule_local_activity_async(
            std::move(input));
    }

    coro::Task<std::any> start_child_workflow_async(
        worker::interceptors::StartChildWorkflowInput input) override {
        auto span = start_outbound_span(
            "StartChildWorkflow:" + input.workflow);
        auto scope = trace_api::Tracer::WithActiveSpan(span);
        input.headers = root_->inject_context(std::move(input.headers));
        span->End();
        co_return co_await next().start_child_workflow_async(std::move(input));
    }

    coro::Task<void> signal_child_workflow_async(
        worker::interceptors::SignalChildWorkflowInput input) override {
        auto span = start_outbound_span(
            "SignalChildWorkflow:" + input.signal);
        auto scope = trace_api::Tracer::WithActiveSpan(span);
        span->End();
        co_await next().signal_child_workflow_async(std::move(input));
    }

    coro::Task<void> signal_external_workflow_async(
        worker::interceptors::SignalExternalWorkflowInput input) override {
        auto span = start_outbound_span(
            "SignalExternalWorkflow:" + input.signal);
        auto scope = trace_api::Tracer::WithActiveSpan(span);
        span->End();
        co_await next().signal_external_workflow_async(std::move(input));
    }

private:
    ::opentelemetry::nostd::shared_ptr<trace_api::Span> start_outbound_span(
        const std::string& name) {
        trace_api::StartSpanOptions opts;
        opts.kind = trace_api::SpanKind::kClient;
        return root_->workflow_tracer()->StartSpan(name, {}, opts);
    }

    TracingInterceptor* root_;
};

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
        // Wrap the outbound interceptor to inject trace context
        // on outgoing calls (schedule activity, start child workflow, etc.)
        outbound_wrapper_ =
            std::make_unique<TracingWorkflowOutbound>(root_, outbound);
        next().init(outbound_wrapper_.get());
    }

    coro::Task<std::any> execute_workflow_async(
        worker::interceptors::ExecuteWorkflowInput input) override {
        auto tracer = root_->workflow_tracer();

        // Get workflow info for span name and tags
        std::string workflow_type;
        std::map<std::string, std::string> attrs;
        try {
            const auto& info = workflows::Workflow::info();
            workflow_type = info.workflow_type;
            if (root_->options().tag_name_workflow_id.has_value()) {
                attrs[*root_->options().tag_name_workflow_id] =
                    info.workflow_id;
            }
            if (root_->options().tag_name_run_id.has_value()) {
                attrs[*root_->options().tag_name_run_id] = info.run_id;
            }
        } catch (...) {
            // Not in workflow context
        }

        auto span = start_span_with_attrs(
            *tracer, "RunWorkflow:" + workflow_type, attrs,
            trace_api::SpanKind::kServer);
        auto scope = trace_api::Tracer::WithActiveSpan(span);

        std::exception_ptr eptr;
        try {
            auto result =
                co_await next().execute_workflow_async(std::move(input));
            span->SetStatus(trace_api::StatusCode::kOk);
            span->End();
            co_return result;
        } catch (...) {
            eptr = std::current_exception();
        }
        record_exception_on_span_ptr(span, eptr);
        span->End();
        std::rethrow_exception(eptr);
    }

    coro::Task<void> handle_signal_async(
        worker::interceptors::HandleSignalInput input) override {
        auto tracer = root_->workflow_tracer();
        auto attrs = build_workflow_attrs();

        auto span = start_span_with_attrs(
            *tracer, "HandleSignal:" + input.signal, attrs,
            trace_api::SpanKind::kServer);
        auto scope = trace_api::Tracer::WithActiveSpan(span);

        std::exception_ptr eptr;
        try {
            co_await next().handle_signal_async(std::move(input));
            span->SetStatus(trace_api::StatusCode::kOk);
            span->End();
            co_return;
        } catch (...) {
            eptr = std::current_exception();
        }
        record_exception_on_span_ptr(span, eptr);
        span->End();
        std::rethrow_exception(eptr);
    }

    std::any handle_query(
        worker::interceptors::HandleQueryInput input) override {
        auto tracer = root_->workflow_tracer();
        auto attrs = build_workflow_attrs();

        auto span = start_span_with_attrs(
            *tracer, "HandleQuery:" + input.query, attrs,
            trace_api::SpanKind::kServer);
        auto scope = trace_api::Tracer::WithActiveSpan(span);

        try {
            auto result = next().handle_query(std::move(input));
            span->SetStatus(trace_api::StatusCode::kOk);
            span->End();
            return result;
        } catch (const std::exception& e) {
            record_exception_on_span(*span, e);
            span->End();
            throw;
        }
    }

    void validate_update(
        worker::interceptors::HandleUpdateInput input) override {
        auto tracer = root_->workflow_tracer();
        auto attrs = build_workflow_attrs();

        auto span = start_span_with_attrs(
            *tracer, "ValidateUpdate:" + input.update, attrs,
            trace_api::SpanKind::kServer);
        auto scope = trace_api::Tracer::WithActiveSpan(span);

        try {
            next().validate_update(std::move(input));
            span->SetStatus(trace_api::StatusCode::kOk);
            span->End();
        } catch (const std::exception& e) {
            record_exception_on_span(*span, e);
            span->End();
            throw;
        }
    }

    coro::Task<std::any> handle_update_async(
        worker::interceptors::HandleUpdateInput input) override {
        auto tracer = root_->workflow_tracer();
        auto attrs = build_workflow_attrs();

        // Add update ID tag if configured
        if (root_->options().tag_name_update_id.has_value() &&
            !input.id.empty()) {
            attrs[*root_->options().tag_name_update_id] = input.id;
        }

        auto span = start_span_with_attrs(
            *tracer, "HandleUpdate:" + input.update, attrs,
            trace_api::SpanKind::kServer);
        auto scope = trace_api::Tracer::WithActiveSpan(span);

        std::exception_ptr eptr;
        try {
            auto result =
                co_await next().handle_update_async(std::move(input));
            span->SetStatus(trace_api::StatusCode::kOk);
            span->End();
            co_return result;
        } catch (...) {
            eptr = std::current_exception();
        }
        record_exception_on_span_ptr(span, eptr);
        span->End();
        std::rethrow_exception(eptr);
    }

private:
    /// Build workflow tag attributes from the current workflow context.
    std::map<std::string, std::string> build_workflow_attrs() const {
        std::map<std::string, std::string> attrs;
        try {
            const auto& info = workflows::Workflow::info();
            if (root_->options().tag_name_workflow_id.has_value()) {
                attrs[*root_->options().tag_name_workflow_id] =
                    info.workflow_id;
            }
            if (root_->options().tag_name_run_id.has_value()) {
                attrs[*root_->options().tag_name_run_id] = info.run_id;
            }
        } catch (...) {
            // Not in workflow context
        }
        return attrs;
    }

    TracingInterceptor* root_;
    std::unique_ptr<TracingWorkflowOutbound> outbound_wrapper_;
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
        next().init(outbound);
    }

    coro::Task<std::any> execute_activity_async(
        worker::interceptors::ExecuteActivityInput input) override {
        auto tracer = root_->activity_tracer();

        // Extract parent context from activity headers
        const context_api::Context* parent_ctx_ptr = nullptr;
        context_api::Context extracted_ctx =
            context_api::RuntimeContext::GetCurrent();
        if (!input.headers.empty()) {
            extracted_ctx = root_->extract_context(input.headers);
            parent_ctx_ptr = &extracted_ctx;
        }

        // Get activity name
        std::string activity_name;
        if (input.activity) {
            activity_name = input.activity->name();
        }

        // Build tags from activity context
        std::map<std::string, std::string> attrs;
        try {
            const auto& act_info =
                activities::ActivityExecutionContext::current().info();
            if (root_->options().tag_name_workflow_id.has_value() &&
                act_info.workflow_id.has_value()) {
                attrs[*root_->options().tag_name_workflow_id] =
                    *act_info.workflow_id;
            }
            if (root_->options().tag_name_run_id.has_value() &&
                act_info.workflow_run_id.has_value()) {
                attrs[*root_->options().tag_name_run_id] =
                    *act_info.workflow_run_id;
            }
            if (root_->options().tag_name_activity_id.has_value()) {
                attrs[*root_->options().tag_name_activity_id] =
                    act_info.activity_id;
            }
        } catch (...) {
            // No activity context available
        }

        auto span = start_span_with_attrs(
            *tracer, "RunActivity:" + activity_name, attrs,
            trace_api::SpanKind::kServer, parent_ctx_ptr);
        auto scope = trace_api::Tracer::WithActiveSpan(span);

        std::exception_ptr eptr;
        try {
            auto result =
                co_await next().execute_activity_async(std::move(input));
            span->SetStatus(trace_api::StatusCode::kOk);
            span->End();
            co_return result;
        } catch (...) {
            eptr = std::current_exception();
        }
        record_exception_on_span_ptr(span, eptr);
        span->End();
        std::rethrow_exception(eptr);
    }

private:
    TracingInterceptor* root_;
};

}  // namespace

// -- TracingInterceptor --

TracingInterceptor::TracingInterceptor()
    : TracingInterceptor(TracingInterceptorOptions{}) {}

TracingInterceptor::TracingInterceptor(TracingInterceptorOptions options)
    : options_(std::move(options)),
      client_tracer_(
          trace_api::Provider::GetTracerProvider()->GetTracer(kClientSourceName)),
      workflow_tracer_(
          trace_api::Provider::GetTracerProvider()->GetTracer(kWorkflowSourceName)),
      activity_tracer_(
          trace_api::Provider::GetTracerProvider()->GetTracer(kActivitySourceName)) {}

TracingInterceptor::~TracingInterceptor() = default;

std::unique_ptr<client::interceptors::ClientOutboundInterceptor>
TracingInterceptor::intercept_client(
    std::unique_ptr<client::interceptors::ClientOutboundInterceptor> next) {
    return std::make_unique<TracingClientOutbound>(this, std::move(next));
}

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

// -- HeaderCarrier for TextMapPropagator --

namespace {

/// TextMapCarrier implementation backed by an unordered_map of headers.
struct HeaderCarrier : propagation_api::TextMapCarrier {
    std::unordered_map<std::string, std::string>& hdrs;
    explicit HeaderCarrier(
        std::unordered_map<std::string, std::string>& h)
        : hdrs(h) {}
    ::opentelemetry::nostd::string_view Get(
        ::opentelemetry::nostd::string_view key) const noexcept override {
        auto it = hdrs.find(std::string(key));
        if (it != hdrs.end()) {
            return it->second;
        }
        return "";
    }
    void Set(::opentelemetry::nostd::string_view key,
             ::opentelemetry::nostd::string_view value) noexcept override {
        hdrs[std::string(key)] = std::string(value);
    }
};

/// Const TextMapCarrier for extraction (read-only).
struct ConstHeaderCarrier : propagation_api::TextMapCarrier {
    const std::unordered_map<std::string, std::string>& hdrs;
    explicit ConstHeaderCarrier(
        const std::unordered_map<std::string, std::string>& h)
        : hdrs(h) {}
    ::opentelemetry::nostd::string_view Get(
        ::opentelemetry::nostd::string_view key) const noexcept override {
        auto it = hdrs.find(std::string(key));
        if (it != hdrs.end()) {
            return it->second;
        }
        return "";
    }
    void Set(::opentelemetry::nostd::string_view /*key*/,
             ::opentelemetry::nostd::string_view /*value*/) noexcept override {
        // Read-only carrier, Set is a no-op
    }
};

}  // namespace

std::unordered_map<std::string, std::string>
TracingInterceptor::inject_context(
    std::unordered_map<std::string, std::string> headers,
    const ::opentelemetry::context::Context& context) const {
    auto propagator =
        propagation_api::GlobalTextMapPropagator::GetGlobalPropagator();
    if (!propagator) {
        return headers;
    }

    HeaderCarrier carrier(headers);
    propagator->Inject(carrier, context);
    return headers;
}

std::unordered_map<std::string, std::string>
TracingInterceptor::inject_context(
    std::unordered_map<std::string, std::string> headers) const {
    return inject_context(
        std::move(headers), context_api::RuntimeContext::GetCurrent());
}

::opentelemetry::context::Context TracingInterceptor::extract_context(
    const std::unordered_map<std::string, std::string>& headers) const {
    auto propagator =
        propagation_api::GlobalTextMapPropagator::GetGlobalPropagator();
    if (!propagator) {
        return context_api::RuntimeContext::GetCurrent();
    }

    ConstHeaderCarrier carrier(headers);
    auto current_ctx = context_api::RuntimeContext::GetCurrent();
    return propagator->Extract(carrier, current_ctx);
}

bool TracingInterceptor::has_context(
    const std::unordered_map<std::string, std::string>& headers) const {
    return headers.find(options_.header_key) != headers.end();
}

}  // namespace temporalio::extensions::opentelemetry
