#include <gtest/gtest.h>

#include <memory>
#include <string>
#include <unordered_map>

#include "temporalio/extensions/opentelemetry/tracing_interceptor.h"
#include "temporalio/extensions/opentelemetry/tracing_options.h"
#include "temporalio/client/interceptors/client_interceptor.h"
#include "temporalio/worker/interceptors/worker_interceptor.h"

using namespace temporalio::extensions::opentelemetry;
using namespace temporalio::worker::interceptors;
using namespace temporalio::client::interceptors;

// ===========================================================================
// TracingInterceptor construction tests
// ===========================================================================

TEST(TracingInterceptorTest, DefaultConstruction) {
    TracingInterceptor interceptor;
    EXPECT_EQ(interceptor.options().header_key, "_tracer-data");
    EXPECT_TRUE(interceptor.options().tag_name_workflow_id.has_value());
    EXPECT_TRUE(interceptor.options().tag_name_run_id.has_value());
    EXPECT_TRUE(interceptor.options().tag_name_activity_id.has_value());
    EXPECT_TRUE(interceptor.options().tag_name_update_id.has_value());
}

TEST(TracingInterceptorTest, ConstructionWithOptions) {
    TracingInterceptorOptions opts{
        .header_key = "custom-key",
        .tag_name_workflow_id = "wf",
        .tag_name_run_id = std::nullopt,
    };
    TracingInterceptor interceptor(std::move(opts));

    EXPECT_EQ(interceptor.options().header_key, "custom-key");
    EXPECT_TRUE(interceptor.options().tag_name_workflow_id.has_value());
    EXPECT_EQ(interceptor.options().tag_name_workflow_id.value(), "wf");
    EXPECT_FALSE(interceptor.options().tag_name_run_id.has_value());
}

TEST(TracingInterceptorTest, SharedPtrConstruction) {
    auto interceptor = std::make_shared<TracingInterceptor>();
    EXPECT_NE(interceptor, nullptr);
    EXPECT_EQ(interceptor->options().header_key, "_tracer-data");
}

// ===========================================================================
// Source name constants
// ===========================================================================

TEST(TracingInterceptorTest, SourceNameConstants) {
    EXPECT_STREQ(TracingInterceptor::kClientSourceName,
                 "Temporalio.Extensions.OpenTelemetry.Client");
    EXPECT_STREQ(TracingInterceptor::kWorkflowSourceName,
                 "Temporalio.Extensions.OpenTelemetry.Workflow");
    EXPECT_STREQ(TracingInterceptor::kActivitySourceName,
                 "Temporalio.Extensions.OpenTelemetry.Activity");
    EXPECT_STREQ(TracingInterceptor::kNexusSourceName,
                 "Temporalio.Extensions.OpenTelemetry.Nexus");
}

// ===========================================================================
// inject_context / extract_context tests
// ===========================================================================

TEST(TracingInterceptorTest, InjectContextReturnsHeaders) {
    TracingInterceptor interceptor;
    std::unordered_map<std::string, std::string> headers;
    headers["existing-key"] = "existing-value";

    auto result = interceptor.inject_context(headers);

    // Should at minimum preserve existing headers
    EXPECT_EQ(result.at("existing-key"), "existing-value");
}

TEST(TracingInterceptorTest, InjectContextEmptyHeaders) {
    TracingInterceptor interceptor;
    std::unordered_map<std::string, std::string> headers;

    auto result = interceptor.inject_context(headers);
    // Should not crash with empty headers
}

TEST(TracingInterceptorTest, ExtractContextReturnsContext) {
    TracingInterceptor interceptor;
    std::unordered_map<std::string, std::string> headers;

    // extract_context should return a context even with empty headers
    auto ctx = interceptor.extract_context(headers);
    // No crash; returns a valid context
    (void)ctx;
}

TEST(TracingInterceptorTest, HasContextMissingHeaderReturnsFalse) {
    TracingInterceptor interceptor;
    std::unordered_map<std::string, std::string> headers;

    // No trace header present
    EXPECT_FALSE(interceptor.has_context(headers));
}

TEST(TracingInterceptorTest, HasContextPresentHeaderReturnsTrue) {
    TracingInterceptor interceptor;
    std::unordered_map<std::string, std::string> headers;
    headers["_tracer-data"] = "some-trace-context";

    EXPECT_TRUE(interceptor.has_context(headers));
}

