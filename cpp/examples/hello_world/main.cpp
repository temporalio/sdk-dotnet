/// @file hello_world/main.cpp
/// @brief Minimal example: connect to Temporal, run a workflow, get the result.
///
/// This example demonstrates the basic Temporal lifecycle:
///   1. Connect to the Temporal server.
///   2. Define a simple workflow that returns a greeting.
///   3. Start a worker to execute the workflow.
///   4. Start the workflow and wait for the result.
///   5. Shut down cleanly.
///
/// Requires a running Temporal server at localhost:7233
/// (e.g., via `temporal server start-dev` or Docker).

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

// A simple greeting workflow that takes a name and returns "Hello, <name>!".
class GreetingWorkflow {
public:
    temporalio::coro::Task<std::string> run(std::string name) {
        co_return "Hello, " + name + "!";
    }
};

int main() {
    std::cout << "Temporal C++ SDK v" << temporalio::version() << "\n";
    std::cout << "Hello World example\n\n";

    namespace client = temporalio::client;
    namespace worker = temporalio::worker;

    try {
        // Step 1: Connect to the Temporal server.
        auto tc = run_task_sync(client::TemporalClient::connect(
            client::TemporalClientConnectOptions{
                .connection = {.target_host = "localhost:7233"},
                .client = {.ns = "default"},
            }));

        std::cout << "Connected to Temporal server.\n";

        // Step 2: Build the workflow definition.
        auto greeting_workflow =
            temporalio::workflows::WorkflowDefinition::create<GreetingWorkflow>(
                "Greeting")
                .run(&GreetingWorkflow::run)
                .build();

        // Step 3: Configure and start the worker on a background thread.
        worker::TemporalWorkerOptions worker_opts;
        worker_opts.task_queue = "hello-world-task-queue";
        worker_opts.workflows.push_back(greeting_workflow);

        std::stop_source worker_stop;
        worker::TemporalWorker w(tc, worker_opts);

        std::jthread worker_thread([&w, token = worker_stop.get_token()]() {
            try {
                run_task_sync(w.execute_async(token));
            } catch (const std::exception& e) {
                std::cerr << "Worker error: " << e.what() << "\n";
            }
        });

        std::cout << "Worker started on task queue: hello-world-task-queue\n";

        // Step 4: Start a workflow execution.
        client::WorkflowOptions opts;
        opts.id = "hello-world-workflow-id";
        opts.task_queue = "hello-world-task-queue";

        auto handle = run_task_sync(tc->start_workflow(
            "Greeting",
            opts,
            std::string("World")));

        std::cout << "Started workflow: " << handle.id()
                  << " (run " << handle.run_id().value_or("") << ")\n";

        // Step 5: Wait for the workflow result.
        auto result = run_task_sync(handle.get_result<std::string>());
        std::cout << "Workflow result: " << result << "\n";

        // Step 6: Shut down the worker.
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
