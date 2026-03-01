#include <gtest/gtest.h>

#include <any>
#include <string>
#include <vector>

#include "temporalio/activities/activity.h"
#include "temporalio/activities/activity_context.h"
#include "temporalio/coro/run_sync.h"
#include "temporalio/coro/task.h"

using namespace temporalio::activities;
using namespace temporalio::coro;

// ===========================================================================
// Sample activities for testing
// ===========================================================================
namespace {

Task<std::string> greet(std::string name) {
    co_return "Hello, " + name;
}

[[maybe_unused]] Task<int> add_one(int x) { co_return x + 1; }

Task<void> noop() { co_return; }

// Multi-argument free functions
Task<int> add_two(int a, int b) { co_return a + b; }

Task<std::string> concat_three(std::string a, std::string b, std::string c) {
    co_return a + b + c;
}

Task<void> multi_arg_void(int /*a*/, std::string /*b*/) { co_return; }

class MyService {
public:
    Task<std::string> process(std::string input) {
        co_return "Processed: " + input;
    }

    Task<int> compute() { co_return 100; }

    // Multi-argument member function
    Task<int> add(int a, int b) { co_return a + b; }

    Task<std::string> format(std::string prefix, int value) {
        co_return prefix + std::to_string(value);
    }
};

}  // namespace

// ===========================================================================
// ActivityDefinition from free function
// ===========================================================================

TEST(ActivityDefinitionTest, CreateFromFreeFunction) {
    auto def = ActivityDefinition::create("greet", &greet);
    EXPECT_EQ(def->name(), "greet");
    EXPECT_FALSE(def->is_dynamic());
}

TEST(ActivityDefinitionTest, CreateFromFreeFunctionNoArg) {
    auto def = ActivityDefinition::create("noop", &noop);
    EXPECT_EQ(def->name(), "noop");
}

// ===========================================================================
// ActivityDefinition from lambda
// ===========================================================================

TEST(ActivityDefinitionTest, CreateFromLambda) {
    auto def = ActivityDefinition::create(
        "double_it", []() -> Task<int> { co_return 42; });
    EXPECT_EQ(def->name(), "double_it");
}

// ===========================================================================
// ActivityDefinition from member function
// ===========================================================================

TEST(ActivityDefinitionTest, CreateFromMemberFunction) {
    MyService service;
    auto def =
        ActivityDefinition::create("process", &MyService::process, &service);
    EXPECT_EQ(def->name(), "process");
}

TEST(ActivityDefinitionTest, CreateFromMemberFunctionNoArg) {
    MyService service;
    auto def =
        ActivityDefinition::create("compute", &MyService::compute, &service);
    EXPECT_EQ(def->name(), "compute");
}

// ===========================================================================
// Dynamic activity
// ===========================================================================

TEST(ActivityDefinitionTest, DynamicActivity) {
    auto def = ActivityDefinition::create("", &noop);
    EXPECT_TRUE(def->is_dynamic());
    EXPECT_TRUE(def->name().empty());
}

// ===========================================================================
// ActivityInfo tests
// ===========================================================================

TEST(ActivityInfoTest, DefaultValues) {
    ActivityInfo info;
    EXPECT_TRUE(info.activity_id.empty());
    EXPECT_TRUE(info.activity_type.empty());
    EXPECT_EQ(info.attempt, 1);
    EXPECT_FALSE(info.is_local);
    EXPECT_FALSE(info.heartbeat_timeout.has_value());
    EXPECT_FALSE(info.schedule_to_close_timeout.has_value());
    EXPECT_FALSE(info.start_to_close_timeout.has_value());
    EXPECT_FALSE(info.workflow_id.has_value());
    EXPECT_FALSE(info.is_workflow_activity());
}

TEST(ActivityInfoTest, IsWorkflowActivity) {
    ActivityInfo info;
    info.workflow_id = "wf-123";
    EXPECT_TRUE(info.is_workflow_activity());
}

TEST(ActivityInfoTest, CustomValues) {
    ActivityInfo info;
    info.activity_id = "act-1";
    info.activity_type = "MyActivity";
    info.attempt = 3;
    info.namespace_ = "production";
    info.task_queue = "my-queue";
    info.is_local = true;

    EXPECT_EQ(info.activity_id, "act-1");
    EXPECT_EQ(info.activity_type, "MyActivity");
    EXPECT_EQ(info.attempt, 3);
    EXPECT_EQ(info.namespace_, "production");
    EXPECT_EQ(info.task_queue, "my-queue");
    EXPECT_TRUE(info.is_local);
}

// ===========================================================================
// Multi-argument free function activities
// ===========================================================================

TEST(ActivityDefinitionTest, CreateFromMultiArgFreeFunction) {
    auto def = ActivityDefinition::create("add_two", &add_two);
    EXPECT_EQ(def->name(), "add_two");
}

TEST(ActivityDefinitionTest, ExecuteMultiArgFreeFunction) {
    auto def = ActivityDefinition::create("add_two", &add_two);
    std::vector<std::any> args{std::any(3), std::any(4)};
    auto result = run_task_sync(def->execute(std::move(args)));
    EXPECT_EQ(std::any_cast<int>(result), 7);
}

