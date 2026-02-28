#include <gtest/gtest.h>

#include <exception>
#include <memory>
#include <stdexcept>
#include <string>
#include <vector>

#include "temporalio/coro/run_sync.h"
#include "temporalio/coro/task.h"
#include "temporalio/common/workflow_history.h"
#include "temporalio/worker/workflow_replayer.h"
#include "temporalio/workflows/workflow_definition.h"

using namespace temporalio::worker;
using namespace temporalio::workflows;
using namespace temporalio::coro;
using namespace temporalio::common;

// ===========================================================================
// Sample workflow for testing
// ===========================================================================
namespace {

class SayHelloWorkflow {
public:
    Task<std::string> run(std::string name) {
        co_return "Hello, " + name + "!";
    }
};

}  // namespace

// ===========================================================================
// WorkflowReplayerOptions tests
// ===========================================================================

TEST(WorkflowReplayerOptionsTest, DefaultValues) {
    WorkflowReplayerOptions opts;
    EXPECT_TRUE(opts.workflows.empty());
    EXPECT_EQ(opts.ns, "ReplayNamespace");
    EXPECT_EQ(opts.task_queue, "ReplayTaskQueue");
    EXPECT_EQ(opts.data_converter, nullptr);
    EXPECT_TRUE(opts.interceptors.empty());
    EXPECT_FALSE(opts.debug_mode);
    EXPECT_EQ(opts.runtime, nullptr);
}

TEST(WorkflowReplayerOptionsTest, CustomNamespace) {
    WorkflowReplayerOptions opts{.ns = "MyReplayNS"};
    EXPECT_EQ(opts.ns, "MyReplayNS");
}

TEST(WorkflowReplayerOptionsTest, CustomTaskQueue) {
    WorkflowReplayerOptions opts{.task_queue = "MyReplayQueue"};
    EXPECT_EQ(opts.task_queue, "MyReplayQueue");
}

TEST(WorkflowReplayerOptionsTest, DebugMode) {
    WorkflowReplayerOptions opts{.debug_mode = true};
    EXPECT_EQ(opts.debug_mode, true);
}

TEST(WorkflowReplayerOptionsTest, WithWorkflow) {
    auto def = WorkflowDefinition::create<SayHelloWorkflow>("SayHelloWorkflow")
                   .run(&SayHelloWorkflow::run)
                   .build();

    WorkflowReplayerOptions opts;
    opts.workflows.push_back(def);

    EXPECT_EQ(opts.workflows.size(), 1u);
    EXPECT_EQ(opts.workflows[0]->name(), "SayHelloWorkflow");
}

// ===========================================================================
// WorkflowReplayResult tests
// ===========================================================================

TEST(WorkflowReplayResultTest, NoFailure) {
    WorkflowReplayResult result;
    result.replay_failure = nullptr;
    EXPECT_FALSE(result.has_failure());
}

TEST(WorkflowReplayResultTest, WithFailure) {
    WorkflowReplayResult result;
    result.replay_failure =
        std::make_exception_ptr(std::runtime_error("nondeterminism"));
    EXPECT_TRUE(result.has_failure());

    try {
        std::rethrow_exception(result.replay_failure);
    } catch (const std::runtime_error& e) {
        EXPECT_EQ(std::string(e.what()), "nondeterminism");
    }
}

TEST(WorkflowReplayResultTest, WorkflowIdPreserved) {
    WorkflowReplayResult result;
    result.workflow_id = "wf-123";
    EXPECT_EQ(result.workflow_id, "wf-123");
}

TEST(WorkflowReplayResultTest, DefaultEmpty) {
    WorkflowReplayResult result;
    EXPECT_TRUE(result.workflow_id.empty());
    EXPECT_FALSE(result.has_failure());
}

// ===========================================================================
// WorkflowHistory (common namespace) integration tests
// ===========================================================================

TEST(WorkflowHistoryReplayTest, ConstructWithIdAndBinaryHistory) {
    WorkflowHistory history("wf-abc", "binary-proto-bytes");
    EXPECT_EQ(history.id(), "wf-abc");
    EXPECT_EQ(history.serialized_history(), "binary-proto-bytes");
}

TEST(WorkflowHistoryReplayTest, FromJsonForReplay) {
    // Verify that from_json produces a WorkflowHistory suitable for replay
    auto history =
        WorkflowHistory::from_json("wf-replay", "{\"events\": []}");
    EXPECT_EQ(history.id(), "wf-replay");
    // Binary protobuf for empty History is empty in proto3
}

// ===========================================================================
// WorkflowReplayer type traits
// ===========================================================================

