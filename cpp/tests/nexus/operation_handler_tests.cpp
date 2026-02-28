#include <gtest/gtest.h>

#include <any>
#include <memory>
#include <optional>
#include <string>
#include <unordered_map>
#include <vector>

#include "temporalio/nexus/operation_handler.h"

using namespace temporalio::nexus;
using namespace temporalio::coro;

// ===========================================================================
// NexusOperationInfo tests
// ===========================================================================

TEST(NexusOperationInfoTest, DefaultValues) {
    NexusOperationInfo info;
    EXPECT_TRUE(info.ns.empty());
    EXPECT_TRUE(info.task_queue.empty());
}

TEST(NexusOperationInfoTest, CustomValues) {
    NexusOperationInfo info{
        .ns = "production",
        .task_queue = "nexus-queue",
    };
    EXPECT_EQ(info.ns, "production");
    EXPECT_EQ(info.task_queue, "nexus-queue");
}

// ===========================================================================
// NexusLink tests
// ===========================================================================

TEST(NexusLinkTest, DefaultValues) {
    NexusLink link;
    EXPECT_TRUE(link.uri.empty());
    EXPECT_TRUE(link.type.empty());
}

TEST(NexusLinkTest, CustomValues) {
    NexusLink link{
        .uri = "temporal:///namespaces/ns/workflows/wf-1/run-1/history",
        .type = "temporal.api.nexus.v1.Link",
    };
    EXPECT_FALSE(link.uri.empty());
    EXPECT_FALSE(link.type.empty());
}

// ===========================================================================
// OperationContext tests
// ===========================================================================

TEST(OperationContextTest, DefaultValues) {
    OperationContext ctx;
    EXPECT_TRUE(ctx.service.empty());
    EXPECT_TRUE(ctx.operation.empty());
    EXPECT_TRUE(ctx.headers.empty());
}

TEST(OperationContextTest, CustomValues) {
    OperationContext ctx{
        .service = "MyService",
        .operation = "MyOperation",
        .headers = {{"Authorization", "Bearer token"}},
    };
    EXPECT_EQ(ctx.service, "MyService");
    EXPECT_EQ(ctx.operation, "MyOperation");
    EXPECT_EQ(ctx.headers.at("Authorization"), "Bearer token");
}

// ===========================================================================
// OperationStartContext tests
// ===========================================================================

TEST(OperationStartContextTest, DefaultValues) {
    OperationStartContext ctx;
    EXPECT_TRUE(ctx.service.empty());
    EXPECT_TRUE(ctx.operation.empty());
    EXPECT_TRUE(ctx.headers.empty());
    EXPECT_FALSE(ctx.request_id.has_value());
    EXPECT_FALSE(ctx.callback_url.has_value());
    EXPECT_TRUE(ctx.callback_headers.empty());
    EXPECT_TRUE(ctx.inbound_links.empty());
    EXPECT_TRUE(ctx.outbound_links.empty());
}

TEST(OperationStartContextTest, CustomValues) {
    OperationStartContext ctx;
    ctx.service = "MyService";
    ctx.operation = "StartOp";
    ctx.request_id = "req-001";
    ctx.callback_url = "http://localhost:8080/callback";
    ctx.callback_headers = {{"X-Callback", "true"}};
    ctx.inbound_links = {NexusLink{.uri = "link-1", .type = "type-1"}};
    ctx.outbound_links = {NexusLink{.uri = "link-2", .type = "type-2"}};

    EXPECT_EQ(ctx.service, "MyService");
    EXPECT_EQ(ctx.request_id.value(), "req-001");
    EXPECT_EQ(ctx.callback_url.value(), "http://localhost:8080/callback");
    ASSERT_EQ(ctx.inbound_links.size(), 1u);
    EXPECT_EQ(ctx.inbound_links[0].uri, "link-1");
    ASSERT_EQ(ctx.outbound_links.size(), 1u);
    EXPECT_EQ(ctx.outbound_links[0].uri, "link-2");
}

// ===========================================================================
// OperationCancelContext tests
// ===========================================================================

TEST(OperationCancelContextTest, DefaultValues) {
    OperationCancelContext ctx;
    EXPECT_TRUE(ctx.service.empty());
    EXPECT_TRUE(ctx.operation.empty());
    EXPECT_TRUE(ctx.operation_token.empty());
}

