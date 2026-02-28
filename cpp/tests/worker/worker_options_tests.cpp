#include <gtest/gtest.h>

#include <memory>
#include <string>
#include <vector>

#include "temporalio/activities/activity.h"
#include "temporalio/coro/task.h"
#include "temporalio/worker/temporal_worker.h"
#include "temporalio/workflows/workflow_definition.h"

using namespace temporalio::worker;
using namespace temporalio::workflows;
using namespace temporalio::activities;
using namespace temporalio::coro;

// ===========================================================================
// Sample types for testing
// ===========================================================================
namespace {

class TestWorkflow {
public:
    Task<std::string> run() { co_return "result"; }
};

Task<void> test_activity() { co_return; }

}  // namespace

// ===========================================================================
// TemporalWorkerOptions default values
// ===========================================================================

TEST(TemporalWorkerOptionsTest, DefaultValues) {
    TemporalWorkerOptions opts;
    EXPECT_TRUE(opts.task_queue.empty());
    EXPECT_TRUE(opts.workflows.empty());
    EXPECT_TRUE(opts.activities.empty());
    EXPECT_TRUE(opts.interceptors.empty());
    EXPECT_EQ(opts.max_concurrent_workflow_tasks, 100u);
    EXPECT_EQ(opts.max_concurrent_activities, 100u);
    EXPECT_EQ(opts.max_concurrent_local_activities, 100u);
    EXPECT_FALSE(opts.disable_eager_activity_dispatch);
    EXPECT_TRUE(opts.build_id.empty());
    EXPECT_TRUE(opts.identity.empty());
}

// ===========================================================================
// TemporalWorkerOptions custom values
// ===========================================================================

TEST(TemporalWorkerOptionsTest, CustomValues) {
    TemporalWorkerOptions opts{
        .task_queue = "my-queue",
        .max_concurrent_workflow_tasks = 50,
        .max_concurrent_activities = 25,
        .max_concurrent_local_activities = 10,
        .disable_eager_activity_dispatch = true,
        .build_id = "v1.0.0",
        .identity = "worker-1",
    };
    EXPECT_EQ(opts.task_queue, "my-queue");
    EXPECT_EQ(opts.max_concurrent_workflow_tasks, 50u);
    EXPECT_EQ(opts.max_concurrent_activities, 25u);
    EXPECT_EQ(opts.max_concurrent_local_activities, 10u);
    EXPECT_TRUE(opts.disable_eager_activity_dispatch);
    EXPECT_EQ(opts.build_id, "v1.0.0");
    EXPECT_EQ(opts.identity, "worker-1");
}

// ===========================================================================
// TemporalWorkerOptions add_activity helper
// ===========================================================================

TEST(TemporalWorkerOptionsTest, AddActivity) {
    TemporalWorkerOptions opts;
    auto act_def = ActivityDefinition::create("test_activity", &test_activity);

    opts.add_activity(act_def);
    ASSERT_EQ(opts.activities.size(), 1u);
    EXPECT_EQ(opts.activities[0]->name(), "test_activity");
}

TEST(TemporalWorkerOptionsTest, AddMultipleActivities) {
    TemporalWorkerOptions opts;
    auto act1 = ActivityDefinition::create("act1", &test_activity);
    auto act2 = ActivityDefinition::create("act2", &test_activity);

    opts.add_activity(act1);
    opts.add_activity(act2);
    ASSERT_EQ(opts.activities.size(), 2u);
    EXPECT_EQ(opts.activities[0]->name(), "act1");
    EXPECT_EQ(opts.activities[1]->name(), "act2");
}

// ===========================================================================
// TemporalWorkerOptions workflow registration
// ===========================================================================

TEST(TemporalWorkerOptionsTest, AddWorkflowDefinition) {
    TemporalWorkerOptions opts;
    auto wf_def = WorkflowDefinition::create<TestWorkflow>("TestWorkflow")
                      .run(&TestWorkflow::run)
                      .build();

    opts.workflows.push_back(wf_def);
    ASSERT_EQ(opts.workflows.size(), 1u);
    EXPECT_EQ(opts.workflows[0]->name(), "TestWorkflow");
}

// ===========================================================================
// TemporalWorker type traits
// ===========================================================================

TEST(TemporalWorkerTest, IsNonCopyable) {
    EXPECT_FALSE(std::is_copy_constructible_v<TemporalWorker>);
    EXPECT_FALSE(std::is_copy_assignable_v<TemporalWorker>);
}
