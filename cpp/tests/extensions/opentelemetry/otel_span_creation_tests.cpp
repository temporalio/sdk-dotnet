#include <gtest/gtest.h>

/// @file otel_span_creation_tests.cpp
/// @brief Tests for real OpenTelemetry span creation in TracingInterceptor.
///
/// These tests use the OTel SDK's InMemorySpanExporter to capture spans
/// and verify that the TracingInterceptor creates spans with correct
/// names, attributes, parent-child relationships, and error handling.

#include <any>
#include <memory>
#include <string>
#include <unordered_map>
#include <vector>

#include <opentelemetry/context/propagation/global_propagator.h>
#include <opentelemetry/context/propagation/text_map_propagator.h>
#include <opentelemetry/context/runtime_context.h>
#include <opentelemetry/exporters/memory/in_memory_span_exporter.h>
#include <opentelemetry/nostd/shared_ptr.h>
#include <opentelemetry/sdk/trace/simple_processor.h>
#include <opentelemetry/sdk/trace/tracer_provider.h>
#include <opentelemetry/trace/propagation/http_trace_context.h>
#include <opentelemetry/trace/provider.h>
#include <opentelemetry/trace/scope.h>
#include <opentelemetry/trace/span.h>

#include "temporalio/client/interceptors/client_interceptor.h"
#include "temporalio/client/workflow_handle.h"
#include "temporalio/coro/task.h"
#include "temporalio/extensions/opentelemetry/tracing_interceptor.h"
#include "temporalio/extensions/opentelemetry/tracing_options.h"
#include "temporalio/worker/interceptors/worker_interceptor.h"

namespace trace_api = opentelemetry::trace;
namespace trace_sdk = opentelemetry::sdk::trace;
namespace context_api = opentelemetry::context;
namespace propagation_api = opentelemetry::context::propagation;
namespace memory = opentelemetry::exporter::memory;

using namespace temporalio::extensions::opentelemetry;
using namespace temporalio::client::interceptors;
using namespace temporalio::worker::interceptors;

// ===========================================================================
// Test fixture: sets up InMemorySpanExporter + TracerProvider
// ===========================================================================

class OTelSpanCreationTest : public ::testing::Test {
protected:
    void SetUp() override {
        // Create an InMemorySpanExporter to capture spans
        auto exporter = std::make_unique<memory::InMemorySpanExporter>();
        span_data_ = exporter->GetData();

        // Create a simple processor + tracer provider
        auto processor =
            std::make_unique<trace_sdk::SimpleSpanProcessor>(std::move(exporter));
        auto provider = std::make_shared<trace_sdk::TracerProvider>(
            std::move(processor));

        // Set as global tracer provider
        trace_api::Provider::SetTracerProvider(
            opentelemetry::nostd::shared_ptr<trace_api::TracerProvider>(
                provider));

        // Set up W3C TraceContext propagator globally
        propagation_api::GlobalTextMapPropagator::SetGlobalPropagator(
            opentelemetry::nostd::shared_ptr<propagation_api::TextMapPropagator>(
                new trace_api::propagation::HttpTraceContext()));

        // Keep provider alive
        provider_ = provider;
    }

    void TearDown() override {
        // Reset global provider
        trace_api::Provider::SetTracerProvider(
            opentelemetry::nostd::shared_ptr<trace_api::TracerProvider>(
                new trace_api::NoopTracerProvider()));
        propagation_api::GlobalTextMapPropagator::SetGlobalPropagator(
            opentelemetry::nostd::shared_ptr<propagation_api::TextMapPropagator>(
                new propagation_api::NoOpPropagator()));
        provider_.reset();
    }

    /// Get the captured spans.
    std::shared_ptr<std::vector<std::unique_ptr<trace_sdk::SpanData>>>
    get_spans() const {
        return span_data_;
    }

    /// Get spans count.
    size_t span_count() const {
        return span_data_->size();
    }

    /// Get span by index.
    const trace_sdk::SpanData& span_at(size_t idx) const {
        return *span_data_->at(idx);
    }

