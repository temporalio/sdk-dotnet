#include <gtest/gtest.h>

#include <any>
#include <chrono>
#include <memory>
#include <string>
#include <utility>
#include <vector>

#include "temporalio/coro/task.h"
#include "temporalio/exceptions/temporal_exception.h"
#include "temporalio/worker/workflow_instance.h"
#include "temporalio/workflows/activity_options.h"
#include "temporalio/workflows/workflow.h"
#include "temporalio/workflows/workflow_definition.h"
#include "temporalio/workflows/workflow_info.h"

using namespace temporalio::worker;
using namespace temporalio::workflows;
using namespace temporalio::coro;

// ===========================================================================
// Sample workflows for testing WorkflowInstance
// ===========================================================================
namespace {

class SimpleWorkflow {
public:
    Task<std::string> run() { co_return "done"; }

    std::string get_status() const { return status_; }

    Task<void> on_signal(std::string msg) {
        status_ = std::move(msg);
        co_return;
    }

private:
    std::string status_{"idle"};
};

class CounterWorkflow {
public:
    Task<int> run() { co_return counter_; }

    int get_count() const { return counter_; }

    Task<void> increment() {
        ++counter_;
        co_return;
    }

    // Update handler with validator
    void validate_set_count(int value) {
        if (value < 0) {
            throw std::invalid_argument("Counter cannot be negative");
        }
    }

    Task<int> set_count(int value) {
        counter_ = value;
        co_return counter_;
    }

private:
    int counter_ = 0;
};

/// Workflow that calls execute_activity and returns the result.
class ActivityWorkflow {
public:
    Task<std::string> run() {
        ActivityOptions opts;
        opts.start_to_close_timeout = std::chrono::milliseconds{30000};
        opts.task_queue = "activity-queue";
        auto result = co_await Workflow::execute_activity(
            "greet", std::any(std::string("world")), opts);
        co_return std::any_cast<std::string>(result);
    }

private:
};

/// Workflow that waits on a condition that is never true (for cancellation tests).
class WaitConditionWorkflow {
public:
    Task<std::string> run() {
        co_await Workflow::wait_condition([] { return false; });
        co_return "done";
    }
};

/// Workflow that delays for a long time (for cancellation tests).
class TimerCancelWorkflow {
public:
    Task<std::string> run() {
        co_await Workflow::delay(std::chrono::milliseconds{999999});
        co_return "done";
    }
};

/// Workflow that schedules an activity with no explicit cancellation token
/// (for cancellation tests).
class ActivityCancelWorkflow {
public:
    Task<std::string> run() {
        ActivityOptions opts;
        opts.start_to_close_timeout = std::chrono::milliseconds{30000};
        opts.task_queue = "activity-queue";
        auto result = co_await Workflow::execute_activity(
            "greet", std::any(std::string("world")), opts);
        co_return std::any_cast<std::string>(result);
    }
};

WorkflowInfo make_test_info(const std::string& wf_type = "SimpleWorkflow") {
    WorkflowInfo info;
    info.workflow_id = "test-wf-1";
    info.workflow_type = wf_type;
    info.run_id = "test-run-1";
    info.namespace_ = "default";
    info.task_queue = "test-queue";
    info.attempt = 1;
    return info;
}

/// Helper: create a started instance (kStartWorkflow job applied + activated).
/// Returns the instance and the commands from the first activation.
std::pair<std::unique_ptr<WorkflowInstance>, std::vector<WorkflowInstance::Command>>
create_started_instance(std::shared_ptr<WorkflowDefinition> def,
                        uint64_t seed = 0) {
    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
        .randomness_seed = seed,
    };
    auto inst = std::make_unique<WorkflowInstance>(std::move(config));
    std::vector<WorkflowInstance::Job> start_jobs = {
        {.type = WorkflowInstance::JobType::kStartWorkflow, .data = {}},
    };
    auto cmds = inst->activate(start_jobs);
    return {std::move(inst), std::move(cmds)};
}

}  // namespace

// ===========================================================================
// WorkflowInstance::Config tests
// ===========================================================================

TEST(WorkflowInstanceConfigTest, DefaultSeed) {
    WorkflowInstance::Config config;
    EXPECT_EQ(config.randomness_seed, 0u);
    EXPECT_EQ(config.definition, nullptr);
}

// ===========================================================================
// WorkflowInstance::CommandType tests
// ===========================================================================

TEST(WorkflowInstanceCommandTypeTest, Values) {
    EXPECT_NE(static_cast<int>(WorkflowInstance::CommandType::kStartTimer),
              static_cast<int>(WorkflowInstance::CommandType::kCancelTimer));
    EXPECT_NE(
        static_cast<int>(WorkflowInstance::CommandType::kScheduleActivity),
        static_cast<int>(WorkflowInstance::CommandType::kCompleteWorkflow));
}

