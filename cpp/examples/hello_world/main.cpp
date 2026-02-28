/// @file hello_world/main.cpp
/// @brief Minimal example: connect to Temporal and start a workflow.
///
/// This example demonstrates the basic client connection and workflow
/// start/result pattern. It requires a running Temporal server at
/// localhost:7233 (e.g., via `temporal server start-dev`).
///
/// Flow:
///   1. Connect to the Temporal server.
///   2. Start a "Greeting" workflow with a string argument.
///   3. Wait for the result using the returned workflow handle.

#include <temporalio/coro/run_sync.h>
#include <temporalio/client/temporal_client.h>
#include <temporalio/client/temporal_connection.h>
#include <temporalio/client/workflow_options.h>
#include <temporalio/version.h>

#include <coroutine>
#include <exception>
#include <iostream>
#include <string>

using temporalio::coro::run_task_sync;

// The async entry point showing the SDK usage pattern.
temporalio::coro::Task<void> run() {
    namespace client = temporalio::client;

    // Step 1: Connect to the Temporal server.
    auto tc = co_await client::TemporalClient::connect(
        client::TemporalClientConnectOptions{
            .connection = {.target_host = "localhost:7233"},
            .client = {.ns = "default"},
        });

    // Step 2: Start a workflow execution.
    client::WorkflowOptions opts;
    opts.id = "hello-world-workflow-id";
    opts.task_queue = "hello-world-task-queue";

    auto handle = co_await tc->start_workflow(
        "Greeting",   // workflow type name
        "\"World\"",  // JSON-encoded argument
        opts);

    std::cout << "Started workflow: " << handle.id()
              << " (run " << handle.run_id().value_or("") << ")\n";

    // Step 3: Wait for the workflow result.
    auto result = co_await handle.get_result();
    std::cout << "Workflow result: " << result << "\n";
}

int main() {
    std::cout << "Temporal C++ SDK v" << temporalio::version() << "\n";
    std::cout << "Hello World example\n\n";

    try {
        run_task_sync(run());
    } catch (const std::exception& e) {
        std::cerr << "Error: " << e.what() << "\n";
        return 1;
    }
    return 0;
}