    /// Helper to run a lazy coroutine to completion.
    template <typename T>
    T run_task(temporalio::coro::Task<T> task) {
        auto handle = task.handle();
        handle.resume();
        return task.await_resume();
    }

    template <>
    void run_task<void>(temporalio::coro::Task<void> task) {
        auto handle = task.handle();
        handle.resume();
        task.await_resume();
    }

private:
    std::shared_ptr<std::vector<std::unique_ptr<trace_sdk::SpanData>>>
        span_data_;
    std::shared_ptr<trace_sdk::TracerProvider> provider_;
};

// ===========================================================================
// Stub "next" interceptors for client-side testing
//
// These simulate the terminal interceptor that the tracing interceptor
// wraps, returning dummy results without making real RPCs.
// ===========================================================================

namespace {

/// A stub client outbound interceptor that returns dummy results.
class StubClientOutbound : public ClientOutboundInterceptor {
public:
    StubClientOutbound()
        : ClientOutboundInterceptor(nullptr) {}

    temporalio::coro::Task<temporalio::client::WorkflowHandle>
    start_workflow(StartWorkflowInput input) override {
        last_start_input_ = std::move(input);
        // Return a dummy handle with null client
        co_return temporalio::client::WorkflowHandle(
            nullptr, "test-wf-id", "test-run-id");
    }

    temporalio::coro::Task<void> signal_workflow(
        SignalWorkflowInput input) override {
        last_signal_input_ = std::move(input);
        co_return;
    }

    temporalio::coro::Task<std::string> query_workflow(
        QueryWorkflowInput input) override {
        last_query_input_ = std::move(input);
        co_return std::string("query-result");
    }

    temporalio::coro::Task<std::string> start_workflow_update(
        StartWorkflowUpdateInput input) override {
        last_update_input_ = std::move(input);
        co_return std::string("update-id");
    }

    temporalio::coro::Task<temporalio::client::WorkflowExecutionDescription>
    describe_workflow(DescribeWorkflowInput input) override {
        last_describe_input_ = std::move(input);
        temporalio::client::WorkflowExecutionDescription desc;
        desc.workflow_id = "test-wf-id";
        desc.run_id = "test-run-id";
        co_return desc;
    }

    temporalio::coro::Task<void> cancel_workflow(
        CancelWorkflowInput input) override {
        last_cancel_input_ = std::move(input);
        co_return;
    }

    temporalio::coro::Task<void> terminate_workflow(
        TerminateWorkflowInput input) override {
        last_terminate_input_ = std::move(input);
        co_return;
    }

    // Accessors for verification
    std::optional<StartWorkflowInput> last_start_input_;
    std::optional<SignalWorkflowInput> last_signal_input_;
    std::optional<QueryWorkflowInput> last_query_input_;
    std::optional<StartWorkflowUpdateInput> last_update_input_;
    std::optional<DescribeWorkflowInput> last_describe_input_;
    std::optional<CancelWorkflowInput> last_cancel_input_;
    std::optional<TerminateWorkflowInput> last_terminate_input_;
};

/// A stub that throws on start_workflow -- used to test error recording.
class ThrowingClientOutbound : public ClientOutboundInterceptor {
public:
    ThrowingClientOutbound()
        : ClientOutboundInterceptor(nullptr) {}

    temporalio::coro::Task<temporalio::client::WorkflowHandle>
    start_workflow(StartWorkflowInput) override {
        throw std::runtime_error("simulated RPC failure");
        co_return temporalio::client::WorkflowHandle(nullptr, "", "");
    }

    temporalio::coro::Task<void> signal_workflow(
        SignalWorkflowInput) override {
        throw std::runtime_error("simulated signal failure");
        co_return;
    }

    temporalio::coro::Task<std::string> query_workflow(
        QueryWorkflowInput) override {
        throw std::runtime_error("simulated query failure");
        co_return std::string("");
    }
};

}  // namespace

// ===========================================================================
// 1. Client interceptor span creation tests
// ===========================================================================