TEST(TracingInterceptorTest, HasContextUsesCustomHeaderKey) {
    TracingInterceptorOptions opts{.header_key = "my-trace-key"};
    TracingInterceptor interceptor(std::move(opts));

    std::unordered_map<std::string, std::string> headers;
    headers["_tracer-data"] = "wrong-key";

    // Should look for "my-trace-key", not "_tracer-data"
    EXPECT_FALSE(interceptor.has_context(headers));

    headers["my-trace-key"] = "trace-data";
    EXPECT_TRUE(interceptor.has_context(headers));
}

// ===========================================================================
// intercept_workflow tests
// ===========================================================================

TEST(TracingInterceptorTest, InterceptWorkflowReturnsNonNull) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    // Create a stub WorkflowInboundInterceptor as "next" in the chain.
    // The default-constructed interceptor has next=nullptr, which is
    // fine for testing that the wrapping works.
    WorkflowInboundInterceptor base_inbound(nullptr);

    auto* result = interceptor->intercept_workflow(&base_inbound);
    EXPECT_NE(result, nullptr);
    // The result should be different from the base (it's a wrapper)
    EXPECT_NE(result, &base_inbound);
}

TEST(TracingInterceptorTest, InterceptWorkflowMultipleCalls) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    WorkflowInboundInterceptor base1(nullptr);
    WorkflowInboundInterceptor base2(nullptr);

    auto* wrapped1 = interceptor->intercept_workflow(&base1);
    auto* wrapped2 = interceptor->intercept_workflow(&base2);

    EXPECT_NE(wrapped1, nullptr);
    EXPECT_NE(wrapped2, nullptr);
    // Each call should produce a different interceptor
    EXPECT_NE(wrapped1, wrapped2);
}

// ===========================================================================
// intercept_activity tests
// ===========================================================================

TEST(TracingInterceptorTest, InterceptActivityReturnsNonNull) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    ActivityInboundInterceptor base_inbound(nullptr);

    auto* result = interceptor->intercept_activity(&base_inbound);
    EXPECT_NE(result, nullptr);
    EXPECT_NE(result, &base_inbound);
}

TEST(TracingInterceptorTest, InterceptActivityMultipleCalls) {
    auto interceptor = std::make_shared<TracingInterceptor>();

    ActivityInboundInterceptor base1(nullptr);
    ActivityInboundInterceptor base2(nullptr);

    auto* wrapped1 = interceptor->intercept_activity(&base1);
    auto* wrapped2 = interceptor->intercept_activity(&base2);

    EXPECT_NE(wrapped1, nullptr);
    EXPECT_NE(wrapped2, nullptr);
    EXPECT_NE(wrapped1, wrapped2);
}

// ===========================================================================
// IWorkerInterceptor interface conformance
// ===========================================================================

TEST(TracingInterceptorTest, IsIWorkerInterceptor) {
    auto interceptor = std::make_shared<TracingInterceptor>();
    // Should be assignable to IWorkerInterceptor pointer
    IWorkerInterceptor* base = interceptor.get();
    EXPECT_NE(base, nullptr);
}

TEST(TracingInterceptorTest, IsIClientInterceptor) {
    auto interceptor = std::make_shared<TracingInterceptor>();
    // Should be assignable to IClientInterceptor pointer
    IClientInterceptor* base = interceptor.get();
    EXPECT_NE(base, nullptr);
}

TEST(TracingInterceptorTest, VirtualDestructor) {
    // Should be safely deletable through base pointer
    std::unique_ptr<IWorkerInterceptor> ptr =
        std::make_unique<TracingInterceptor>();
    EXPECT_NO_THROW(ptr.reset());
}

// ===========================================================================
// Tracer accessor tests
// ===========================================================================

TEST(TracingInterceptorTest, TracersAreNonNull) {
    TracingInterceptor interceptor;
    EXPECT_NE(interceptor.client_tracer(), nullptr);
    EXPECT_NE(interceptor.workflow_tracer(), nullptr);
    EXPECT_NE(interceptor.activity_tracer(), nullptr);
}

// ===========================================================================
// enable_shared_from_this tests
// ===========================================================================

TEST(TracingInterceptorTest, SharedFromThis) {
    auto interceptor = std::make_shared<TracingInterceptor>();
    auto shared = interceptor->shared_from_this();
    EXPECT_EQ(shared.get(), interceptor.get());
    EXPECT_EQ(shared.use_count(), interceptor.use_count());
}

// ===========================================================================
// Options immutability after construction
// ===========================================================================

TEST(TracingInterceptorTest, OptionsAreConstRef) {
    TracingInterceptorOptions opts{.header_key = "test-key"};
    TracingInterceptor interceptor(opts);

    const auto& ref = interceptor.options();
    EXPECT_EQ(ref.header_key, "test-key");
    // Verify it's the same reference on repeated calls
    EXPECT_EQ(&interceptor.options(), &interceptor.options());
}
