#include <gtest/gtest.h>

#include <chrono>
#include <functional>
#include <optional>
#include <random>
#include <stdexcept>
#include <stop_token>
#include <string>

#include "temporalio/coro/task.h"
#include "temporalio/workflows/workflow.h"
#include "temporalio/workflows/workflow_info.h"

using namespace temporalio::workflows;
using namespace std::chrono_literals;

// ===========================================================================
// Mock WorkflowContext for testing the static Workflow API
// ===========================================================================
namespace {

class MockWorkflowContext : public WorkflowContext {
public:
    MockWorkflowContext() {
        info_.workflow_id = "test-wf-1";
        info_.workflow_type = "TestWorkflow";
        info_.run_id = "test-run-1";
        info_.namespace_ = "default";
        info_.task_queue = "test-queue";
        info_.attempt = 1;
    }

    const WorkflowInfo& info() const override { return info_; }

    std::stop_token cancellation_token() const override {
        return cancel_source_.get_token();
    }

    bool continue_as_new_suggested() const override {
        return continue_as_new_suggested_;
    }

    bool all_handlers_finished() const override {
        return all_handlers_finished_;
    }

    std::chrono::system_clock::time_point utc_now() const override {
        return current_time_;
    }

    std::mt19937& random() override { return random_; }

    int current_history_length() const override { return history_length_; }
    int current_history_size() const override { return history_size_; }
    bool is_replaying() const override { return is_replaying_; }

    const WorkflowUpdateInfo* current_update_info() const override {
        return update_info_ ? &*update_info_ : nullptr;
    }

    bool patched(const std::string& patch_id) override {
        patched_calls_.push_back(patch_id);
        return patched_result_;
    }

    void deprecate_patch(const std::string& patch_id) override {
        deprecated_calls_.push_back(patch_id);
    }

    temporalio::coro::Task<void> start_timer(
        std::chrono::milliseconds /*duration*/,
        std::stop_token /*ct*/) override {
        start_timer_called_ = true;
        co_return;
    }

    temporalio::coro::Task<bool> register_condition(
        std::function<bool()> condition,
        std::optional<std::chrono::milliseconds> /*timeout*/,
        std::stop_token /*ct*/) override {
        register_condition_called_ = true;
        if (condition && condition()) {
            co_return true;
        }
        co_return false;
    }

    temporalio::coro::Task<std::any> schedule_activity(
        const std::string& /*activity_type*/,
        std::vector<std::any> /*args*/,
        const ActivityOptions& /*options*/) override {
        schedule_activity_called_ = true;
        co_return std::any{};
    }

    // Test helpers
    bool schedule_activity_called_ = false;
    bool start_timer_called_ = false;
    bool register_condition_called_ = false;
    WorkflowInfo info_;
    std::stop_source cancel_source_;
    bool continue_as_new_suggested_ = false;
    bool all_handlers_finished_ = true;
    std::chrono::system_clock::time_point current_time_ =
        std::chrono::system_clock::now();
    std::mt19937 random_{42};
    int history_length_ = 10;
    int history_size_ = 1024;
    bool is_replaying_ = false;
    std::optional<WorkflowUpdateInfo> update_info_;
    bool patched_result_ = true;
    std::vector<std::string> patched_calls_;
    std::vector<std::string> deprecated_calls_;
};

}  // namespace

// ===========================================================================
// Workflow::in_workflow() tests
// ===========================================================================

TEST(WorkflowAmbientTest, InWorkflowReturnsFalseOutsideContext) {
    EXPECT_FALSE(Workflow::in_workflow());
}

TEST(WorkflowAmbientTest, InWorkflowReturnsTrueInsideContext) {
    MockWorkflowContext ctx;
    WorkflowContextScope scope(&ctx);
    EXPECT_TRUE(Workflow::in_workflow());
}

TEST(WorkflowAmbientTest, InWorkflowRestoredAfterScope) {
    {
        MockWorkflowContext ctx;
        WorkflowContextScope scope(&ctx);
        EXPECT_TRUE(Workflow::in_workflow());
    }
    EXPECT_FALSE(Workflow::in_workflow());
}

// ===========================================================================
// Workflow static methods throw outside context
// ===========================================================================

TEST(WorkflowAmbientTest, InfoThrowsOutsideContext) {
    EXPECT_THROW(Workflow::info(), std::runtime_error);
}