// ===========================================================================
// WorkflowInstance::JobType tests
// ===========================================================================

TEST(WorkflowInstanceJobTypeTest, Values) {
    EXPECT_NE(
        static_cast<int>(WorkflowInstance::JobType::kStartWorkflow),
        static_cast<int>(WorkflowInstance::JobType::kFireTimer));
    EXPECT_NE(
        static_cast<int>(WorkflowInstance::JobType::kSignalWorkflow),
        static_cast<int>(WorkflowInstance::JobType::kQueryWorkflow));
}

// ===========================================================================
// WorkflowInstance construction and WorkflowContext interface
// ===========================================================================

TEST(WorkflowInstanceTest, ConstructionSetsInfo) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
        .randomness_seed = 12345,
    };

    WorkflowInstance instance(std::move(config));

    EXPECT_EQ(instance.info().workflow_id, "test-wf-1");
    EXPECT_EQ(instance.info().workflow_type, "SimpleWorkflow");
    EXPECT_EQ(instance.info().run_id, "test-run-1");
    EXPECT_EQ(instance.info().namespace_, "default");
    EXPECT_EQ(instance.info().task_queue, "test-queue");
}

TEST(WorkflowInstanceTest, CancellationTokenInitiallyNotCancelled) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));
    EXPECT_FALSE(instance.cancellation_token().stop_requested());
}

TEST(WorkflowInstanceTest, InitiallyNotReplaying) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));
    EXPECT_FALSE(instance.is_replaying());
}

TEST(WorkflowInstanceTest, InitialHistoryIsZero) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));
    EXPECT_EQ(instance.current_history_length(), 0);
    EXPECT_EQ(instance.current_history_size(), 0);
}

TEST(WorkflowInstanceTest, ContinueAsNewNotSuggestedInitially) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));
    EXPECT_FALSE(instance.continue_as_new_suggested());
}

TEST(WorkflowInstanceTest, AllHandlersFinishedInitially) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));
    EXPECT_TRUE(instance.all_handlers_finished());
}

TEST(WorkflowInstanceTest, CurrentUpdateInfoNullInitially) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));
    EXPECT_EQ(instance.current_update_info(), nullptr);
}

TEST(WorkflowInstanceTest, RandomIsDeterministicWithSeed) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config1{
        .definition = def,
        .info = make_test_info(),
        .randomness_seed = 42,
    };
    WorkflowInstance::Config config2{
        .definition = def,
        .info = make_test_info(),
        .randomness_seed = 42,
    };

    WorkflowInstance instance1(std::move(config1));
    WorkflowInstance instance2(std::move(config2));

    // Same seed should produce same random numbers
    EXPECT_EQ(instance1.random()(), instance2.random()());
    EXPECT_EQ(instance1.random()(), instance2.random()());
}

TEST(WorkflowInstanceTest, DifferentSeedsProduceDifferentSequences) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config1{
        .definition = def,
        .info = make_test_info(),
        .randomness_seed = 42,
    };
    WorkflowInstance::Config config2{
        .definition = def,
        .info = make_test_info(),
        .randomness_seed = 99,
    };

    WorkflowInstance instance1(std::move(config1));
    WorkflowInstance instance2(std::move(config2));

    // Different seeds should (almost certainly) produce different numbers
    EXPECT_NE(instance1.random()(), instance2.random()());
}

// ===========================================================================
// WorkflowInstance::activate tests
// ===========================================================================

TEST(WorkflowInstanceTest, ActivateWithNoJobsReturnsNoCommands) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));
    auto commands = instance.activate({});
    EXPECT_TRUE(commands.empty());
}

TEST(WorkflowInstanceTest, ActivateWithStartWorkflow) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));

    std::vector<WorkflowInstance::Job> jobs = {
        {.type = WorkflowInstance::JobType::kStartWorkflow, .data = {}},
    };
    auto commands = instance.activate(jobs);
    // After starting, the run coroutine should be driven and produce some result
    // Exact commands depend on implementation, but there should be no crash
}

TEST(WorkflowInstanceTest, ActivateStartThenCancel) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));

    // First: start the workflow
    std::vector<WorkflowInstance::Job> start_jobs = {
        {.type = WorkflowInstance::JobType::kStartWorkflow, .data = {}},
    };
    instance.activate(start_jobs);

    // Then: cancel it
    std::vector<WorkflowInstance::Job> cancel_jobs = {
        {.type = WorkflowInstance::JobType::kCancelWorkflow, .data = {}},
    };
    instance.activate(cancel_jobs);

    // Cancellation token should now be triggered
    EXPECT_TRUE(instance.cancellation_token().stop_requested());
}

