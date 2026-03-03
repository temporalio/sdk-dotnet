/// @file activity_worker/main.cpp
/// @brief Example: define activities, a workflow, and run a worker end-to-end.
///
/// This example demonstrates:
///   1. Defining multiple activities using ActivityDefinition::create().
///   2. Defining a workflow that calls activities via Workflow::execute_activity().
///   3. Configuring a TemporalWorker with activities and workflows.
///   4. Starting a workflow and getting the result.
///   5. Shutting down the worker cleanly.
///
/// Requires a running Temporal server at localhost:7233.

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

// -- Activity definitions --

// A simple greeting activity that returns a formatted string.
temporalio::coro::Task<std::string> greet(std::string name) {
    std::cout << "  [activity] greet(\"" << name << "\") executing\n";
    co_return "Hello, " + name + "!";
}

// A no-arg activity that returns a fixed identifier string.
temporalio::coro::Task<std::string> get_worker_info() {
    std::cout << "  [activity] get_worker_info() executing\n";
    co_return "activity-worker-example-v1";
}

// -- Workflow definition --

// A workflow that calls both activities and combines their results.
class GreetingWorkflow {
public:
    temporalio::coro::Task<std::string> run(std::string name) {
        namespace wf = temporalio::workflows;
        wf::ActivityOptions opts;
        opts.start_to_close_timeout = std::chrono::seconds(30);

        // Call the greet activity.
        auto greeting = co_await wf::Workflow::execute_activity<std::string>(
            "greet", opts, name);

        // Call the get_worker_info activity.
        auto info = co_await wf::Workflow::execute_activity<std::string>(
            "get_worker_info", opts);

        co_return greeting + " (worker: " + info + ")";
    }
};

int main() {
    std::cout << "Temporal C++ SDK v" << temporalio::version() << "\n";
    std::cout << "Activity Worker example\n\n";

    namespace client = temporalio::client;
    namespace worker = temporalio::worker;

    try {
        // Step 1: Connect to Temporal.
        auto tc = run_task_sync(client::TemporalClient::connect(
            client::TemporalClientConnectOptions{
                .connection = {.target_host = "localhost:7233"},
                .client = {.ns = "default"},
            }));

        std::cout << "Connected to Temporal server.\n";

        // Step 2: Build activity definitions.
        auto greet_activity =
            temporalio::activities::ActivityDefinition::create("greet", &greet);
        auto info_activity =
            temporalio::activities::ActivityDefinition::create(
                "get_worker_info", &get_worker_info);

        // Step 3: Build workflow definition.
        auto greeting_workflow =
            temporalio::workflows::WorkflowDefinition::create<GreetingWorkflow>(
                "GreetingWorkflow")
                .run(&GreetingWorkflow::run)
                .build();

        // Step 4: Configure the worker.
        worker::TemporalWorkerOptions opts;
        opts.task_queue = "activity-example-queue";
        opts.activities.push_back(greet_activity);
        opts.activities.push_back(info_activity);
        opts.workflows.push_back(greeting_workflow);
        opts.max_concurrent_activities = 10;
        opts.max_concurrent_workflow_tasks = 10;

        std::cout << "Starting worker on task queue: " << opts.task_queue << "\n"
                  << "  Activities: " << opts.activities.size() << "\n"
                  << "  Workflows:  " << opts.workflows.size() << "\n";

        // Step 5: Create and run the worker in a background thread.
        worker::TemporalWorker w(tc, opts);

        std::stop_source worker_stop;
        std::jthread worker_thread([&w, token = worker_stop.get_token()]() {
            try {
                run_task_sync(w.execute_async(token));
            } catch (const std::exception& e) {
                std::cerr << "Worker error: " << e.what() << "\n";
            }
        });

        // Step 6: Start a workflow execution.
        client::WorkflowOptions wf_opts;
        wf_opts.id = "activity-example-workflow";
        wf_opts.task_queue = "activity-example-queue";

        auto handle = run_task_sync(tc->start_workflow(
            "GreetingWorkflow",
            wf_opts,
            std::string("Temporal")));

        std::cout << "Started workflow: " << handle.id()
                  << " (run " << handle.run_id().value_or("") << ")\n";

        // Step 7: Wait for the workflow result.
        auto result = run_task_sync(handle.get_result<std::string>());
        std::cout << "Workflow result: " << result << "\n";

        // Step 8: Shut down the worker.
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
