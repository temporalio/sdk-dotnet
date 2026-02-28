#include <gtest/gtest.h>

#include <any>
#include <functional>
#include <memory>
#include <stop_token>
#include <string>
#include <vector>

#include "temporalio/activities/activity_context.h"
#include "temporalio/testing/activity_environment.h"

using namespace temporalio::testing;
using namespace temporalio::activities;

// ===========================================================================
// ActivityEnvironment construction tests
// ===========================================================================

TEST(ActivityEnvironmentTest, DefaultConstruction) {
    ActivityEnvironment env;
    EXPECT_FALSE(env.is_cancelled());
    EXPECT_TRUE(env.info().activity_id.empty());
}

TEST(ActivityEnvironmentTest, IsNonCopyable) {
    EXPECT_FALSE(std::is_copy_constructible_v<ActivityEnvironment>);
    EXPECT_FALSE(std::is_copy_assignable_v<ActivityEnvironment>);
}

// ===========================================================================
// ActivityEnvironment info tests
// ===========================================================================

TEST(ActivityEnvironmentTest, SetInfo) {
    ActivityEnvironment env;
    ActivityInfo info;
    info.activity_type = "SomeActivity";
    info.activity_id = "act-1";
    info.attempt = 2;

    env.set_info(std::move(info));
    EXPECT_EQ(env.info().activity_type, "SomeActivity");
    EXPECT_EQ(env.info().activity_id, "act-1");
    EXPECT_EQ(env.info().attempt, 2);
}

TEST(ActivityEnvironmentTest, DefaultActivityInfo) {
    auto info = default_activity_info();
    EXPECT_EQ(info.activity_id, "test-activity-id");
    EXPECT_EQ(info.activity_type, "TestActivity");
    EXPECT_EQ(info.attempt, 1);
    EXPECT_EQ(info.namespace_, "default");
    EXPECT_EQ(info.task_queue, "test-task-queue");
    EXPECT_TRUE(info.workflow_id.has_value());
    EXPECT_EQ(info.workflow_id.value(), "test-workflow-id");
    EXPECT_TRUE(info.is_workflow_activity());
}

// ===========================================================================
// ActivityEnvironment cancellation tests
// ===========================================================================

TEST(ActivityEnvironmentTest, CancelSetsFlag) {
    ActivityEnvironment env;
    EXPECT_FALSE(env.is_cancelled());
    env.cancel();
    EXPECT_TRUE(env.is_cancelled());
}

TEST(ActivityEnvironmentTest, MultipleCancelsAreIdempotent) {
    ActivityEnvironment env;
    env.cancel();
    env.cancel();
    EXPECT_TRUE(env.is_cancelled());
}

// ===========================================================================
// ActivityEnvironment heartbeat tests
// ===========================================================================

TEST(ActivityEnvironmentTest, SetHeartbeater) {
    ActivityEnvironment env;
    std::vector<std::vector<std::any>> heartbeats;

    env.set_heartbeater([&heartbeats](std::vector<std::any> details) {
        heartbeats.push_back(std::move(details));
    });

    // Heartbeat is not directly callable on env, but the callback is set.
    // This just verifies the setter works without crashing.
    EXPECT_TRUE(heartbeats.empty());
}

// ===========================================================================
// ActivityEnvironment run tests
// ===========================================================================

TEST(ActivityEnvironmentTest, RunSyncFunction) {
    ActivityEnvironment env;
    auto result = env.run([]() { return 42; });
    EXPECT_EQ(result, 42);
}

TEST(ActivityEnvironmentTest, RunStringReturning) {
    ActivityEnvironment env;
    auto result = env.run([]() -> std::string { return "hello"; });
    EXPECT_EQ(result, "hello");
}

TEST(ActivityEnvironmentTest, RunVoidFunction) {
    ActivityEnvironment env;
    bool called = false;
    env.run([&called]() { called = true; });
    EXPECT_TRUE(called);
}

TEST(ActivityEnvironmentTest, RunWithCapture) {
    ActivityEnvironment env;
    int x = 10;
    auto result = env.run([&x]() { return x * 2; });
    EXPECT_EQ(result, 20);
}

TEST(ActivityEnvironmentTest, RunThrowingFunction) {
    ActivityEnvironment env;
    EXPECT_THROW(
        env.run([]() -> int { throw std::runtime_error("activity failed"); }),
        std::runtime_error);
}

// ===========================================================================
// ActivityEnvironment context wiring tests
// ===========================================================================

TEST(ActivityEnvironmentTest, RunEstablishesActivityContext) {
    ActivityEnvironment env;
    env.set_info(default_activity_info());

    bool has_context = false;
    env.run([&has_context]() {
        has_context = ActivityExecutionContext::has_current();
    });
    EXPECT_TRUE(has_context);
}