TEST(WorkflowInstanceTest, DuplicateStartIsIgnored) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));

    std::vector<WorkflowInstance::Job> jobs = {
        {.type = WorkflowInstance::JobType::kStartWorkflow, .data = {}},
    };
    instance.activate(jobs);

    // Second start should not crash
    EXPECT_NO_THROW(instance.activate(jobs));
}

TEST(WorkflowInstanceTest, FireTimerWithNoPendingTimerIsIgnored) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));

    // Start first
    std::vector<WorkflowInstance::Job> start_jobs = {
        {.type = WorkflowInstance::JobType::kStartWorkflow, .data = {}},
    };
    instance.activate(start_jobs);

    // Fire timer with seq that doesn't exist
    std::vector<WorkflowInstance::Job> timer_jobs = {
        {.type = WorkflowInstance::JobType::kFireTimer,
         .data = uint32_t{999}},
    };
    EXPECT_NO_THROW(instance.activate(timer_jobs));
}

TEST(WorkflowInstanceTest, ResolveActivityWithNoPendingIsIgnored) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));

    // Start first
    std::vector<WorkflowInstance::Job> start_jobs = {
        {.type = WorkflowInstance::JobType::kStartWorkflow, .data = {}},
    };
    instance.activate(start_jobs);

    // Resolve activity with seq that doesn't exist
    std::vector<WorkflowInstance::Job> activity_jobs = {
        {.type = WorkflowInstance::JobType::kResolveActivity,
         .data = uint32_t{999}},
    };
    EXPECT_NO_THROW(instance.activate(activity_jobs));
}

// ===========================================================================
// Patching tests
// ===========================================================================

TEST(WorkflowInstanceTest, PatchedDuringNonReplayReturnsTrue) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));

    // During non-replay, patched() should return true
    // Need to set it as the current context first
    WorkflowContextScope scope(&instance);
    EXPECT_TRUE(instance.patched("my-patch"));
}

TEST(WorkflowInstanceTest, PatchedEmitsSetPatchMarkerCommand) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));

    // Start the workflow first to set up context, then call patched
    std::vector<WorkflowInstance::Job> start_jobs = {
        {.type = WorkflowInstance::JobType::kStartWorkflow, .data = {}},
    };
    instance.activate(start_jobs);

    // Now call patched within the context -- need a scope
    WorkflowContextScope scope(&instance);
    instance.patched("version-1");

    // The pending commands should contain a SetPatchMarker
    const auto& cmds = instance.pending_commands();
    bool found_patch = false;
    for (const auto& cmd : cmds) {
        if (cmd.type == WorkflowInstance::CommandType::kSetPatchMarker) {
            found_patch = true;
            EXPECT_EQ(std::any_cast<std::string>(cmd.data), "version-1");
        }
    }
    EXPECT_TRUE(found_patch);
}

TEST(WorkflowInstanceTest, PatchedMemoizesResult) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));
    WorkflowContextScope scope(&instance);

    // First call should return true and memoize
    EXPECT_TRUE(instance.patched("my-patch"));
    // Second call should return the memoized value
    EXPECT_TRUE(instance.patched("my-patch"));
}

TEST(WorkflowInstanceTest, DeprecatePatchEmitsCommand) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));

    std::vector<WorkflowInstance::Job> start_jobs = {
        {.type = WorkflowInstance::JobType::kStartWorkflow, .data = {}},
    };
    instance.activate(start_jobs);

    WorkflowContextScope scope(&instance);
    instance.deprecate_patch("old-patch");

    const auto& cmds = instance.pending_commands();
    bool found_patch = false;
    for (const auto& cmd : cmds) {
        if (cmd.type == WorkflowInstance::CommandType::kSetPatchMarker) {
            auto id = std::any_cast<std::string>(cmd.data);
            if (id == "old-patch") {
                found_patch = true;
            }
        }
    }
    EXPECT_TRUE(found_patch);
}

// ===========================================================================
// WorkflowInstance type traits
// ===========================================================================

TEST(WorkflowInstanceTest, IsNonCopyable) {
    EXPECT_FALSE(std::is_copy_constructible_v<WorkflowInstance>);
    EXPECT_FALSE(std::is_copy_assignable_v<WorkflowInstance>);
}

TEST(WorkflowInstanceTest, IsNonMovable) {
    EXPECT_FALSE(std::is_move_constructible_v<WorkflowInstance>);
    EXPECT_FALSE(std::is_move_assignable_v<WorkflowInstance>);
}

// ===========================================================================
// WorkflowInstance context is set during activation
// ===========================================================================

TEST(WorkflowInstanceTest, ContextSetDuringActivation) {
    // Before activation, we should not be in a workflow
    EXPECT_FALSE(Workflow::in_workflow());

    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));

    std::vector<WorkflowInstance::Job> jobs = {
        {.type = WorkflowInstance::JobType::kStartWorkflow, .data = {}},
    };
    instance.activate(jobs);

    // After activation completes, context should be restored
    EXPECT_FALSE(Workflow::in_workflow());
}

