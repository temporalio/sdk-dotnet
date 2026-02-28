/// @file signal_workflow/main.cpp
/// @brief Example: start a workflow and send it a signal.
///
/// This example demonstrates:
///   1. Defining a workflow class with a signal handler.
///   2. Building a WorkflowDefinition using the builder API.
///   3. Connecting to Temporal and starting a workflow.
///   4. Sending a signal to the running workflow.
///   5. Retrieving the final result.
///
/// Requires a running Temporal server at localhost:7233.

#include <temporalio/coro/run_sync.h>
#include <temporalio/coro/task.h>
#include <temporalio/client/temporal_client.h>
#include <temporalio/client/workflow_options.h>
#include <temporalio/version.h>
#include <temporalio/workflows/workflow.h>
#include <temporalio/workflows/workflow_definition.h>

#include <coroutine>
#include <exception>
#include <iostream>
#include <string>

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

// The async entry point.
temporalio::coro::Task<void> run() {
    namespace client = temporalio::client;

    // Step 1: Connect to Temporal.
    auto tc = co_await client::TemporalClient::connect(
        client::TemporalClientConnectOptions{
            .connection = {.target_host = "localhost:7233"},
        });

    // Step 2: Start the Accumulator workflow.
    client::WorkflowOptions opts;
    opts.id = "signal-example-workflow";
    opts.task_queue = "signal-example-queue";

    auto handle = co_await tc->start_workflow("Accumulator", "{}", opts);
    std::cout << "Started workflow: " << handle.id() << "\n";

    // Step 3: Send signals with messages.
    co_await handle.signal("add_message", "\"hello\"");
    std::cout << "Sent signal: hello\n";

    co_await handle.signal("add_message", "\"world\"");
    std::cout << "Sent signal: world\n";

    // Step 4: Send the "done" signal to complete the workflow.
    co_await handle.signal("add_message", "\"done\"");
    std::cout << "Sent signal: done\n";

    // Step 5: Get the final result.
    auto result = co_await handle.get_result();
    std::cout << "Workflow result: " << result << "\n";
}

int main() {
    std::cout << "Temporal C++ SDK v" << temporalio::version() << "\n";
    std::cout << "Signal Workflow example\n\n";

    // Show the workflow definition (local validation).
    auto def = make_accumulator_definition();
    std::cout << "Registered workflow: " << def->name()
              << " (signals: " << def->signals().size()
              << ", queries: " << def->queries().size() << ")\n\n";

    try {
        run_task_sync(run());
    } catch (const std::exception& e) {
        std::cerr << "Error: " << e.what() << "\n";
        return 1;
    }
    return 0;
}