TEST(ActivityEnvironmentTest, RunContextHasCorrectInfo) {
    ActivityEnvironment env;
    auto info = default_activity_info();
    info.activity_type = "MyTestActivity";
    info.activity_id = "act-42";
    info.attempt = 3;
    env.set_info(std::move(info));

    std::string seen_type;
    std::string seen_id;
    int seen_attempt = 0;
    env.run([&]() {
        auto& ctx = ActivityExecutionContext::current();
        seen_type = ctx.info().activity_type;
        seen_id = ctx.info().activity_id;
        seen_attempt = ctx.info().attempt;
    });
    EXPECT_EQ(seen_type, "MyTestActivity");
    EXPECT_EQ(seen_id, "act-42");
    EXPECT_EQ(seen_attempt, 3);
}

TEST(ActivityEnvironmentTest, RunContextClearedAfterReturn) {
    ActivityEnvironment env;
    env.run([]() {
        EXPECT_TRUE(ActivityExecutionContext::has_current());
    });
    EXPECT_FALSE(ActivityExecutionContext::has_current());
}

TEST(ActivityEnvironmentTest, RunContextClearedAfterException) {
    ActivityEnvironment env;
    try {
        env.run([]() -> int {
            EXPECT_TRUE(ActivityExecutionContext::has_current());
            throw std::runtime_error("test error");
        });
    } catch (...) {
        // expected
    }
    EXPECT_FALSE(ActivityExecutionContext::has_current());
}

TEST(ActivityEnvironmentTest, CancelTriggersContextToken) {
    ActivityEnvironment env;

    bool was_cancelled = false;
    env.cancel();
    env.run([&was_cancelled]() {
        was_cancelled =
            ActivityExecutionContext::current().cancellation_token()
                .stop_requested();
    });
    EXPECT_TRUE(was_cancelled);
}

TEST(ActivityEnvironmentTest, CancelNotRequestedByDefault) {
    ActivityEnvironment env;

    bool was_cancelled = true;
    env.run([&was_cancelled]() {
        was_cancelled =
            ActivityExecutionContext::current().cancellation_token()
                .stop_requested();
    });
    EXPECT_FALSE(was_cancelled);
}

TEST(ActivityEnvironmentTest, WorkerShutdownToken) {
    ActivityEnvironment env;
    env.worker_shutdown_source().request_stop();

    bool was_shutdown = false;
    env.run([&was_shutdown]() {
        was_shutdown =
            ActivityExecutionContext::current().worker_shutdown_token()
                .stop_requested();
    });
    EXPECT_TRUE(was_shutdown);
}

TEST(ActivityEnvironmentTest, HeartbeatFromContext) {
    ActivityEnvironment env;
    std::vector<std::vector<std::any>> heartbeats;
    env.set_heartbeater([&heartbeats](std::vector<std::any> details) {
        heartbeats.push_back(std::move(details));
    });

    env.run([&]() {
        ActivityExecutionContext::current().heartbeat(
            std::any(std::string("progress-1")));
        ActivityExecutionContext::current().heartbeat(
            std::any(std::string("progress-2")));
    });

    EXPECT_EQ(heartbeats.size(), 2u);
    EXPECT_EQ(std::any_cast<std::string>(heartbeats[0][0]), "progress-1");
    EXPECT_EQ(std::any_cast<std::string>(heartbeats[1][0]), "progress-2");
}

TEST(ActivityEnvironmentTest, HeartbeatEmptyDetails) {
    ActivityEnvironment env;
    std::vector<std::vector<std::any>> heartbeats;
    env.set_heartbeater([&heartbeats](std::vector<std::any> details) {
        heartbeats.push_back(std::move(details));
    });

    env.run([&]() {
        ActivityExecutionContext::current().heartbeat();
    });

    EXPECT_EQ(heartbeats.size(), 1u);
    EXPECT_TRUE(heartbeats[0].empty());
}

TEST(ActivityEnvironmentTest, RunWithReturnValueAndContext) {
    ActivityEnvironment env;
    env.set_info(default_activity_info());

    auto result = env.run([]() -> std::string {
        return ActivityExecutionContext::current().info().activity_type;
    });
    EXPECT_EQ(result, "TestActivity");
}

// ===========================================================================
// ActivityEnvironmentInfo tests
// ===========================================================================

TEST(ActivityEnvironmentInfoTest, DefaultValues) {
    ActivityEnvironmentInfo env_info;
    EXPECT_TRUE(env_info.info.activity_id.empty());
    EXPECT_TRUE(env_info.info.activity_type.empty());
}

TEST(ActivityEnvironmentInfoTest, CustomValues) {
    ActivityEnvironmentInfo env_info;
    env_info.info.activity_type = "CustomActivity";
    env_info.info.namespace_ = "test-ns";
    EXPECT_EQ(env_info.info.activity_type, "CustomActivity");
    EXPECT_EQ(env_info.info.namespace_, "test-ns");
}