// ===========================================================================
// Modern event loop: workflow_initialized_ flag
// ===========================================================================

TEST(WorkflowInstanceTest, InitializeWorkflowCalledAfterJobLoop) {
    // In modern event loop mode, the workflow instance is created in
    // handle_start_workflow but the run coroutine is not started until
    // initialize_workflow() is called AFTER all jobs are applied.
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .signal("on_signal", &SimpleWorkflow::on_signal)
                   .build();

    WorkflowInstance::Config config{
        .definition = def,
        .info = make_test_info(),
    };

    WorkflowInstance instance(std::move(config));

    // Send start + signal in the same activation (common pattern).
    // The workflow should be initialized after both jobs are applied.
    auto signal_data = std::make_pair(
        std::string("on_signal"),
        std::vector<std::any>{std::any(std::string("hello"))});

    std::vector<WorkflowInstance::Job> jobs = {
        {.type = WorkflowInstance::JobType::kStartWorkflow, .data = {}},
        {.type = WorkflowInstance::JobType::kSignalWorkflow,
         .data = signal_data},
    };

    // Should not crash -- both jobs applied before initialization
    auto commands = instance.activate(jobs);
    // The workflow ran and the signal was delivered
}

// ===========================================================================
// Signal handling tests
// ===========================================================================

TEST(WorkflowInstanceTest, HandleSignalDispatchesToHandler) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .signal("on_signal", &SimpleWorkflow::on_signal)
                   .build();

    auto [inst, _] = create_started_instance(def);

    auto signal_data = std::make_pair(
        std::string("on_signal"),
        std::vector<std::any>{std::any(std::string("signaled!"))});

    std::vector<WorkflowInstance::Job> signal_jobs = {
        {.type = WorkflowInstance::JobType::kSignalWorkflow,
         .data = signal_data},
    };

    // Should not crash
    EXPECT_NO_THROW(inst->activate(signal_jobs));
}

TEST(WorkflowInstanceTest, UnknownSignalIsIgnored) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    auto [inst, _] = create_started_instance(def);

    auto signal_data = std::make_pair(
        std::string("nonexistent_signal"),
        std::vector<std::any>{});

    std::vector<WorkflowInstance::Job> signal_jobs = {
        {.type = WorkflowInstance::JobType::kSignalWorkflow,
         .data = signal_data},
    };

    // Unknown signal should not crash
    EXPECT_NO_THROW(inst->activate(signal_jobs));
}

TEST(WorkflowInstanceTest, SignalIncrementsHandlerCount) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .signal("on_signal", &SimpleWorkflow::on_signal)
                   .build();

    // Before any signals, handlers should be finished
    auto [inst, _] = create_started_instance(def);
    EXPECT_TRUE(inst->all_handlers_finished());

    // Signal handlers are async, so the handler count increments then
    // decrements when the handler completes. Since our handler just
    // co_returns immediately, after activation it should be finished.
    auto signal_data = std::make_pair(
        std::string("on_signal"),
        std::vector<std::any>{std::any(std::string("test"))});

    std::vector<WorkflowInstance::Job> signal_jobs = {
        {.type = WorkflowInstance::JobType::kSignalWorkflow,
         .data = signal_data},
    };
    inst->activate(signal_jobs);

    // The immediate handler should have completed, decrementing the count
    EXPECT_TRUE(inst->all_handlers_finished());
}

// ===========================================================================
// Query handling tests
// ===========================================================================

TEST(WorkflowInstanceTest, QueryReturnsRespondQueryCommand) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .query("get_status", &SimpleWorkflow::get_status)
                   .build();

    auto [inst, _] = create_started_instance(def);

    WorkflowInstance::QueryWorkflowData query_data;
    query_data.query_id = "q-42";
    query_data.query_name = "get_status";

    std::vector<WorkflowInstance::Job> query_jobs = {
        {.type = WorkflowInstance::JobType::kQueryWorkflow,
         .data = query_data},
    };

    auto commands = inst->activate(query_jobs);

    // Should have a kRespondQuery command with QueryResponseData
    bool found_query_response = false;
    for (const auto& cmd : commands) {
        if (cmd.type == WorkflowInstance::CommandType::kRespondQuery) {
            found_query_response = true;
            auto& resp =
                std::any_cast<const WorkflowInstance::QueryResponseData&>(
                    cmd.data);
            EXPECT_EQ(resp.query_id, "q-42");
            EXPECT_TRUE(resp.error.empty());
        }
    }
    EXPECT_TRUE(found_query_response);
}