TEST(OperationCancelContextTest, CustomValues) {
    OperationCancelContext ctx;
    ctx.service = "MyService";
    ctx.operation = "CancelOp";
    ctx.operation_token = "token-abc";

    EXPECT_EQ(ctx.service, "MyService");
    EXPECT_EQ(ctx.operation_token, "token-abc");
}

// ===========================================================================
// OperationFetchResultContext tests
// ===========================================================================

TEST(OperationFetchResultContextTest, DefaultValues) {
    OperationFetchResultContext ctx;
    EXPECT_TRUE(ctx.operation_token.empty());
}

// ===========================================================================
// OperationFetchInfoContext tests
// ===========================================================================

TEST(OperationFetchInfoContextTest, DefaultValues) {
    OperationFetchInfoContext ctx;
    EXPECT_TRUE(ctx.operation_token.empty());
}

// ===========================================================================
// OperationStartResult tests
// ===========================================================================

TEST(OperationStartResultTest, DefaultValues) {
    OperationStartResult result;
    EXPECT_FALSE(result.is_sync);
    EXPECT_FALSE(result.sync_result.has_value());
    EXPECT_TRUE(result.async_operation_token.empty());
}

TEST(OperationStartResultTest, SyncFactory) {
    auto result = OperationStartResult::sync(std::any(42));
    EXPECT_TRUE(result.is_sync);
    EXPECT_TRUE(result.sync_result.has_value());
    EXPECT_EQ(std::any_cast<int>(result.sync_result), 42);
    EXPECT_TRUE(result.async_operation_token.empty());
}

TEST(OperationStartResultTest, SyncFactoryWithString) {
    auto result =
        OperationStartResult::sync(std::any(std::string("hello")));
    EXPECT_TRUE(result.is_sync);
    EXPECT_EQ(std::any_cast<std::string>(result.sync_result), "hello");
}

TEST(OperationStartResultTest, AsyncFactory) {
    auto result = OperationStartResult::async_result("op-token-123");
    EXPECT_FALSE(result.is_sync);
    EXPECT_FALSE(result.sync_result.has_value());
    EXPECT_EQ(result.async_operation_token, "op-token-123");
}

TEST(OperationStartResultTest, AsyncFactoryEmptyToken) {
    auto result = OperationStartResult::async_result("");
    EXPECT_FALSE(result.is_sync);
    EXPECT_TRUE(result.async_operation_token.empty());
}

// ===========================================================================
// NexusOperationState tests
// ===========================================================================

TEST(NexusOperationStateTest, DefaultValues) {
    NexusOperationState state;
    EXPECT_TRUE(state.state.empty());
}

TEST(NexusOperationStateTest, CustomValues) {
    NexusOperationState state{.state = "running"};
    EXPECT_EQ(state.state, "running");
}

// ===========================================================================
// HandlerErrorType tests
// ===========================================================================

TEST(HandlerErrorTypeTest, Values) {
    EXPECT_NE(static_cast<int>(HandlerErrorType::kBadRequest),
              static_cast<int>(HandlerErrorType::kInternal));
    EXPECT_NE(static_cast<int>(HandlerErrorType::kNotFound),
              static_cast<int>(HandlerErrorType::kUnavailable));
}

// ===========================================================================
// NexusWorkflowRunHandle tests
// ===========================================================================

TEST(NexusWorkflowRunHandleTest, Construction) {
    NexusWorkflowRunHandle handle("default", "wf-123", 0);
    EXPECT_EQ(handle.ns(), "default");
    EXPECT_EQ(handle.workflow_id(), "wf-123");
    EXPECT_EQ(handle.version(), 0);
}

TEST(NexusWorkflowRunHandleTest, DefaultVersion) {
    NexusWorkflowRunHandle handle("ns", "wf-1");
    EXPECT_EQ(handle.version(), 0);
}

TEST(NexusWorkflowRunHandleTest, ToTokenRoundTrip) {
    NexusWorkflowRunHandle original("default", "wf-roundtrip", 0);
    std::string token = original.to_token();
    EXPECT_FALSE(token.empty());

    auto restored = NexusWorkflowRunHandle::from_token(token);
    EXPECT_EQ(restored.ns(), "default");
    EXPECT_EQ(restored.workflow_id(), "wf-roundtrip");
    EXPECT_EQ(restored.version(), 0);
}

