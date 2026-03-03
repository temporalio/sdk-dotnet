/// @file integration_tests.cpp
/// @brief P0 + P1 integration tests for the Temporal C++ SDK.
///
/// These tests require a running Temporal server. They are automatically
/// skipped if the WorkflowEnvironment fixture did not initialize (e.g., no
/// local dev server or external server configured).

#include <gtest/gtest.h>

#include <temporalio/activities/activity.h>
#include <temporalio/client/temporal_client.h>
#include <temporalio/client/workflow_handle.h>
#include <temporalio/client/workflow_options.h>
#include <temporalio/coro/run_sync.h>
#include <temporalio/coro/task.h>
#include <temporalio/worker/temporal_worker.h>
#include <temporalio/workflows/activity_options.h>
#include <temporalio/workflows/workflow.h>
#include <temporalio/workflows/workflow_definition.h>

#include "../fixtures/workflow_environment_fixture.h"

#include <temporalio/exceptions/temporal_exception.h>

#include <atomic>
#include <chrono>
#include <cstdint>
#include <stdexcept>
#include <stop_token>
#include <string>
#include <thread>
#include <vector>

using temporalio::coro::run_task_sync;
using temporalio::coro::Task;

namespace wf = temporalio::workflows;
namespace client = temporalio::client;
namespace worker = temporalio::worker;
namespace activities = temporalio::activities;

// ============================================================================
// Helper workflow and activity definitions
// ============================================================================

// -- Simple greeting workflow (P0: BasicWorkflowRoundTrip) --

class EchoWorkflow {
public:
    Task<std::string> run(std::string input) {
        co_return "echo:" + input;
    }
};

// -- Activity that does string formatting --

Task<std::string> format_greeting(std::string name) {
    co_return "Hello, " + name + "!";
}

// -- Workflow that calls an activity (P0: ActivityExecution) --

class ActivityCallerWorkflow {
public:
    Task<std::string> run(std::string name) {
        wf::ActivityOptions opts;
        opts.start_to_close_timeout = std::chrono::seconds(30);

        auto result = co_await wf::Workflow::execute_activity<std::string>(
            "format_greeting", opts, name);

        co_return result;
    }
};

// -- Workflow that just returns immediately (P0: WorkerLifecycle) --

class NoOpWorkflow {
public:
    Task<std::string> run() {
        co_return "done";
    }
};

// -- Workflow that returns its input * 2 (P0: ConcurrentWorkflows) --

class DoubleWorkflow {
public:
    Task<int> run(int value) {
        co_return value * 2;
    }
};

// -- Accumulator workflow with signal + query (P1: SignalAndQuery) --

class SignalQueryWorkflow {
public:
    Task<std::string> run() {
        co_await wf::Workflow::wait_condition(
            [this]() { return done_; });
        co_return accumulated_;
    }

    Task<void> add_value(std::string val) {
        if (!accumulated_.empty()) {
            accumulated_ += ",";
        }
        accumulated_ += val;
        co_return;
    }

    Task<void> finish() {
        done_ = true;
        co_return;
    }

    std::string get_values() const { return accumulated_; }

private:
    std::string accumulated_;
    bool done_ = false;
};

// -- Shopping cart workflow with update + validator (P1: UpdateWithValidator) --

class UpdateCartWorkflow {
public:
    Task<std::string> run() {
        co_await wf::Workflow::wait_condition(
            [this]() { return checked_out_; });
        co_await wf::Workflow::wait_condition(
            []() { return wf::Workflow::all_handlers_finished(); });
        co_return result_;
    }

    void validate_add_item(std::string item) {
        if (item.empty()) {
            throw std::invalid_argument("Item name cannot be empty");
        }
    }

    Task<int> add_item(std::string item) {
        if (!result_.empty()) {
            result_ += ",";
        }
        result_ += item;
        count_++;
        co_return count_;
    }

    Task<void> checkout() {
        checked_out_ = true;
        co_return;
    }

private:
    std::string result_;
    int count_ = 0;
    bool checked_out_ = false;
};

// -- Cancelable workflow (P1: WorkflowCancellation) --

class CancelableWorkflow {
public:
    Task<std::string> run() {
        // Wait on a condition that is never true; cancellation will interrupt.
        co_await wf::Workflow::wait_condition([] { return false; });
        co_return "not-cancelled";
    }
};

// -- Timer workflow (P1: TimerWorkflow) --

class DelayWorkflow {
public:
    Task<std::string> run() {
        using namespace std::chrono_literals;
        co_await wf::Workflow::delay(500ms);
        co_return "delayed";
    }
};

// -- Workflow that returns after a condition (P1: WorkflowDescribe) --

class DescribableWorkflow {
public:
    Task<std::string> run(std::string input) {
        co_return "result:" + input;
    }
};