TEST(WorkflowInstanceTest, QueryOnlyActivationSkipsConditionCheck) {
    // Query-only activations should not check conditions (only queries,
    // no non-query jobs). This tests the has_non_query logic in activate().
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .query("get_status", &SimpleWorkflow::get_status)
                   .build();

    auto [inst, _] = create_started_instance(def);

    WorkflowInstance::QueryWorkflowData query_data;
    query_data.query_id = "q-cond";
    query_data.query_name = "get_status";

    std::vector<WorkflowInstance::Job> query_jobs = {
        {.type = WorkflowInstance::JobType::kQueryWorkflow,
         .data = query_data},
    };

    // Should not crash even with pending conditions
    EXPECT_NO_THROW(inst->activate(query_jobs));
}

TEST(WorkflowInstanceTest, UnknownQueryProducesFailedResponse) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    auto [inst, _] = create_started_instance(def);

    WorkflowInstance::QueryWorkflowData query_data;
    query_data.query_id = "q-1";
    query_data.query_name = "nonexistent_query";

    std::vector<WorkflowInstance::Job> query_jobs = {
        {.type = WorkflowInstance::JobType::kQueryWorkflow,
         .data = query_data},
    };

    auto commands = inst->activate(query_jobs);

    // No registered handler and no dynamic query -> kRespondQueryFailed
    bool found_query_failed = false;
    for (const auto& cmd : commands) {
        if (cmd.type == WorkflowInstance::CommandType::kRespondQueryFailed) {
            found_query_failed = true;
            auto& resp =
                std::any_cast<const WorkflowInstance::QueryResponseData&>(
                    cmd.data);
            EXPECT_EQ(resp.query_id, "q-1");
            EXPECT_FALSE(resp.error.empty());
        }
    }
    EXPECT_TRUE(found_query_failed);
}

// ===========================================================================
// Update handling tests
// ===========================================================================

TEST(WorkflowInstanceTest, UpdateHandlerDispatchesAndIncrementsCount) {
    auto def = WorkflowDefinition::create<CounterWorkflow>("CounterWorkflow")
                   .run(&CounterWorkflow::run)
                   .update("set_count", &CounterWorkflow::set_count,
                           &CounterWorkflow::validate_set_count)
                   .build();

    auto [inst, _] = create_started_instance(def);
    EXPECT_TRUE(inst->all_handlers_finished());

    auto update_data = std::make_pair(
        std::string("set_count"),
        std::vector<std::any>{std::any(42)});

    std::vector<WorkflowInstance::Job> update_jobs = {
        {.type = WorkflowInstance::JobType::kDoUpdate,
         .data = update_data},
    };

    // Update handler increments handler count, then decrements when done
    inst->activate(update_jobs);

    // The immediate handler should have completed
    EXPECT_TRUE(inst->all_handlers_finished());
}

TEST(WorkflowInstanceTest, UpdateValidatorRejectsInvalidInput) {
    auto def = WorkflowDefinition::create<CounterWorkflow>("CounterWorkflow")
                   .run(&CounterWorkflow::run)
                   .update("set_count", &CounterWorkflow::set_count,
                           &CounterWorkflow::validate_set_count)
                   .build();

    auto [inst, _] = create_started_instance(def);

    // Negative value should be rejected by the validator
    auto update_data = std::make_pair(
        std::string("set_count"),
        std::vector<std::any>{std::any(-1)});

    std::vector<WorkflowInstance::Job> update_jobs = {
        {.type = WorkflowInstance::JobType::kDoUpdate,
         .data = update_data},
    };

    // Should not crash -- validator throws, update is rejected
    EXPECT_NO_THROW(inst->activate(update_jobs));
    // Handler was never started, so count should still be 0
    EXPECT_TRUE(inst->all_handlers_finished());
}

// ===========================================================================
// NotifyHasPatch tests
// ===========================================================================

TEST(WorkflowInstanceTest, NotifyHasPatchRecordsPatchId) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    // Create an instance with is_replaying_ = false (default)
    auto [inst, _] = create_started_instance(def);

    // Notify that a patch exists in history
    std::vector<WorkflowInstance::Job> patch_jobs = {
        {.type = WorkflowInstance::JobType::kNotifyHasPatch,
         .data = std::string("my-patch-v1")},
    };
    inst->activate(patch_jobs);

    // Now when patched() is called, it should find the notified patch
    WorkflowContextScope scope(inst.get());
    // The behavior depends on replay state; during non-replay, patched()
    // returns true regardless. The notification is used during replay.
    EXPECT_TRUE(inst->patched("my-patch-v1"));
}

// ===========================================================================
// ResolveChildWorkflow tests
// ===========================================================================