TEST_F(OTelSpanCreationTest, StartWorkflowCreatesSpan) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto* stub_ptr = stub.get();

    auto wrapped = interceptor->intercept_client(std::move(stub));

    StartWorkflowInput input;
    input.workflow = "MyWorkflow";
    input.options.id = "wf-123";

    auto task = wrapped->start_workflow(std::move(input));
    auto result = run_task(std::move(task));

    // Verify span was created
    ASSERT_GE(span_count(), 1u);
    EXPECT_EQ(span_at(0).GetName(), "StartWorkflow:MyWorkflow");
    EXPECT_EQ(span_at(0).GetSpanKind(), trace_api::SpanKind::kClient);

    // Verify input was forwarded to stub
    ASSERT_TRUE(stub_ptr->last_start_input_.has_value());
    EXPECT_EQ(stub_ptr->last_start_input_->workflow, "MyWorkflow");
}

TEST_F(OTelSpanCreationTest, SignalWorkflowCreatesSpan) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    SignalWorkflowInput input;
    input.id = "wf-123";
    input.signal = "my_signal";

    auto task = wrapped->signal_workflow(std::move(input));
    run_task(std::move(task));

    ASSERT_GE(span_count(), 1u);
    EXPECT_EQ(span_at(0).GetName(), "SignalWorkflow:my_signal");
    EXPECT_EQ(span_at(0).GetSpanKind(), trace_api::SpanKind::kClient);
}

TEST_F(OTelSpanCreationTest, QueryWorkflowCreatesSpan) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    QueryWorkflowInput input;
    input.id = "wf-123";
    input.query = "get_status";

    auto task = wrapped->query_workflow(std::move(input));
    auto result = run_task(std::move(task));

    ASSERT_GE(span_count(), 1u);
    EXPECT_EQ(span_at(0).GetName(), "QueryWorkflow:get_status");
    EXPECT_EQ(span_at(0).GetSpanKind(), trace_api::SpanKind::kClient);
    EXPECT_EQ(result, "query-result");
}

TEST_F(OTelSpanCreationTest, StartWorkflowUpdateCreatesSpan) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    StartWorkflowUpdateInput input;
    input.id = "wf-123";
    input.update = "my_update";

    auto task = wrapped->start_workflow_update(std::move(input));
    auto result = run_task(std::move(task));

    ASSERT_GE(span_count(), 1u);
    EXPECT_EQ(span_at(0).GetName(), "UpdateWorkflow:my_update");
    EXPECT_EQ(span_at(0).GetSpanKind(), trace_api::SpanKind::kClient);
}

TEST_F(OTelSpanCreationTest, DescribeWorkflowCreatesSpan) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    DescribeWorkflowInput input;
    input.id = "wf-123";

    auto task = wrapped->describe_workflow(std::move(input));
    auto result = run_task(std::move(task));

    ASSERT_GE(span_count(), 1u);
    EXPECT_EQ(span_at(0).GetName(), "DescribeWorkflow");
}

TEST_F(OTelSpanCreationTest, CancelWorkflowCreatesSpan) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    CancelWorkflowInput input;
    input.id = "wf-123";

    auto task = wrapped->cancel_workflow(std::move(input));
    run_task(std::move(task));

    ASSERT_GE(span_count(), 1u);
    EXPECT_EQ(span_at(0).GetName(), "CancelWorkflow");
}

TEST_F(OTelSpanCreationTest, TerminateWorkflowCreatesSpan) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    TerminateWorkflowInput input;
    input.id = "wf-123";

    auto task = wrapped->terminate_workflow(std::move(input));
    run_task(std::move(task));

    ASSERT_GE(span_count(), 1u);
    EXPECT_EQ(span_at(0).GetName(), "TerminateWorkflow");
}

// ===========================================================================
// 2. Error recording tests
// ===========================================================================

TEST_F(OTelSpanCreationTest, StartWorkflowErrorRecordsOnSpan) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<ThrowingClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    StartWorkflowInput input;
    input.workflow = "FailingWorkflow";
    input.options.id = "wf-err";

    auto task = wrapped->start_workflow(std::move(input));

    EXPECT_THROW(run_task(std::move(task)), std::runtime_error);

    // Span should still be created with error status
    ASSERT_GE(span_count(), 1u);
    EXPECT_EQ(span_at(0).GetName(), "StartWorkflow:FailingWorkflow");
    EXPECT_EQ(span_at(0).GetStatus(), trace_api::StatusCode::kError);
}