// -- Activity that throws (P1: ErrorPropagation) --

// The co_return after throw is unreachable but required to make this a
// coroutine. Suppress C4702 on MSVC (treated as error under /WX).
#ifdef _MSC_VER
#pragma warning(push)
#pragma warning(disable : 4702)
#endif

Task<std::string> failing_activity() {
    throw std::runtime_error("activity-failure-message");
    co_return "";
}

#ifdef _MSC_VER
#pragma warning(pop)
#endif

class ErrorWorkflow {
public:
    Task<std::string> run() {
        wf::ActivityOptions opts;
        opts.start_to_close_timeout = std::chrono::seconds(10);
        // Disable retries so the error propagates immediately.
        temporalio::common::RetryPolicy retry;
        retry.maximum_attempts = 1;
        opts.retry_policy = retry;

        try {
            co_await wf::Workflow::execute_activity<std::string>(
                "failing_activity", opts);
            co_return "no-error";
        } catch (const std::exception& e) {
            co_return std::string("caught:") + e.what();
        }
    }
};

// ============================================================================
// Test fixture
// ============================================================================

class IntegrationTest : public temporalio::testing::WorkflowEnvironmentTestBase {
protected:
    // Helper to run a worker in the background and execute a function,
    // then cleanly shut down.
    template <typename Func>
    void with_worker(const worker::TemporalWorkerOptions& opts, Func&& func) {
        worker::TemporalWorker w(client(), opts);

        std::stop_source worker_stop;
        std::jthread worker_thread([&w, token = worker_stop.get_token()]() {
            try {
                run_task_sync(w.execute_async(token));
            } catch (const std::exception&) {
                // Worker shutdown - expected when stop is requested
            }
        });

        try {
            func();
        } catch (...) {
            worker_stop.request_stop();
            worker_thread.join();
            throw;
        }

        worker_stop.request_stop();
        worker_thread.join();
    }
};

// ============================================================================
// P0 Tests - Critical path
// ============================================================================

// P0-1: Start a simple workflow and get a string result back.
TEST_F(IntegrationTest, BasicWorkflowRoundTrip) {
    auto tq = unique_task_queue();

    auto echo_def = wf::WorkflowDefinition::create<EchoWorkflow>("Echo")
        .run(&EchoWorkflow::run)
        .build();

    worker::TemporalWorkerOptions worker_opts;
    worker_opts.task_queue = tq;
    worker_opts.workflows.push_back(echo_def);

    with_worker(worker_opts, [&]() {
        client::WorkflowOptions opts;
        opts.id = "basic-roundtrip-" + tq;
        opts.task_queue = tq;

        auto handle = run_task_sync(
            client()->start_workflow("Echo", opts, std::string("test-input")));

        EXPECT_FALSE(handle.id().empty());
        EXPECT_TRUE(handle.run_id().has_value());

        auto result = run_task_sync(handle.get_result<std::string>());
        EXPECT_EQ(result, "echo:test-input");
    });
}

// P0-2: Workflow calls an activity, activity returns, workflow returns.
TEST_F(IntegrationTest, ActivityExecution) {
    auto tq = unique_task_queue();

    auto act_def = activities::ActivityDefinition::create(
        "format_greeting", &format_greeting);

    auto wf_def = wf::WorkflowDefinition::create<ActivityCallerWorkflow>(
        "ActivityCaller")
        .run(&ActivityCallerWorkflow::run)
        .build();

    worker::TemporalWorkerOptions worker_opts;
    worker_opts.task_queue = tq;
    worker_opts.workflows.push_back(wf_def);
    worker_opts.activities.push_back(act_def);

    with_worker(worker_opts, [&]() {
        client::WorkflowOptions opts;
        opts.id = "activity-exec-" + tq;
        opts.task_queue = tq;

        auto handle = run_task_sync(
            client()->start_workflow(
                "ActivityCaller", opts, std::string("Temporal")));

        auto result = run_task_sync(handle.get_result<std::string>());
        EXPECT_EQ(result, "Hello, Temporal!");
    });
}

// P0-3: Start worker, run workflow, graceful shutdown without crash.
TEST_F(IntegrationTest, WorkerLifecycle) {
    auto tq = unique_task_queue();

    auto noop_def = wf::WorkflowDefinition::create<NoOpWorkflow>("NoOp")
        .run(&NoOpWorkflow::run)
        .build();

    worker::TemporalWorkerOptions worker_opts;
    worker_opts.task_queue = tq;
    worker_opts.workflows.push_back(noop_def);

    // This test verifies that the worker starts, processes a workflow, and
    // shuts down cleanly without crashing or hanging.
    worker::TemporalWorker w(client(), worker_opts);
    std::stop_source worker_stop;

    std::jthread worker_thread([&w, token = worker_stop.get_token()]() {
        run_task_sync(w.execute_async(token));
    });

    // Run a workflow to prove the worker is functional.
    client::WorkflowOptions opts;
    opts.id = "worker-lifecycle-" + tq;
    opts.task_queue = tq;

    auto handle = run_task_sync(client()->start_workflow("NoOp", opts));
    auto result = run_task_sync(handle.get_result<std::string>());
    EXPECT_EQ(result, "done");

    // Graceful shutdown.
    worker_stop.request_stop();
    worker_thread.join();
    // If we get here without hanging or crashing, the test passes.
}