TEST(NexusWorkflowRunHandleTest, FromTokenInvalidThrows) {
    EXPECT_THROW(NexusWorkflowRunHandle::from_token("not-valid-base64!!!"),
                 std::invalid_argument);
}

// ===========================================================================
// NexusServiceDefinition tests
// ===========================================================================

namespace {

class StubNexusHandler : public INexusOperationHandler {
public:
    explicit StubNexusHandler(std::string name) : name_(std::move(name)) {}

    Task<OperationStartResult> start_async(OperationStartContext context,
                                           std::any input) override {
        co_return OperationStartResult::sync(std::any(42));
    }

    Task<std::any> fetch_result_async(
        OperationFetchResultContext context) override {
        co_return std::any(std::string("result"));
    }

    Task<NexusOperationState> fetch_info_async(
        OperationFetchInfoContext context) override {
        co_return NexusOperationState{.state = "running"};
    }

    Task<void> cancel_async(OperationCancelContext context) override {
        co_return;
    }

    const std::string& name() const override { return name_; }

private:
    std::string name_;
};

}  // namespace

TEST(NexusServiceDefinitionTest, ConstructWithNameAndOperations) {
    auto op1 = std::make_shared<StubNexusHandler>("op1");
    auto op2 = std::make_shared<StubNexusHandler>("op2");

    NexusServiceDefinition svc("MyService", {op1, op2});
    EXPECT_EQ(svc.name(), "MyService");
    ASSERT_EQ(svc.operations().size(), 2u);
    EXPECT_EQ(svc.operations()[0]->name(), "op1");
    EXPECT_EQ(svc.operations()[1]->name(), "op2");
}

TEST(NexusServiceDefinitionTest, EmptyOperations) {
    NexusServiceDefinition svc("EmptyService", {});
    EXPECT_EQ(svc.name(), "EmptyService");
    EXPECT_TRUE(svc.operations().empty());
}

TEST(NexusServiceDefinitionTest, FindOperationFound) {
    auto op1 = std::make_shared<StubNexusHandler>("start");
    auto op2 = std::make_shared<StubNexusHandler>("cancel");

    NexusServiceDefinition svc("Svc", {op1, op2});
    auto* found = svc.find_operation("cancel");
    ASSERT_NE(found, nullptr);
    EXPECT_EQ(found->name(), "cancel");
}

TEST(NexusServiceDefinitionTest, FindOperationNotFound) {
    auto op1 = std::make_shared<StubNexusHandler>("start");

    NexusServiceDefinition svc("Svc", {op1});
    auto* found = svc.find_operation("nonexistent");
    EXPECT_EQ(found, nullptr);
}

TEST(NexusServiceDefinitionTest, FindOperationEmptyList) {
    NexusServiceDefinition svc("Svc", {});
    auto* found = svc.find_operation("anything");
    EXPECT_EQ(found, nullptr);
}

// ===========================================================================
// INexusOperationHandler interface tests
// ===========================================================================

TEST(INexusOperationHandlerTest, VirtualDestructorWorks) {
    std::unique_ptr<INexusOperationHandler> ptr =
        std::make_unique<StubNexusHandler>("test");
    EXPECT_NO_THROW(ptr.reset());
}

TEST(INexusOperationHandlerTest, StubHandlerReturnsName) {
    StubNexusHandler handler("process_order");
    EXPECT_EQ(handler.name(), "process_order");
}

// ===========================================================================
// WorkflowRunOperationHandler factory test
// ===========================================================================

TEST(WorkflowRunOperationHandlerTest, FromHandleFactoryCreatesHandler) {
    auto handler = WorkflowRunOperationHandler::from_handle_factory(
        "test-op",
        [](WorkflowRunOperationContext& ctx, const std::string& input)
            -> Task<NexusWorkflowRunHandle> {
            co_return NexusWorkflowRunHandle("ns", "wf-1");
        });
    ASSERT_NE(handler, nullptr);
    EXPECT_EQ(handler->name(), "test-op");
}

// ===========================================================================
// WorkflowRunOperationContext tests
// ===========================================================================