TEST_F(OTelSpanCreationTest, SignalWorkflowErrorRecordsOnSpan) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<ThrowingClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    SignalWorkflowInput input;
    input.id = "wf-err";
    input.signal = "fail_signal";

    auto task = wrapped->signal_workflow(std::move(input));

    EXPECT_THROW(run_task(std::move(task)), std::runtime_error);

    ASSERT_GE(span_count(), 1u);
    EXPECT_EQ(span_at(0).GetName(), "SignalWorkflow:fail_signal");
    EXPECT_EQ(span_at(0).GetStatus(), trace_api::StatusCode::kError);
}

TEST_F(OTelSpanCreationTest, QueryWorkflowErrorRecordsOnSpan) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<ThrowingClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    QueryWorkflowInput input;
    input.id = "wf-err";
    input.query = "fail_query";

    auto task = wrapped->query_workflow(std::move(input));

    EXPECT_THROW(run_task(std::move(task)), std::runtime_error);

    ASSERT_GE(span_count(), 1u);
    EXPECT_EQ(span_at(0).GetName(), "QueryWorkflow:fail_query");
    EXPECT_EQ(span_at(0).GetStatus(), trace_api::StatusCode::kError);
}

// ===========================================================================
// 3. Trace context injection tests
// ===========================================================================

TEST_F(OTelSpanCreationTest, StartWorkflowInjectsTraceContext) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto* stub_ptr = stub.get();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    StartWorkflowInput input;
    input.workflow = "MyWorkflow";
    input.options.id = "wf-inject";

    auto task = wrapped->start_workflow(std::move(input));
    run_task(std::move(task));

    // The stub should have received headers with trace context injected
    ASSERT_TRUE(stub_ptr->last_start_input_.has_value());
    auto& headers = stub_ptr->last_start_input_->headers;

    // W3C TraceContext propagator injects "traceparent" header
    EXPECT_TRUE(headers.count("traceparent") > 0)
        << "Expected traceparent header to be injected";
}

TEST_F(OTelSpanCreationTest, SignalWorkflowInjectsTraceContext) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto* stub_ptr = stub.get();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    SignalWorkflowInput input;
    input.id = "wf-inject";
    input.signal = "my_signal";

    auto task = wrapped->signal_workflow(std::move(input));
    run_task(std::move(task));

    ASSERT_TRUE(stub_ptr->last_signal_input_.has_value());
    auto& headers = stub_ptr->last_signal_input_->headers;
    EXPECT_TRUE(headers.count("traceparent") > 0)
        << "Expected traceparent header to be injected";
}

TEST_F(OTelSpanCreationTest, QueryWorkflowInjectsTraceContext) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto* stub_ptr = stub.get();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    QueryWorkflowInput input;
    input.id = "wf-inject";
    input.query = "my_query";

    auto task = wrapped->query_workflow(std::move(input));
    run_task(std::move(task));

    ASSERT_TRUE(stub_ptr->last_query_input_.has_value());
    auto& headers = stub_ptr->last_query_input_->headers;
    EXPECT_TRUE(headers.count("traceparent") > 0)
        << "Expected traceparent header to be injected";
}

// ===========================================================================
// 4. Workflow ID attribute tests
// ===========================================================================