// P0-4: Run 5+ workflows simultaneously, all complete successfully.
TEST_F(IntegrationTest, ConcurrentWorkflows) {
    auto tq = unique_task_queue();

    auto double_def = wf::WorkflowDefinition::create<DoubleWorkflow>("Double")
        .run(&DoubleWorkflow::run)
        .build();

    worker::TemporalWorkerOptions worker_opts;
    worker_opts.task_queue = tq;
    worker_opts.workflows.push_back(double_def);

    with_worker(worker_opts, [&]() {
        constexpr int kNumWorkflows = 5;
        std::vector<client::WorkflowHandle> handles;
        handles.reserve(kNumWorkflows);

        // Start all workflows.
        for (int i = 0; i < kNumWorkflows; ++i) {
            client::WorkflowOptions opts;
            opts.id = "concurrent-" + tq + "-" + std::to_string(i);
            opts.task_queue = tq;

            auto handle = run_task_sync(
                client()->start_workflow("Double", opts, i));
            handles.push_back(std::move(handle));
        }

        // Wait for all results and verify.
        for (int i = 0; i < kNumWorkflows; ++i) {
            auto result = run_task_sync(handles[static_cast<size_t>(i)].get_result<int>());
            EXPECT_EQ(result, i * 2) << "Workflow " << i << " returned wrong result";
        }
    });
}

// ============================================================================
// P1 Tests - Important features
// ============================================================================

// P1-5: Send signal, query state, verify updated value.
TEST_F(IntegrationTest, SignalAndQuery) {
    auto tq = unique_task_queue();

    auto sig_def = wf::WorkflowDefinition::create<SignalQueryWorkflow>(
        "SignalQuery")
        .run(&SignalQueryWorkflow::run)
        .signal("add_value", &SignalQueryWorkflow::add_value)
        .signal("finish", &SignalQueryWorkflow::finish)
        .query("get_values", &SignalQueryWorkflow::get_values)
        .build();

    worker::TemporalWorkerOptions worker_opts;
    worker_opts.task_queue = tq;
    worker_opts.workflows.push_back(sig_def);

    with_worker(worker_opts, [&]() {
        client::WorkflowOptions opts;
        opts.id = "signal-query-" + tq;
        opts.task_queue = tq;

        auto handle = run_task_sync(
            client()->start_workflow("SignalQuery", opts));

        // Send signals.
        run_task_sync(handle.signal("add_value", std::string("alpha")));
        run_task_sync(handle.signal("add_value", std::string("beta")));

        // Query the current state.
        auto queried = run_task_sync(handle.query<std::string>("get_values"));
        EXPECT_EQ(queried, "alpha,beta");

        // Signal done and get final result.
        run_task_sync(handle.signal("finish"));
        auto result = run_task_sync(handle.get_result<std::string>());
        EXPECT_EQ(result, "alpha,beta");
    });
}

// P1-6: Update with passing validator succeeds; failing validator rejects.
TEST_F(IntegrationTest, UpdateWithValidator) {
    auto tq = unique_task_queue();

    auto cart_def = wf::WorkflowDefinition::create<UpdateCartWorkflow>(
        "UpdateCart")
        .run(&UpdateCartWorkflow::run)
        .update("add_item",
                &UpdateCartWorkflow::add_item,
                &UpdateCartWorkflow::validate_add_item)
        .signal("checkout", &UpdateCartWorkflow::checkout)
        .build();

    worker::TemporalWorkerOptions worker_opts;
    worker_opts.task_queue = tq;
    worker_opts.workflows.push_back(cart_def);

    with_worker(worker_opts, [&]() {
        client::WorkflowOptions opts;
        opts.id = "update-validator-" + tq;
        opts.task_queue = tq;

        auto handle = run_task_sync(
            client()->start_workflow("UpdateCart", opts));

        // Valid update should succeed.
        auto count1 = run_task_sync(
            handle.update<int>("add_item", std::string("apple")));
        EXPECT_EQ(count1, 1);

        auto count2 = run_task_sync(
            handle.update<int>("add_item", std::string("banana")));
        EXPECT_EQ(count2, 2);

        // Invalid update (empty name) should be rejected by the validator.
        EXPECT_THROW(
            run_task_sync(
                handle.update<int>("add_item", std::string(""))),
            std::exception);

        // Signal checkout and verify final result.
        run_task_sync(handle.signal("checkout"));
        auto result = run_task_sync(handle.get_result<std::string>());
        EXPECT_EQ(result, "apple,banana");
    });
}