TEST(WorkflowRunOperationContextTest, ConstructFromStartContext) {
    OperationStartContext start_ctx;
    start_ctx.service = "MyService";
    start_ctx.operation = "MyOp";
    start_ctx.request_id = "req-1";

    WorkflowRunOperationContext op_ctx(std::move(start_ctx));
    EXPECT_EQ(op_ctx.handler_context().service, "MyService");
    EXPECT_EQ(op_ctx.handler_context().operation, "MyOp");
    EXPECT_EQ(op_ctx.handler_context().request_id.value(), "req-1");
}

// ===========================================================================
// NexusOperationExecutionContext tests
// ===========================================================================

TEST(NexusOperationExecutionContextTest, Construction) {
    OperationContext handler_ctx{
        .service = "Svc",
        .operation = "Op",
        .headers = {{"key", "value"}},
    };
    NexusOperationInfo info{
        .ns = "default",
        .task_queue = "q1",
    };

    NexusOperationExecutionContext ctx(
        std::move(handler_ctx), std::move(info), nullptr);

    EXPECT_EQ(ctx.info().ns, "default");
    EXPECT_EQ(ctx.info().task_queue, "q1");
    EXPECT_EQ(ctx.handler_context().service, "Svc");
    EXPECT_EQ(ctx.handler_context().operation, "Op");
    EXPECT_EQ(ctx.handler_context().headers.at("key"), "value");
}

TEST(NexusOperationExecutionContextTest, NullClientPtr) {
    OperationContext handler_ctx;
    NexusOperationInfo info;

    NexusOperationExecutionContext ctx(
        std::move(handler_ctx), std::move(info), nullptr);

    EXPECT_EQ(ctx.temporal_client_ptr(), nullptr);
}

TEST(NexusOperationExecutionContextTest, HasCurrentIsFalseByDefault) {
    // Outside any ContextScope, has_current should be false
    EXPECT_FALSE(NexusOperationExecutionContext::has_current());
}

TEST(NexusOperationExecutionContextTest, CurrentThrowsOutsideScope) {
    // Calling current() outside a ContextScope should throw
    EXPECT_THROW(NexusOperationExecutionContext::current(),
                 std::logic_error);
}

// ===========================================================================
// ContextScope tests
// ===========================================================================

TEST(ContextScopeTest, SetsAndRestoresContext) {
    OperationContext handler_ctx;
    NexusOperationInfo info{.ns = "test-ns"};

    NexusOperationExecutionContext ctx(
        std::move(handler_ctx), std::move(info), nullptr);

    EXPECT_FALSE(NexusOperationExecutionContext::has_current());

    {
        ContextScope scope(&ctx);
        EXPECT_TRUE(NexusOperationExecutionContext::has_current());
        EXPECT_EQ(&NexusOperationExecutionContext::current(), &ctx);
        EXPECT_EQ(NexusOperationExecutionContext::current().info().ns,
                  "test-ns");
    }

    // After scope ends, context should be restored to none
    EXPECT_FALSE(NexusOperationExecutionContext::has_current());
}

TEST(ContextScopeTest, NestedScopes) {
    OperationContext handler_ctx1;
    NexusOperationInfo info1{.ns = "outer"};
    NexusOperationExecutionContext ctx1(
        std::move(handler_ctx1), std::move(info1), nullptr);

    OperationContext handler_ctx2;
    NexusOperationInfo info2{.ns = "inner"};
    NexusOperationExecutionContext ctx2(
        std::move(handler_ctx2), std::move(info2), nullptr);

    {
        ContextScope outer(&ctx1);
        EXPECT_EQ(NexusOperationExecutionContext::current().info().ns,
                  "outer");

        {
            ContextScope inner(&ctx2);
            EXPECT_EQ(NexusOperationExecutionContext::current().info().ns,
                      "inner");
        }

        // After inner scope ends, outer context should be restored
        EXPECT_EQ(NexusOperationExecutionContext::current().info().ns,
                  "outer");
    }

    EXPECT_FALSE(NexusOperationExecutionContext::has_current());
}

TEST(ContextScopeTest, NullScope) {
    ContextScope scope(nullptr);
    // Should not crash; current should be null
    EXPECT_FALSE(NexusOperationExecutionContext::has_current());
}

TEST(ContextScopeTest, IsNonCopyable) {
    EXPECT_FALSE(std::is_copy_constructible_v<ContextScope>);
    EXPECT_FALSE(std::is_copy_assignable_v<ContextScope>);
}