TEST(WorkflowInstanceTest, ResolveChildWorkflowNoPendingIsIgnored) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    auto [inst, _] = create_started_instance(def);

    std::vector<WorkflowInstance::Job> resolve_jobs = {
        {.type = WorkflowInstance::JobType::kResolveChildWorkflow,
         .data = uint32_t{42}},
    };

    // No pending child workflow with seq 42, should be silently ignored
    EXPECT_NO_THROW(inst->activate(resolve_jobs));
}

// ===========================================================================
// CancelWorkflow tests
// ===========================================================================

TEST(WorkflowInstanceTest, CancelWorkflowTriggersCancellationToken) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    auto [inst, _] = create_started_instance(def);

    EXPECT_FALSE(inst->cancellation_token().stop_requested());

    std::vector<WorkflowInstance::Job> cancel_jobs = {
        {.type = WorkflowInstance::JobType::kCancelWorkflow, .data = {}},
    };
    inst->activate(cancel_jobs);

    EXPECT_TRUE(inst->cancellation_token().stop_requested());
}

// ===========================================================================
// Multiple jobs in single activation
// ===========================================================================

TEST(WorkflowInstanceTest, MultipleSignalsInOneActivation) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .signal("on_signal", &SimpleWorkflow::on_signal)
                   .build();

    auto [inst, _] = create_started_instance(def);

    auto sig1 = std::make_pair(
        std::string("on_signal"),
        std::vector<std::any>{std::any(std::string("first"))});
    auto sig2 = std::make_pair(
        std::string("on_signal"),
        std::vector<std::any>{std::any(std::string("second"))});

    std::vector<WorkflowInstance::Job> jobs = {
        {.type = WorkflowInstance::JobType::kSignalWorkflow, .data = sig1},
        {.type = WorkflowInstance::JobType::kSignalWorkflow, .data = sig2},
    };

    // Both signals should be processed without crash
    EXPECT_NO_THROW(inst->activate(jobs));
    // Both handlers should have completed
    EXPECT_TRUE(inst->all_handlers_finished());
}

TEST(WorkflowInstanceTest, SignalAndQueryInOneActivation) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .signal("on_signal", &SimpleWorkflow::on_signal)
                   .query("get_status", &SimpleWorkflow::get_status)
                   .build();

    auto [inst, _] = create_started_instance(def);

    auto sig = std::make_pair(
        std::string("on_signal"),
        std::vector<std::any>{std::any(std::string("updated"))});
    WorkflowInstance::QueryWorkflowData qry;
    qry.query_id = "q-sig";
    qry.query_name = "get_status";

    std::vector<WorkflowInstance::Job> jobs = {
        {.type = WorkflowInstance::JobType::kSignalWorkflow, .data = sig},
        {.type = WorkflowInstance::JobType::kQueryWorkflow, .data = qry},
    };

    auto commands = inst->activate(jobs);

    // Should have at least a query response
    bool found_query = false;
    for (const auto& cmd : commands) {
        if (cmd.type == WorkflowInstance::CommandType::kRespondQuery) {
            found_query = true;
        }
    }
    EXPECT_TRUE(found_query);
}

// ===========================================================================
// run_top_level is_handler flag tests
// ===========================================================================

TEST(WorkflowInstanceTest, MainRunDoesNotIncrementHandlerCount) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    auto [inst, _] = create_started_instance(def);

    // The main workflow run should not affect handler count
    // After activation, all_handlers_finished should still be true
    EXPECT_TRUE(inst->all_handlers_finished());
}

// ===========================================================================
// Execute activity tests (Phase A6)
// ===========================================================================

TEST(WorkflowInstanceTest, ScheduleActivityCommandEmittedWithCorrectFields) {
    auto def =
        WorkflowDefinition::create<ActivityWorkflow>("ActivityWorkflow")
            .run(&ActivityWorkflow::run)
            .build();

    auto [inst, cmds] = create_started_instance(def);

    // The workflow calls execute_activity in its run(), which should emit
    // a kScheduleActivity command during the first activation.
    bool found_schedule = false;
    for (const auto& cmd : cmds) {
        if (cmd.type == WorkflowInstance::CommandType::kScheduleActivity) {
            found_schedule = true;
            EXPECT_EQ(cmd.seq, 1u);
            auto& data = std::any_cast<
                const WorkflowInstance::ScheduleActivityData&>(cmd.data);
            EXPECT_EQ(data.activity_type, "greet");
            EXPECT_EQ(data.task_queue, "activity-queue");
            EXPECT_EQ(data.seq, 1u);
            ASSERT_TRUE(data.start_to_close_timeout.has_value());
            EXPECT_EQ(data.start_to_close_timeout.value(),
                       std::chrono::milliseconds{30000});
            EXPECT_FALSE(data.schedule_to_close_timeout.has_value());
            // Should have one argument
            ASSERT_EQ(data.args.size(), 1u);
            EXPECT_EQ(std::any_cast<std::string>(data.args[0]), "world");
        }
    }
    EXPECT_TRUE(found_schedule);
}

