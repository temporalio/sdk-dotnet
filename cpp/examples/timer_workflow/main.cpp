/// @file timer_workflow/main.cpp
/// @brief Example: timers, conditions, and deterministic time.
///
/// This example demonstrates:
///   1. Using Workflow::delay() for deterministic timers.
///   2. Using Workflow::wait_condition() with a timeout fallback.
///   3. Using Workflow::utc_now() for deterministic timestamps.
///   4. A signal handler that unblocks a waiting condition.
///   5. Running a worker in the background and driving the workflow
///      from the client side.
///
/// Requires a running Temporal server at localhost:7233.

#include <temporalio/coro/run_sync.h>
#include <temporalio/coro/task.h>
#include <temporalio/client/temporal_client.h>
#include <temporalio/client/workflow_options.h>
#include <temporalio/version.h>
#include <temporalio/worker/temporal_worker.h>
#include <temporalio/workflows/workflow.h>
#include <temporalio/workflows/workflow_definition.h>

#include <chrono>
#include <exception>
#include <iostream>
#include <string>
#include <stop_token>
#include <thread>

using temporalio::coro::run_task_sync;

// ---------------------------------------------------------------------------
// Workflow definition
// ---------------------------------------------------------------------------

/// A workflow that demonstrates timers and conditions.
///
/// The workflow:
///   1. Records the start time using Workflow::utc_now().
///   2. Sleeps for 2 seconds using Workflow::delay().
///   3. Waits up to 10 seconds for a signal to set the approval flag.
///   4. If the signal arrives in time, returns "approved"; otherwise
///      returns "timed-out" as a fallback.
class TimerWorkflow {
public:
    /// Main workflow entry point.
    temporalio::coro::Task<std::string> run() {
        using namespace std::chrono_literals;
        namespace wf = temporalio::workflows;

        // Record the deterministic start time.
        auto start = wf::Workflow::utc_now();
        auto start_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            start.time_since_epoch()).count();

        // Step 1: Deterministic timer -- sleep for 2 seconds.
        co_await wf::Workflow::delay(2000ms);

        auto after_delay = wf::Workflow::utc_now();
        auto elapsed_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            after_delay - start).count();

        // Step 2: Wait for approval signal with a 10-second timeout.
        // wait_condition returns true if the predicate became true,
        // false if the timeout expired first.
        bool got_approval = co_await wf::Workflow::wait_condition(
            [this]() { return approved_; },
            10000ms);

        // Build the result string.
        std::string result;
        if (got_approval) {
            result = "approved after " + std::to_string(elapsed_ms)
                     + "ms delay (start_epoch_ms=" + std::to_string(start_ms)
                     + ", reason=" + approval_reason_ + ")";
        } else {
            result = "timed-out after " + std::to_string(elapsed_ms)
                     + "ms delay (start_epoch_ms=" + std::to_string(start_ms)
                     + ")";
        }

        co_return result;
    }

    /// Signal handler: approve the workflow with a reason string.
    temporalio::coro::Task<void> approve(std::string reason) {
        approval_reason_ = std::move(reason);
        approved_ = true;
        co_return;
    }

    /// Query handler: check whether the workflow has been approved.
    std::string status() const {
        return approved_ ? "approved" : "waiting";
    }

private:
    bool approved_ = false;
    std::string approval_reason_;
};

/// Build the TimerWorkflow definition.
std::shared_ptr<temporalio::workflows::WorkflowDefinition>
make_timer_definition() {
    return temporalio::workflows::WorkflowDefinition::create<TimerWorkflow>(
               "TimerWorkflow")
        .run(&TimerWorkflow::run)
        .signal("approve", &TimerWorkflow::approve)
        .query("status", &TimerWorkflow::status)
        .build();
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

int main() {
    std::cout << "Temporal C++ SDK v" << temporalio::version() << "\n";
    std::cout << "Timer Workflow example\n\n";

    namespace client = temporalio::client;
    namespace worker = temporalio::worker;

    try {
        // Step 1: Connect to Temporal.
        // Each run_task_sync call drives a coroutine to completion, blocking
        // the main thread. Between calls, the main thread is free.
        auto tc = run_task_sync(client::TemporalClient::connect(
            client::TemporalClientConnectOptions{
                .connection = {.target_host = "localhost:7233"},
            }));
        std::cout << "Connected to Temporal server.\n";

        // Step 2: Build the workflow definition.
        auto timer_workflow = make_timer_definition();

        // Step 3: Configure and create the worker.
        worker::TemporalWorkerOptions opts;
        opts.task_queue = "timer-example-queue";
        opts.workflows.push_back(timer_workflow);
        opts.max_concurrent_workflow_tasks = 10;

        std::cout << "Starting worker on task queue: " << opts.task_queue << "\n";

        worker::TemporalWorker w(tc, opts);

        // Step 4: Run the worker in a background thread.
        std::stop_source worker_stop;
        std::jthread worker_thread([&w, token = worker_stop.get_token()]() {
            try {
                run_task_sync(w.execute_async(token));
            } catch (const std::exception& e) {
                std::cerr << "Worker error: " << e.what() << "\n";
            }
        });

        // Step 5: Start the TimerWorkflow.
        client::WorkflowOptions wo;
        wo.id = "timer-example-workflow";
        wo.task_queue = "timer-example-queue";

        auto handle = run_task_sync(tc->start_workflow("TimerWorkflow", wo));
        std::cout << "Started workflow: " << handle.id()
                  << " (run " << handle.run_id().value_or("") << ")\n";

        // Step 6: Query the status (should be "waiting").
        auto status = run_task_sync(handle.query<std::string>("status"));
        std::cout << "Workflow status: " << status << "\n";

        // Step 7: Send the approval signal after a short pause.
        std::cout << "Sending approval signal...\n";
        run_task_sync(handle.signal("approve", std::string("manager-override")));
        std::cout << "Signal sent.\n";

        // Step 8: Get the final result.
        auto result = run_task_sync(handle.get_result<std::string>());
        std::cout << "Workflow result: " << result << "\n";

        // Step 9: Shut down the worker.
        // IMPORTANT: request_stop and join are called on the main thread
        // (not on a Rust callback thread), so they don't starve the tokio
        // runtime that the bridge uses for poll cancellation callbacks.
        worker_stop.request_stop();
        worker_thread.join();
        std::cout << "Worker shut down.\n";

    } catch (const std::exception& e) {
        std::cerr << "Error: " << e.what() << "\n";
        return 1;
    }
    return 0;
}