TEST(ActivityDefinitionTest, ExecuteThreeArgFreeFunction) {
    auto def = ActivityDefinition::create("concat", &concat_three);
    std::vector<std::any> args{
        std::any(std::string("hello")),
        std::any(std::string(" ")),
        std::any(std::string("world"))};
    auto result = run_task_sync(def->execute(std::move(args)));
    EXPECT_EQ(std::any_cast<std::string>(result), "hello world");
}

TEST(ActivityDefinitionTest, ExecuteMultiArgVoidFreeFunction) {
    auto def = ActivityDefinition::create("void_multi", &multi_arg_void);
    std::vector<std::any> args{std::any(42), std::any(std::string("test"))};
    // Should not throw
    auto result = run_task_sync(def->execute(std::move(args)));
    EXPECT_FALSE(result.has_value());
}

// ===========================================================================
// Multi-argument lambda activities
// ===========================================================================

TEST(ActivityDefinitionTest, CreateFromMultiArgLambda) {
    auto def = ActivityDefinition::create(
        "multiply",
        [](int a, int b) -> Task<int> { co_return a * b; });
    EXPECT_EQ(def->name(), "multiply");
}

TEST(ActivityDefinitionTest, ExecuteMultiArgLambda) {
    auto def = ActivityDefinition::create(
        "multiply",
        [](int a, int b) -> Task<int> { co_return a * b; });
    std::vector<std::any> args{std::any(6), std::any(7)};
    auto result = run_task_sync(def->execute(std::move(args)));
    EXPECT_EQ(std::any_cast<int>(result), 42);
}

TEST(ActivityDefinitionTest, ExecuteSingleArgLambda) {
    auto def = ActivityDefinition::create(
        "negate",
        [](int x) -> Task<int> { co_return -x; });
    std::vector<std::any> args{std::any(5)};
    auto result = run_task_sync(def->execute(std::move(args)));
    EXPECT_EQ(std::any_cast<int>(result), -5);
}

TEST(ActivityDefinitionTest, ExecuteZeroArgLambda) {
    auto def = ActivityDefinition::create(
        "constant",
        []() -> Task<int> { co_return 99; });
    std::vector<std::any> args;
    auto result = run_task_sync(def->execute(std::move(args)));
    EXPECT_EQ(std::any_cast<int>(result), 99);
}

// ===========================================================================
// Multi-argument member function activities
// ===========================================================================

TEST(ActivityDefinitionTest, CreateFromMultiArgMemberFunction) {
    MyService service;
    auto def =
        ActivityDefinition::create("add", &MyService::add, &service);
    EXPECT_EQ(def->name(), "add");
}

TEST(ActivityDefinitionTest, ExecuteMultiArgMemberFunction) {
    MyService service;
    auto def =
        ActivityDefinition::create("add", &MyService::add, &service);
    std::vector<std::any> args{std::any(10), std::any(20)};
    auto result = run_task_sync(def->execute(std::move(args)));
    EXPECT_EQ(std::any_cast<int>(result), 30);
}

TEST(ActivityDefinitionTest, ExecuteMixedArgMemberFunction) {
    MyService service;
    auto def = ActivityDefinition::create(
        "format", &MyService::format, &service);
    std::vector<std::any> args{
        std::any(std::string("value=")), std::any(42)};
    auto result = run_task_sync(def->execute(std::move(args)));
    EXPECT_EQ(std::any_cast<std::string>(result), "value=42");
}

// ===========================================================================
// Single-arg execution tests (verify no regression)
// ===========================================================================

TEST(ActivityDefinitionTest, ExecuteSingleArgFreeFunction) {
    auto def = ActivityDefinition::create("greet", &greet);
    std::vector<std::any> args{std::any(std::string("World"))};
    auto result = run_task_sync(def->execute(std::move(args)));
    EXPECT_EQ(std::any_cast<std::string>(result), "Hello, World");
}

TEST(ActivityDefinitionTest, ExecuteNoArgFreeFunction) {
    auto def = ActivityDefinition::create("noop", &noop);
    std::vector<std::any> args;
    auto result = run_task_sync(def->execute(std::move(args)));
    EXPECT_FALSE(result.has_value());
}

TEST(ActivityDefinitionTest, ExecuteSingleArgMemberFunction) {
    MyService service;
    auto def =
        ActivityDefinition::create("process", &MyService::process, &service);
    std::vector<std::any> args{std::any(std::string("input"))};
    auto result = run_task_sync(def->execute(std::move(args)));
    EXPECT_EQ(std::any_cast<std::string>(result), "Processed: input");
}

TEST(ActivityDefinitionTest, ExecuteNoArgMemberFunction) {
    MyService service;
    auto def =
        ActivityDefinition::create("compute", &MyService::compute, &service);
    std::vector<std::any> args;
    auto result = run_task_sync(def->execute(std::move(args)));
    EXPECT_EQ(std::any_cast<int>(result), 100);
}