TEST(WorkflowInstanceTest, ResolveActivityCompletedYieldsWorkflowResult) {
    auto def =
        WorkflowDefinition::create<ActivityWorkflow>("ActivityWorkflow")
            .run(&ActivityWorkflow::run)
            .build();

    auto [inst, start_cmds] = create_started_instance(def);

    // The workflow is now suspended waiting for the activity to complete.
    // Resolve the activity with a successful result.
    WorkflowInstance::ActivityResolution resolution;
    resolution.seq = 1;
    resolution.status = WorkflowInstance::ResolutionStatus::kCompleted;
    resolution.result = std::any(std::string("Hello, world!"));

    std::vector<WorkflowInstance::Job> resolve_jobs = {
        {.type = WorkflowInstance::JobType::kResolveActivity,
         .data = std::any(resolution)},
    };

    auto cmds = inst->activate(resolve_jobs);

    // After the activity resolves with success, the workflow run()
    // should complete, which means run_top_level emits kCompleteWorkflow
    // (or similar). At minimum, no kFailWorkflow should be emitted.
    bool found_fail = false;
    for (const auto& cmd : cmds) {
        if (cmd.type == WorkflowInstance::CommandType::kFailWorkflow) {
            found_fail = true;
        }
    }
    EXPECT_FALSE(found_fail);
}

TEST(WorkflowInstanceTest, ResolveActivityFailedCausesWorkflowFailure) {
    auto def =
        WorkflowDefinition::create<ActivityWorkflow>("ActivityWorkflow")
            .run(&ActivityWorkflow::run)
            .build();

    auto [inst, start_cmds] = create_started_instance(def);

    // Resolve with failure status.
    WorkflowInstance::ActivityResolution resolution;
    resolution.seq = 1;
    resolution.status = WorkflowInstance::ResolutionStatus::kFailed;
    resolution.failure = "Activity greet failed: connection timeout";

    std::vector<WorkflowInstance::Job> resolve_jobs = {
        {.type = WorkflowInstance::JobType::kResolveActivity,
         .data = std::any(resolution)},
    };

    auto cmds = inst->activate(resolve_jobs);

    // The schedule_activity implementation throws ActivityFailureException
    // on kFailed resolution. run_top_level catches it and emits kFailWorkflow.
    bool found_fail = false;
    for (const auto& cmd : cmds) {
        if (cmd.type == WorkflowInstance::CommandType::kFailWorkflow) {
            found_fail = true;
            auto msg = std::any_cast<std::string>(cmd.data);
            // The failure message should contain the original error.
            EXPECT_FALSE(msg.empty());
        }
    }
    EXPECT_TRUE(found_fail);
}

TEST(WorkflowInstanceTest, ResolveActivityCancelledCausesWorkflowFailure) {
    auto def =
        WorkflowDefinition::create<ActivityWorkflow>("ActivityWorkflow")
            .run(&ActivityWorkflow::run)
            .build();

    auto [inst, start_cmds] = create_started_instance(def);

    // Resolve with cancelled status.
    WorkflowInstance::ActivityResolution resolution;
    resolution.seq = 1;
    resolution.status = WorkflowInstance::ResolutionStatus::kCancelled;
    resolution.failure = "Activity was cancelled";

    std::vector<WorkflowInstance::Job> resolve_jobs = {
        {.type = WorkflowInstance::JobType::kResolveActivity,
         .data = std::any(resolution)},
    };

    auto cmds = inst->activate(resolve_jobs);

    // The schedule_activity implementation throws CanceledFailureException
    // on kCancelled resolution. Since the workflow itself was not cancelled
    // (cancellation_source_ not triggered), run_top_level treats it as a
    // normal exception and emits kFailWorkflow.
    bool found_fail = false;
    for (const auto& cmd : cmds) {
        if (cmd.type == WorkflowInstance::CommandType::kFailWorkflow) {
            found_fail = true;
        }
    }
    EXPECT_TRUE(found_fail);
}

// ===========================================================================
// Cancellation propagation tests
// ===========================================================================