// P1-7: Workflow with a timer/delay, verify completion.
TEST_F(IntegrationTest, TimerWorkflow) {
    auto tq = unique_task_queue();

    auto timer_def = wf::WorkflowDefinition::create<DelayWorkflow>("Delay")
        .run(&DelayWorkflow::run)
        .build();

    worker::TemporalWorkerOptions worker_opts;
    worker_opts.task_queue = tq;
    worker_opts.workflows.push_back(timer_def);

    with_worker(worker_opts, [&]() {
        client::WorkflowOptions opts;
        opts.id = "timer-" + tq;
        opts.task_queue = tq;

        auto handle = run_task_sync(
            client()->start_workflow("Delay", opts));

        auto result = run_task_sync(handle.get_result<std::string>());
        EXPECT_EQ(result, "delayed");
    });
}

// P1-8: Describe a completed workflow and verify metadata.
TEST_F(IntegrationTest, WorkflowDescribe) {
    auto tq = unique_task_queue();

    auto desc_def = wf::WorkflowDefinition::create<DescribableWorkflow>(
        "Describable")
        .run(&DescribableWorkflow::run)
        .build();

    worker::TemporalWorkerOptions worker_opts;
    worker_opts.task_queue = tq;
    worker_opts.workflows.push_back(desc_def);

    with_worker(worker_opts, [&]() {
        client::WorkflowOptions opts;
        opts.id = "describe-wf-" + tq;
        opts.task_queue = tq;

        auto handle = run_task_sync(
            client()->start_workflow("Describable", opts,
                                    std::string("test-data")));

        // Wait for the workflow to complete.
        auto result = run_task_sync(handle.get_result<std::string>());
        EXPECT_EQ(result, "result:test-data");

        // Describe the completed workflow.
        auto desc = run_task_sync(handle.describe());
        EXPECT_EQ(desc.workflow_id, "describe-wf-" + tq);
        EXPECT_EQ(desc.workflow_type, "Describable");
        EXPECT_FALSE(desc.run_id.empty());
        EXPECT_EQ(desc.task_queue, tq);
    });
}

// P1-9: Activity throws, workflow receives proper exception info.
TEST_F(IntegrationTest, ErrorPropagation) {
    auto tq = unique_task_queue();

    auto fail_act = activities::ActivityDefinition::create(
        "failing_activity", &failing_activity);

    auto err_def = wf::WorkflowDefinition::create<ErrorWorkflow>(
        "ErrorWorkflow")
        .run(&ErrorWorkflow::run)
        .build();

    worker::TemporalWorkerOptions worker_opts;
    worker_opts.task_queue = tq;
    worker_opts.workflows.push_back(err_def);
    worker_opts.activities.push_back(fail_act);

    with_worker(worker_opts, [&]() {
        client::WorkflowOptions opts;
        opts.id = "error-prop-" + tq;
        opts.task_queue = tq;

        auto handle = run_task_sync(
            client()->start_workflow("ErrorWorkflow", opts));

        auto result = run_task_sync(handle.get_result<std::string>());
        // The workflow should have caught the activity failure.
        // The exact error message may be wrapped by Temporal, so we just
        // check that it starts with "caught:" indicating the catch block ran.
        EXPECT_TRUE(result.rfind("caught:", 0) == 0)
            << "Expected result to start with 'caught:', got: " << result;
    });
}

// P1-10: Cancel a running workflow; verify it terminates with cancellation.
TEST_F(IntegrationTest, WorkflowCancellation) {
    auto tq = unique_task_queue();

    auto cancel_def = wf::WorkflowDefinition::create<CancelableWorkflow>(
        "Cancelable")
        .run(&CancelableWorkflow::run)
        .build();

    worker::TemporalWorkerOptions worker_opts;
    worker_opts.task_queue = tq;
    worker_opts.workflows.push_back(cancel_def);

    with_worker(worker_opts, [&]() {
        client::WorkflowOptions opts;
        opts.id = "cancel-wf-" + tq;
        opts.task_queue = tq;

        auto handle = run_task_sync(
            client()->start_workflow("Cancelable", opts));

        // Give the workflow a moment to start and block on wait_condition.
        std::this_thread::sleep_for(std::chrono::milliseconds(500));

        // Cancel the workflow.
        run_task_sync(handle.cancel());

        // Getting the result should throw because the workflow was cancelled.
        EXPECT_THROW(
            run_task_sync(handle.get_result<std::string>()),
            std::exception);
    });
}
