/// @file workflow_activity/main.cpp
/// @brief Example: workflow calls an activity and returns the result.
///
/// This example demonstrates the complete Temporal lifecycle:
///   1. Define an activity (greet) that takes a name and returns a greeting.
///   2. Define a workflow that calls Workflow::execute_activity().
///   3. Connect a client, create a worker, start the workflow, get the result.
///
/// Mirrors the C# SimpleBench pattern. Requires a running Temporal server
/// at localhost:7233 (e.g., via `temporal server start-dev`).

#include <temporalio/activities/activity.h>
#include <temporalio/coro/run_sync.h>
#include <temporalio/coro/task.h>
#include <temporalio/client/temporal_client.h>
#include <temporalio/client/workflow_options.h>
#include <temporalio/runtime/temporal_runtime.h>
#include <temporalio/version.h>
#include <temporalio/worker/temporal_worker.h>
#include <temporalio/workflows/activity_options.h>
#include <temporalio/workflows/workflow.h>
#include <temporalio/workflows/workflow_definition.h>

#include <chrono>
#include <exception>
#include <iostream>
#include <stop_token>
#include <string>
#include <thread>

using temporalio::coro::run_task_sync;

// -- Activity definition --

// A simple greeting activity: takes a name, returns "Hello, <name>!".
temporalio::coro::Task<std::string> greet(std::string name) {
    std::cout << "  [activity] greet(\"" << name << "\") executing\n";
    co_return "Hello, " + name + "!";
}

// -- Workflow definition --

// A workflow that calls the greet activity and returns its result.
class GreetingWorkflow {
public:
    temporalio::coro::Task<std::string> run(std::string name) {
        // Call the activity with a 30-second start-to-close timeout.
        namespace wf = temporalio::workflows;
        wf::ActivityOptions opts;
        opts.start_to_close_timeout = std::chrono::seconds(30);

        auto result = co_await wf::Workflow::execute_activity<std::string>(
            "greet", opts, name);

        co_return result;
    }
};

int main() {
    std::cout << "Temporal C++ SDK v" << temporalio::version() << "\n";
    std::cout << "Workflow Activity example\n\n";

    namespace client = temporalio::client;
    namespace worker = temporalio::worker;

    try {
        // Step 1: Connect to Temporal.
        // Each run_task_sync call drives a coroutine to completion, blocking
        // the main thread. Between calls, the main thread is free.
        auto tc = run_task_sync(client::TemporalClient::connect(
            client::TemporalClientConnectOptions{
                .connection = {.target_host = "localhost:7233"},
                .client = {.ns = "default"},
            }));

        std::cout << "Connected to Temporal server.\n";

        // Step 2: Build activity and workflow definitions.
        auto greet_activity =
            temporalio::activities::ActivityDefinition::create("greet", &greet);

        auto greeting_workflow =
            temporalio::workflows::WorkflowDefinition::create<GreetingWorkflow>(
                "GreetingWorkflow")
                .run(&GreetingWorkflow::run)
                .build();

        // Step 3: Configure and start the worker on a background thread.
        worker::TemporalWorkerOptions worker_opts;
        worker_opts.task_queue = "workflow-activity-queue";
        worker_opts.activities.push_back(greet_activity);
        worker_opts.workflows.push_back(greeting_workflow);

        std::stop_source worker_stop;
        worker::TemporalWorker w(tc, worker_opts);

        std::jthread worker_thread([&w, token = worker_stop.get_token()]() {
            std::cout << "Worker started on task queue: workflow-activity-queue\n";
            try {
                run_task_sync(w.execute_async(token));
            } catch (const std::exception& e) {
                std::cerr << "Worker error: " << e.what() << "\n";
            }
        });

        // Step 4: Start a workflow execution.
        client::WorkflowOptions wf_opts;
        wf_opts.id = "greeting-workflow-id";
        wf_opts.task_queue = "workflow-activity-queue";

        auto handle = run_task_sync(tc->start_workflow(
            "GreetingWorkflow",
            wf_opts,
            std::string("Temporal")));  // typed argument

        std::cout << "Started workflow: " << handle.id()
                  << " (run " << handle.run_id().value_or("") << ")\n";

        // Step 5: Wait for the workflow result.
        auto result = run_task_sync(handle.get_result<std::string>());
        std::cout << "Workflow result: " << result << "\n";

        // Step 6: Shut down the worker.
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

    // Explicitly tear down the Rust runtime before process exit to avoid
    // races between static destruction and glibc TLS cleanup on Linux.
    temporalio::runtime::TemporalRuntime::reset_default();

    return 0;
}
