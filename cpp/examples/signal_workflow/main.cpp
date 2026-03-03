/// @file signal_workflow/main.cpp
/// @brief Example: start a workflow and send it signals.
///
/// This example demonstrates:
///   1. Defining a workflow class with signal and query handlers.
///   2. Building a WorkflowDefinition using the builder API.
///   3. Starting a worker, connecting to Temporal, and starting a workflow.
///   4. Sending signals to the running workflow.
///   5. Retrieving the final result after signaling "done".
///
/// Requires a running Temporal server at localhost:7233.

#include <temporalio/coro/run_sync.h>
#include <temporalio/coro/task.h>
#include <temporalio/client/temporal_client.h>
#include <temporalio/client/workflow_options.h>
#include <temporalio/runtime/temporal_runtime.h>
#include <temporalio/version.h>
#include <temporalio/worker/temporal_worker.h>
#include <temporalio/workflows/workflow.h>
#include <temporalio/workflows/workflow_definition.h>

#include <exception>
#include <iostream>
#include <stop_token>
#include <string>
#include <thread>

using temporalio::coro::run_task_sync;

// -- Workflow definition --
// A workflow that accumulates messages via signals and returns them
// when it receives a "done" signal.
class AccumulatorWorkflow {
public:
    // The main workflow run method. Waits until done_ is set, then
    // returns all accumulated messages as a single string.
    temporalio::coro::Task<std::string> run() {
        co_await temporalio::workflows::Workflow::wait_condition(
            [this]() { return done_; });
        co_return result_;
    }

    // Signal handler: appends a message. If the message is "done",
    // sets the done flag so the workflow completes.
    temporalio::coro::Task<void> add_message(std::string msg) {
        if (msg == "done") {
            done_ = true;
        } else {
            if (!result_.empty()) {
                result_ += ", ";
            }
            result_ += msg;
        }
        co_return;
    }

    // Query handler: returns the current accumulated result.
    std::string get_result() const { return result_; }

private:
    std::string result_;
    bool done_ = false;
};

// Build the workflow definition using the builder API.
std::shared_ptr<temporalio::workflows::WorkflowDefinition>
make_accumulator_definition() {
    return temporalio::workflows::WorkflowDefinition::create<AccumulatorWorkflow>(
               "Accumulator")
        .run(&AccumulatorWorkflow::run)
        .signal("add_message", &AccumulatorWorkflow::add_message)
        .query("get_result", &AccumulatorWorkflow::get_result)
        .build();
}

int main() {
    std::cout << "Temporal C++ SDK v" << temporalio::version() << "\n";
    std::cout << "Signal Workflow example\n\n";

    namespace client = temporalio::client;
    namespace worker = temporalio::worker;

    // Show the workflow definition (local validation).
    auto def = make_accumulator_definition();
    std::cout << "Registered workflow: " << def->name()
              << " (signals: " << def->signals().size()
              << ", queries: " << def->queries().size() << ")\n\n";

    try {
        // Step 1: Connect to Temporal.
        auto tc = run_task_sync(client::TemporalClient::connect(
            client::TemporalClientConnectOptions{
                .connection = {.target_host = "localhost:7233"},
                .client = {.ns = "default"},
            }));

        std::cout << "Connected to Temporal server.\n";

        // Step 2: Build the workflow definition and configure worker.
        auto accumulator_workflow = make_accumulator_definition();

        worker::TemporalWorkerOptions worker_opts;
        worker_opts.task_queue = "signal-example-queue";
        worker_opts.workflows.push_back(accumulator_workflow);

        std::stop_source worker_stop;
        worker::TemporalWorker w(tc, worker_opts);

        std::jthread worker_thread([&w, token = worker_stop.get_token()]() {
            try {
                run_task_sync(w.execute_async(token));
            } catch (const std::exception& e) {
                std::cerr << "Worker error: " << e.what() << "\n";
            }
        });

        std::cout << "Worker started on task queue: signal-example-queue\n";

        // Step 3: Start the Accumulator workflow.
        client::WorkflowOptions opts;
        opts.id = "signal-example-workflow";
        opts.task_queue = "signal-example-queue";

        auto handle = run_task_sync(tc->start_workflow("Accumulator", opts));
        std::cout << "Started workflow: " << handle.id() << "\n";

        // Step 4: Send signals with messages.
        run_task_sync(handle.signal("add_message", std::string("hello")));
        std::cout << "Sent signal: hello\n";

        run_task_sync(handle.signal("add_message", std::string("world")));
        std::cout << "Sent signal: world\n";

        // Step 5: Send the "done" signal to complete the workflow.
        run_task_sync(handle.signal("add_message", std::string("done")));
        std::cout << "Sent signal: done\n";

        // Step 6: Get the final result.
        auto result = run_task_sync(handle.get_result<std::string>());
        std::cout << "Workflow result: " << result << "\n";

        // Step 7: Shut down the worker.
        worker_stop.request_stop();
        worker_thread.join();
        std::cout << "Worker shut down.\n";

    } catch (const std::exception& e) {
        std::cerr << "Error: " << e.what() << "\n";
        return 1;
    }

    // Explicitly tear down the Rust runtime before process exit to avoid
    // races between static destruction and glibc TLS cleanup on Linux.
    temporalio::runtime::TemporalRuntime::reset_default();

    return 0;
}