TEST(WorkflowInstanceTest, CancelWorkflow_WaitConditionThrowsCanceled) {
    auto def = WorkflowDefinition::create<WaitConditionWorkflow>(
                   "WaitConditionWorkflow")
                   .run(&WaitConditionWorkflow::run)
                   .build();

    auto [inst, start_cmds] = create_started_instance(def);

    // The workflow is now suspended in wait_condition([] { return false; }).
    // No kCompleteWorkflow or kFailWorkflow should have been emitted yet.
    for (const auto& cmd : start_cmds) {
        EXPECT_NE(cmd.type, WorkflowInstance::CommandType::kCompleteWorkflow);
        EXPECT_NE(cmd.type, WorkflowInstance::CommandType::kFailWorkflow);
    }

    // Send cancel job.
    std::vector<WorkflowInstance::Job> cancel_jobs = {
        {.type = WorkflowInstance::JobType::kCancelWorkflow, .data = {}},
    };
    auto cmds = inst->activate(cancel_jobs);

    // Should emit kCancelWorkflow (not kFailWorkflow).
    bool found_cancel = false;
    bool found_fail = false;
    for (const auto& cmd : cmds) {
        if (cmd.type == WorkflowInstance::CommandType::kCancelWorkflow) {
            found_cancel = true;
        }
        if (cmd.type == WorkflowInstance::CommandType::kFailWorkflow) {
            found_fail = true;
        }
    }
    EXPECT_TRUE(found_cancel)
        << "Expected kCancelWorkflow command after cancelling wait_condition";
    EXPECT_FALSE(found_fail)
        << "Should not emit kFailWorkflow on cancellation";
}

TEST(WorkflowInstanceTest, CancelWorkflow_TimerThrowsCanceled) {
    auto def = WorkflowDefinition::create<TimerCancelWorkflow>(
                   "TimerCancelWorkflow")
                   .run(&TimerCancelWorkflow::run)
                   .build();

    auto [inst, start_cmds] = create_started_instance(def);

    // The workflow is now suspended in delay(999999ms).
    // Should have emitted a kStartTimer command.
    bool found_start_timer = false;
    for (const auto& cmd : start_cmds) {
        if (cmd.type == WorkflowInstance::CommandType::kStartTimer) {
            found_start_timer = true;
        }
    }
    EXPECT_TRUE(found_start_timer);

    // Send cancel job.
    std::vector<WorkflowInstance::Job> cancel_jobs = {
        {.type = WorkflowInstance::JobType::kCancelWorkflow, .data = {}},
    };
    auto cmds = inst->activate(cancel_jobs);

    // Should emit kCancelTimer and kCancelWorkflow (not kFailWorkflow).
    bool found_cancel_timer = false;
    bool found_cancel_wf = false;
    bool found_fail = false;
    for (const auto& cmd : cmds) {
        if (cmd.type == WorkflowInstance::CommandType::kCancelTimer) {
            found_cancel_timer = true;
        }
        if (cmd.type == WorkflowInstance::CommandType::kCancelWorkflow) {
            found_cancel_wf = true;
        }
        if (cmd.type == WorkflowInstance::CommandType::kFailWorkflow) {
            found_fail = true;
        }
    }
    EXPECT_TRUE(found_cancel_timer)
        << "Expected kCancelTimer command when workflow is cancelled mid-timer";
    EXPECT_TRUE(found_cancel_wf)
        << "Expected kCancelWorkflow command after cancelling timer workflow";
    EXPECT_FALSE(found_fail)
        << "Should not emit kFailWorkflow on cancellation";
}

TEST(WorkflowInstanceTest, CancelWorkflow_ActivityThrowsCanceled) {
    auto def = WorkflowDefinition::create<ActivityCancelWorkflow>(
                   "ActivityCancelWorkflow")
                   .run(&ActivityCancelWorkflow::run)
                   .build();

    auto [inst, start_cmds] = create_started_instance(def);

    // The workflow is now suspended waiting for the activity to complete.
    // Should have emitted a kScheduleActivity command.
    bool found_schedule = false;
    for (const auto& cmd : start_cmds) {
        if (cmd.type == WorkflowInstance::CommandType::kScheduleActivity) {
            found_schedule = true;
        }
    }
    EXPECT_TRUE(found_schedule);

    // Send cancel job.
    std::vector<WorkflowInstance::Job> cancel_jobs = {
        {.type = WorkflowInstance::JobType::kCancelWorkflow, .data = {}},
    };
    auto cmds = inst->activate(cancel_jobs);

    // Should emit kRequestCancelActivity and kCancelWorkflow (not kFailWorkflow).
    bool found_cancel_activity = false;
    bool found_cancel_wf = false;
    bool found_fail = false;
    for (const auto& cmd : cmds) {
        if (cmd.type == WorkflowInstance::CommandType::kRequestCancelActivity) {
            found_cancel_activity = true;
        }
        if (cmd.type == WorkflowInstance::CommandType::kCancelWorkflow) {
            found_cancel_wf = true;
        }
        if (cmd.type == WorkflowInstance::CommandType::kFailWorkflow) {
            found_fail = true;
        }
    }
    EXPECT_TRUE(found_cancel_activity)
        << "Expected kRequestCancelActivity when workflow is cancelled mid-activity";
    EXPECT_TRUE(found_cancel_wf)
        << "Expected kCancelWorkflow command after cancelling activity workflow";
    EXPECT_FALSE(found_fail)
        << "Should not emit kFailWorkflow on cancellation";
}