TEST(WorkflowAmbientTest, CancellationTokenThrowsOutsideContext) {
    EXPECT_THROW(Workflow::cancellation_token(), std::runtime_error);
}

TEST(WorkflowAmbientTest, UtcNowThrowsOutsideContext) {
    EXPECT_THROW(Workflow::utc_now(), std::runtime_error);
}

TEST(WorkflowAmbientTest, RandomThrowsOutsideContext) {
    EXPECT_THROW(Workflow::random(), std::runtime_error);
}

TEST(WorkflowAmbientTest, IsReplayingThrowsOutsideContext) {
    EXPECT_THROW(Workflow::is_replaying(), std::runtime_error);
}

TEST(WorkflowAmbientTest, CurrentHistoryLengthThrowsOutsideContext) {
    EXPECT_THROW(Workflow::current_history_length(), std::runtime_error);
}

TEST(WorkflowAmbientTest, CurrentHistorySizeThrowsOutsideContext) {
    EXPECT_THROW(Workflow::current_history_size(), std::runtime_error);
}

TEST(WorkflowAmbientTest, ContinueAsNewSuggestedThrowsOutsideContext) {
    EXPECT_THROW(Workflow::continue_as_new_suggested(), std::runtime_error);
}

TEST(WorkflowAmbientTest, AllHandlersFinishedThrowsOutsideContext) {
    EXPECT_THROW(Workflow::all_handlers_finished(), std::runtime_error);
}

TEST(WorkflowAmbientTest, PatchedThrowsOutsideContext) {
    EXPECT_THROW(Workflow::patched("test-patch"), std::runtime_error);
}

TEST(WorkflowAmbientTest, DeprecatePatchThrowsOutsideContext) {
    EXPECT_THROW(Workflow::deprecate_patch("test-patch"), std::runtime_error);
}

TEST(WorkflowAmbientTest, CurrentUpdateInfoThrowsOutsideContext) {
    EXPECT_THROW(Workflow::current_update_info(), std::runtime_error);
}

// ===========================================================================
// Workflow static methods with context
// ===========================================================================

TEST(WorkflowAmbientTest, InfoReturnsCorrectData) {
    MockWorkflowContext ctx;
    WorkflowContextScope scope(&ctx);

    const auto& info = Workflow::info();
    EXPECT_EQ(info.workflow_id, "test-wf-1");
    EXPECT_EQ(info.workflow_type, "TestWorkflow");
    EXPECT_EQ(info.run_id, "test-run-1");
    EXPECT_EQ(info.namespace_, "default");
    EXPECT_EQ(info.task_queue, "test-queue");
}

TEST(WorkflowAmbientTest, CancellationTokenNotTriggered) {
    MockWorkflowContext ctx;
    WorkflowContextScope scope(&ctx);

    auto token = Workflow::cancellation_token();
    EXPECT_FALSE(token.stop_requested());
}

TEST(WorkflowAmbientTest, CancellationTokenTriggered) {
    MockWorkflowContext ctx;
    ctx.cancel_source_.request_stop();
    WorkflowContextScope scope(&ctx);

    auto token = Workflow::cancellation_token();
    EXPECT_TRUE(token.stop_requested());
}

TEST(WorkflowAmbientTest, ContinueAsNewSuggested) {
    MockWorkflowContext ctx;
    ctx.continue_as_new_suggested_ = true;
    WorkflowContextScope scope(&ctx);

    EXPECT_TRUE(Workflow::continue_as_new_suggested());
}

TEST(WorkflowAmbientTest, AllHandlersFinished) {
    MockWorkflowContext ctx;
    ctx.all_handlers_finished_ = false;
    WorkflowContextScope scope(&ctx);

    EXPECT_FALSE(Workflow::all_handlers_finished());
}

TEST(WorkflowAmbientTest, UtcNowReturnsDeterministicTime) {
    MockWorkflowContext ctx;
    auto expected = std::chrono::system_clock::time_point{} + 1000s;
    ctx.current_time_ = expected;
    WorkflowContextScope scope(&ctx);

    EXPECT_EQ(Workflow::utc_now(), expected);
}