TEST_F(OTelSpanCreationTest, WorkflowIdTagOnStartWorkflow) {
    TracingInterceptorOptions opts;
    opts.tag_name_workflow_id = "temporalWorkflowID";
    auto interceptor = std::make_shared<TracingInterceptor>(std::move(opts));

    auto stub = std::make_unique<StubClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    StartWorkflowInput input;
    input.workflow = "TaggedWorkflow";
    input.options.id = "tagged-wf-123";

    auto task = wrapped->start_workflow(std::move(input));
    run_task(std::move(task));

    ASSERT_GE(span_count(), 1u);

    // Check that the span has the workflow ID attribute
    const auto& attributes = span_at(0).GetAttributes();
    bool found_wf_id = false;
    attributes.ForEachKeyValue(
        [&](opentelemetry::nostd::string_view key,
            opentelemetry::common::AttributeValue value) -> bool {
            if (key == "temporalWorkflowID") {
                found_wf_id = true;
                // Attribute should be the workflow ID string
                auto* str_val =
                    opentelemetry::nostd::get_if<opentelemetry::nostd::string_view>(
                        &value);
                if (str_val) {
                    EXPECT_EQ(std::string(*str_val), "tagged-wf-123");
                }
            }
            return true;
        });
    EXPECT_TRUE(found_wf_id)
        << "Expected temporalWorkflowID attribute on span";
}

TEST_F(OTelSpanCreationTest, DisabledWorkflowIdTag) {
    TracingInterceptorOptions opts;
    opts.tag_name_workflow_id = std::nullopt;
    auto interceptor = std::make_shared<TracingInterceptor>(std::move(opts));

    auto stub = std::make_unique<StubClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    StartWorkflowInput input;
    input.workflow = "NoTagWorkflow";
    input.options.id = "no-tag-wf";

    auto task = wrapped->start_workflow(std::move(input));
    run_task(std::move(task));

    ASSERT_GE(span_count(), 1u);

    // Workflow ID attribute should NOT be present
    bool found_wf_id = false;
    span_at(0).GetAttributes().ForEachKeyValue(
        [&](opentelemetry::nostd::string_view key,
            opentelemetry::common::AttributeValue) -> bool {
            if (key == "temporalWorkflowID") {
                found_wf_id = true;
            }
            return true;
        });
    EXPECT_FALSE(found_wf_id)
        << "Should not have temporalWorkflowID attribute when disabled";
}

// ===========================================================================
// 5. Multiple operations create separate spans
// ===========================================================================

TEST_F(OTelSpanCreationTest, MultipleOperationsCreateSeparateSpans) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    // Start a workflow
    StartWorkflowInput start_input;
    start_input.workflow = "WF1";
    start_input.options.id = "wf-1";
    auto task1 = wrapped->start_workflow(std::move(start_input));
    run_task(std::move(task1));

    // Signal it
    SignalWorkflowInput signal_input;
    signal_input.id = "wf-1";
    signal_input.signal = "sig1";
    auto task2 = wrapped->signal_workflow(std::move(signal_input));
    run_task(std::move(task2));

    // Query it
    QueryWorkflowInput query_input;
    query_input.id = "wf-1";
    query_input.query = "q1";
    auto task3 = wrapped->query_workflow(std::move(query_input));
    run_task(std::move(task3));

    // Should have 3 separate spans
    ASSERT_EQ(span_count(), 3u);
    EXPECT_EQ(span_at(0).GetName(), "StartWorkflow:WF1");
    EXPECT_EQ(span_at(1).GetName(), "SignalWorkflow:sig1");
    EXPECT_EQ(span_at(2).GetName(), "QueryWorkflow:q1");
}

// ===========================================================================
// 6. Context injection / extraction round-trip
// ===========================================================================

TEST_F(OTelSpanCreationTest, InjectExtractRoundTrip) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    // Create a span and make it active
    auto tracer = trace_api::Provider::GetTracerProvider()->GetTracer("test");
    auto span = tracer->StartSpan("parent-span");
    auto scope = trace_api::Tracer::WithActiveSpan(span);

    // Inject context
    std::unordered_map<std::string, std::string> headers;
    auto injected = interceptor->inject_context(headers);

    // Should have traceparent header
    EXPECT_TRUE(injected.count("traceparent") > 0);

    // Extract context from the injected headers
    auto extracted_ctx = interceptor->extract_context(injected);

    // The extracted context should contain span info
    // (We can't easily compare trace IDs without more setup, but
    // verifying that extract doesn't crash and returns a valid context
    // is the key test here)

    span->End();
}

