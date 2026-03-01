#pragma once

/// @file tracing_interceptor.h
/// @brief OpenTelemetry tracing interceptor for the Temporal C++ SDK.
///
/// TracingInterceptor implements both IClientInterceptor and
/// IWorkerInterceptor to create and propagate OpenTelemetry spans for
/// client calls, workflow executions, activity executions, and Nexus
/// operations.
///
/// Usage:
///   auto interceptor = std::make_shared<TracingInterceptor>();
///   // Add to client options
///   client_options.interceptors.push_back(interceptor);
///   // It will automatically apply to workers created from this client.

#include <memory>
#include <string>
#include <unordered_map>
#include <vector>

#include <opentelemetry/context/propagation/text_map_propagator.h>
#include <opentelemetry/nostd/shared_ptr.h>
#include <opentelemetry/trace/tracer.h>

#include "temporalio/extensions/opentelemetry/tracing_options.h"
#include "temporalio/client/interceptors/client_interceptor.h"
#include "temporalio/worker/interceptors/worker_interceptor.h"

namespace temporalio::extensions::opentelemetry {

/// OpenTelemetry tracing interceptor for both client and worker sides.
///
/// Creates spans for:
/// - Client: StartWorkflow, SignalWorkflow, QueryWorkflow, etc.
/// - Workflow: Execute, HandleSignal, HandleQuery, HandleUpdate
/// - Activity: Execute
/// - Nexus: StartOperation, CancelOperation
///
/// Propagates trace context via Temporal headers using the configured
/// header key.
class TracingInterceptor
    : public client::interceptors::IClientInterceptor,
      public worker::interceptors::IWorkerInterceptor,
      public std::enable_shared_from_this<TracingInterceptor> {
public:
    /// Source names used for span creation.
    static constexpr const char* kClientSourceName =
        "Temporalio.Extensions.OpenTelemetry.Client";
    static constexpr const char* kWorkflowSourceName =
        "Temporalio.Extensions.OpenTelemetry.Workflow";
    static constexpr const char* kActivitySourceName =
        "Temporalio.Extensions.OpenTelemetry.Activity";
    static constexpr const char* kNexusSourceName =
        "Temporalio.Extensions.OpenTelemetry.Nexus";

    /// Create a tracing interceptor with default options.
    TracingInterceptor();

    /// Create a tracing interceptor with the given options.
    explicit TracingInterceptor(TracingInterceptorOptions options);

    ~TracingInterceptor() override;

    /// Get the options.
    const TracingInterceptorOptions& options() const noexcept {
        return options_;
    }

    /// Get the workflow tracer.
    ::opentelemetry::nostd::shared_ptr<::opentelemetry::trace::Tracer>
    workflow_tracer() const noexcept {
        return workflow_tracer_;
    }

    /// Get the activity tracer.
    ::opentelemetry::nostd::shared_ptr<::opentelemetry::trace::Tracer>
    activity_tracer() const noexcept {
        return activity_tracer_;
    }

    /// Get the client tracer.
    ::opentelemetry::nostd::shared_ptr<::opentelemetry::trace::Tracer>
    client_tracer() const noexcept {
        return client_tracer_;
    }

    // -- IClientInterceptor --

    /// Create a client outbound interceptor that wraps the given next.
    std::unique_ptr<client::interceptors::ClientOutboundInterceptor>
    intercept_client(
        std::unique_ptr<client::interceptors::ClientOutboundInterceptor> next)
        override;

    // -- IWorkerInterceptor --

    /// Create a workflow inbound interceptor that wraps the given next.
    worker::interceptors::WorkflowInboundInterceptor* intercept_workflow(
        worker::interceptors::WorkflowInboundInterceptor* next) override;

    /// Create an activity inbound interceptor that wraps the given next.
    worker::interceptors::ActivityInboundInterceptor* intercept_activity(
        worker::interceptors::ActivityInboundInterceptor* next) override;

    /// Inject current OTel context into Temporal headers using the
    /// configured propagator.
    /// @param headers Existing headers (may be empty).
    /// @param context The OTel context to inject. If empty, uses current
    ///   context.
    /// @return Updated headers with trace context injected.
    std::unordered_map<std::string, std::string> inject_context(
        std::unordered_map<std::string, std::string> headers,
        const ::opentelemetry::context::Context& context) const;

    /// Inject current OTel context into Temporal headers.
    /// Uses the currently active OTel context.
    /// @param headers Existing headers (may be empty).
    /// @return Updated headers with trace context injected.
    std::unordered_map<std::string, std::string> inject_context(
        std::unordered_map<std::string, std::string> headers) const;

    /// Extract trace context from Temporal headers into an OTel context.
    /// @param headers Headers to extract from.
    /// @return The extracted context, or an empty context if not found.
    ::opentelemetry::context::Context extract_context(
        const std::unordered_map<std::string, std::string>& headers) const;

    /// Check whether headers contain trace context.
    /// @param headers Headers to check.
    /// @return True if trace context was found.
    bool has_context(
        const std::unordered_map<std::string, std::string>& headers) const;

private:
    TracingInterceptorOptions options_;

    // Tracers for each domain
    ::opentelemetry::nostd::shared_ptr<::opentelemetry::trace::Tracer>
        client_tracer_;
    ::opentelemetry::nostd::shared_ptr<::opentelemetry::trace::Tracer>
        workflow_tracer_;
    ::opentelemetry::nostd::shared_ptr<::opentelemetry::trace::Tracer>
        activity_tracer_;

    // Owned interceptor instances (kept alive for the interceptor chain)
    std::vector<
        std::unique_ptr<worker::interceptors::WorkflowInboundInterceptor>>
        workflow_interceptors_;
    std::vector<
        std::unique_ptr<worker::interceptors::ActivityInboundInterceptor>>
        activity_interceptors_;
    std::vector<
        std::unique_ptr<worker::interceptors::WorkflowOutboundInterceptor>>
        workflow_outbound_interceptors_;
};

}  // namespace temporalio::extensions::opentelemetry