TEST(WorkflowReplayerTest, IsNonCopyable) {
    EXPECT_FALSE(std::is_copy_constructible_v<WorkflowReplayer>);
    EXPECT_FALSE(std::is_copy_assignable_v<WorkflowReplayer>);
}

TEST(WorkflowReplayerTest, IsNonMovable) {
    EXPECT_FALSE(std::is_move_constructible_v<WorkflowReplayer>);
    EXPECT_FALSE(std::is_move_assignable_v<WorkflowReplayer>);
}

// ===========================================================================
// WorkflowReplayer construction tests
// ===========================================================================

TEST(WorkflowReplayerTest, ConstructWithWorkflow) {
    auto def = WorkflowDefinition::create<SayHelloWorkflow>("SayHelloWorkflow")
                   .run(&SayHelloWorkflow::run)
                   .build();

    WorkflowReplayerOptions opts;
    opts.workflows.push_back(def);

    WorkflowReplayer replayer(std::move(opts));
    EXPECT_EQ(replayer.options().workflows.size(), 1u);
    EXPECT_EQ(replayer.options().ns, "ReplayNamespace");
    EXPECT_EQ(replayer.options().task_queue, "ReplayTaskQueue");
}

TEST(WorkflowReplayerTest, ConstructWithMultipleWorkflows) {
    auto def1 =
        WorkflowDefinition::create<SayHelloWorkflow>("Workflow1")
            .run(&SayHelloWorkflow::run)
            .build();
    auto def2 =
        WorkflowDefinition::create<SayHelloWorkflow>("Workflow2")
            .run(&SayHelloWorkflow::run)
            .build();

    WorkflowReplayerOptions opts;
    opts.workflows.push_back(def1);
    opts.workflows.push_back(def2);

    WorkflowReplayer replayer(std::move(opts));
    EXPECT_EQ(replayer.options().workflows.size(), 2u);
}

TEST(WorkflowReplayerTest, ConstructWithEmptyWorkflowsThrows) {
    WorkflowReplayerOptions opts;
    EXPECT_THROW(WorkflowReplayer replayer(std::move(opts)),
                 std::invalid_argument);
}

TEST(WorkflowReplayerTest, ConstructPreservesOptions) {
    auto def = WorkflowDefinition::create<SayHelloWorkflow>("SayHelloWorkflow")
                   .run(&SayHelloWorkflow::run)
                   .build();

    WorkflowReplayerOptions opts;
    opts.workflows.push_back(def);
    opts.ns = "custom-ns";
    opts.task_queue = "custom-queue";
    opts.debug_mode = true;

    WorkflowReplayer replayer(std::move(opts));
    EXPECT_EQ(replayer.options().ns, "custom-ns");
    EXPECT_EQ(replayer.options().task_queue, "custom-queue");
    EXPECT_TRUE(replayer.options().debug_mode);
}

// ===========================================================================
// WorkflowReplayer replay tests (no runtime - structural only)
// ===========================================================================

TEST(WorkflowReplayerTest, ReplayWithoutRuntimeSucceeds) {
    auto def = WorkflowDefinition::create<SayHelloWorkflow>("SayHelloWorkflow")
                   .run(&SayHelloWorkflow::run)
                   .build();

    WorkflowReplayerOptions opts;
    opts.workflows.push_back(def);

    WorkflowReplayer replayer(std::move(opts));

    // Without a runtime, replay should succeed without errors
    // (no bridge to detect nondeterminism)
    WorkflowHistory history("wf-test", "");
    auto result = run_task_sync(replayer.replay_workflow(history));
    EXPECT_EQ(result.workflow_id, "wf-test");
    EXPECT_FALSE(result.has_failure());
}

TEST(WorkflowReplayerTest, ReplayMultipleWithoutRuntime) {
    auto def = WorkflowDefinition::create<SayHelloWorkflow>("SayHelloWorkflow")
                   .run(&SayHelloWorkflow::run)
                   .build();

    WorkflowReplayerOptions opts;
    opts.workflows.push_back(def);

    WorkflowReplayer replayer(std::move(opts));

    std::vector<WorkflowHistory> histories;
    histories.emplace_back("wf-1", "");
    histories.emplace_back("wf-2", "");
    histories.emplace_back("wf-3", "");

    auto results = run_task_sync(replayer.replay_workflows(histories));
    EXPECT_EQ(results.size(), 3u);
    EXPECT_EQ(results[0].workflow_id, "wf-1");
    EXPECT_EQ(results[1].workflow_id, "wf-2");
    EXPECT_EQ(results[2].workflow_id, "wf-3");
    for (const auto& r : results) {
        EXPECT_FALSE(r.has_failure());
    }
}