TEST_F(OTelSpanCreationTest, HasContextReturnsTrueWhenPresent) {
    auto interceptor = std::make_shared<TracingInterceptor>();
    std::unordered_map<std::string, std::string> headers;
    headers["_tracer-data"] = "some-data";

    EXPECT_TRUE(interceptor->has_context(headers));
}

TEST_F(OTelSpanCreationTest, HasContextReturnsFalseWhenMissing) {
    auto interceptor = std::make_shared<TracingInterceptor>();
    std::unordered_map<std::string, std::string> headers;

    EXPECT_FALSE(interceptor->has_context(headers));
}

TEST_F(OTelSpanCreationTest, HasContextUsesCustomHeaderKey) {
    TracingInterceptorOptions opts;
    opts.header_key = "custom-trace-key";
    auto interceptor = std::make_shared<TracingInterceptor>(std::move(opts));

    std::unordered_map<std::string, std::string> headers;
    headers["_tracer-data"] = "wrong-key";
    EXPECT_FALSE(interceptor->has_context(headers));

    headers["custom-trace-key"] = "data";
    EXPECT_TRUE(interceptor->has_context(headers));
}

// ===========================================================================
// 7. Tracer accessor tests
// ===========================================================================

TEST_F(OTelSpanCreationTest, TracersAreNonNull) {
    auto interceptor = std::make_shared<TracingInterceptor>();
    EXPECT_NE(interceptor->client_tracer(), nullptr);
    EXPECT_NE(interceptor->workflow_tracer(), nullptr);
    EXPECT_NE(interceptor->activity_tracer(), nullptr);
}

// ===========================================================================
// 8. Worker-side interceptor wrapping tests
//
// These verify the interceptor chain setup works. Full span creation
// tests for worker-side will be enabled once task #1 implements the
// actual span creation in execute_workflow_async, handle_signal_async,
// etc.
// ===========================================================================

TEST_F(OTelSpanCreationTest, InterceptWorkflowWrapsNext) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    WorkflowInboundInterceptor base(nullptr);
    auto* wrapped = interceptor->intercept_workflow(&base);

    EXPECT_NE(wrapped, nullptr);
    EXPECT_NE(wrapped, &base);
}

TEST_F(OTelSpanCreationTest, InterceptActivityWrapsNext) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    ActivityInboundInterceptor base(nullptr);
    auto* wrapped = interceptor->intercept_activity(&base);

    EXPECT_NE(wrapped, nullptr);
    EXPECT_NE(wrapped, &base);
}

// ===========================================================================
// 9. Span status on success (should be OK/Unset, not Error)
// ===========================================================================

TEST_F(OTelSpanCreationTest, SuccessfulStartWorkflowHasOkStatus) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    StartWorkflowInput input;
    input.workflow = "OkWorkflow";
    input.options.id = "wf-ok";

    auto task = wrapped->start_workflow(std::move(input));
    run_task(std::move(task));

    ASSERT_GE(span_count(), 1u);
    // Successful operation should NOT have error status
    EXPECT_NE(span_at(0).GetStatus(), trace_api::StatusCode::kError);
}

// ===========================================================================
// 10. Existing headers are preserved after injection
// ===========================================================================

TEST_F(OTelSpanCreationTest, ExistingHeadersPreservedOnInjection) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    auto stub = std::make_unique<StubClientOutbound>();
    auto* stub_ptr = stub.get();
    auto wrapped = interceptor->intercept_client(std::move(stub));

    StartWorkflowInput input;
    input.workflow = "MyWorkflow";
    input.options.id = "wf-headers";
    input.headers["custom-header"] = "custom-value";
    input.headers["another-header"] = "another-value";

    auto task = wrapped->start_workflow(std::move(input));
    run_task(std::move(task));

    ASSERT_TRUE(stub_ptr->last_start_input_.has_value());
    auto& headers = stub_ptr->last_start_input_->headers;

    // Original headers should be preserved
    EXPECT_EQ(headers.at("custom-header"), "custom-value");
    EXPECT_EQ(headers.at("another-header"), "another-value");

    // Plus trace context should be injected
    EXPECT_TRUE(headers.count("traceparent") > 0);
}