TEST(WorkflowAmbientTest, RandomReturnsDeterministicGenerator) {
    MockWorkflowContext ctx;
    WorkflowContextScope scope(&ctx);

    // Same seed should produce same sequence
    std::mt19937 expected{42};
    auto& rng = Workflow::random();
    EXPECT_EQ(rng(), expected());
}

TEST(WorkflowAmbientTest, CurrentHistoryLength) {
    MockWorkflowContext ctx;
    ctx.history_length_ = 42;
    WorkflowContextScope scope(&ctx);

    EXPECT_EQ(Workflow::current_history_length(), 42);
}

TEST(WorkflowAmbientTest, CurrentHistorySize) {
    MockWorkflowContext ctx;
    ctx.history_size_ = 8192;
    WorkflowContextScope scope(&ctx);

    EXPECT_EQ(Workflow::current_history_size(), 8192);
}

TEST(WorkflowAmbientTest, IsReplayingFalse) {
    MockWorkflowContext ctx;
    ctx.is_replaying_ = false;
    WorkflowContextScope scope(&ctx);

    EXPECT_FALSE(Workflow::is_replaying());
}

TEST(WorkflowAmbientTest, IsReplayingTrue) {
    MockWorkflowContext ctx;
    ctx.is_replaying_ = true;
    WorkflowContextScope scope(&ctx);

    EXPECT_TRUE(Workflow::is_replaying());
}

TEST(WorkflowAmbientTest, PatchedDelegatesToContext) {
    MockWorkflowContext ctx;
    ctx.patched_result_ = true;
    WorkflowContextScope scope(&ctx);

    EXPECT_TRUE(Workflow::patched("my-patch"));
    ASSERT_EQ(ctx.patched_calls_.size(), 1u);
    EXPECT_EQ(ctx.patched_calls_[0], "my-patch");
}

TEST(WorkflowAmbientTest, DeprecatePatchDelegatesToContext) {
    MockWorkflowContext ctx;
    WorkflowContextScope scope(&ctx);

    Workflow::deprecate_patch("old-patch");
    ASSERT_EQ(ctx.deprecated_calls_.size(), 1u);
    EXPECT_EQ(ctx.deprecated_calls_[0], "old-patch");
}

TEST(WorkflowAmbientTest, CurrentUpdateInfoNullByDefault) {
    MockWorkflowContext ctx;
    WorkflowContextScope scope(&ctx);

    EXPECT_EQ(Workflow::current_update_info(), nullptr);
}

TEST(WorkflowAmbientTest, CurrentUpdateInfoReturnsValue) {
    MockWorkflowContext ctx;
    ctx.update_info_ = WorkflowUpdateInfo{
        .id = "upd-1",
        .name = "my_update",
    };
    WorkflowContextScope scope(&ctx);

    auto* info = Workflow::current_update_info();
    ASSERT_NE(info, nullptr);
    EXPECT_EQ(info->id, "upd-1");
    EXPECT_EQ(info->name, "my_update");
}

// ===========================================================================
// WorkflowContextScope tests
// ===========================================================================

TEST(WorkflowContextScopeTest, NestedScopes) {
    MockWorkflowContext outer;
    outer.info_.workflow_id = "outer-wf";

    MockWorkflowContext inner;
    inner.info_.workflow_id = "inner-wf";

    {
        WorkflowContextScope scope1(&outer);
        EXPECT_EQ(Workflow::info().workflow_id, "outer-wf");

        {
            WorkflowContextScope scope2(&inner);
            EXPECT_EQ(Workflow::info().workflow_id, "inner-wf");
        }

        // Restored to outer
        EXPECT_EQ(Workflow::info().workflow_id, "outer-wf");
    }

    EXPECT_FALSE(Workflow::in_workflow());
}

TEST(WorkflowContextScopeTest, NullContextScope) {
    WorkflowContextScope scope(nullptr);
    EXPECT_FALSE(Workflow::in_workflow());
}

TEST(WorkflowContextScopeTest, IsNonCopyable) {
    EXPECT_FALSE(std::is_copy_constructible_v<WorkflowContextScope>);
    EXPECT_FALSE(std::is_copy_assignable_v<WorkflowContextScope>);
}

// ===========================================================================
// Workflow is not instantiable
// ===========================================================================

TEST(WorkflowTest, IsNotDefaultConstructible) {
    EXPECT_FALSE(std::is_default_constructible_v<Workflow>);
}
