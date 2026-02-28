/// @file activity_worker/main.cpp
/// @brief Example: define activities and create a worker.
///
/// This example demonstrates:
///   1. Defining activities using ActivityDefinition::create().
///   2. Defining a workflow using the WorkflowDefinition builder.
///   3. Configuring a TemporalWorker with activities and workflows.
///   4. Running the worker until shutdown.
///
/// Requires a running Temporal server at localhost:7233.

#include <temporalio/activities/activity.h>
#include <temporalio/coro/run_sync.h>
#include <temporalio/coro/task.h>
#include <temporalio/client/temporal_client.h>
#include <temporalio/version.h>
#include <temporalio/worker/temporal_worker.h>
#include <temporalio/workflows/workflow_definition.h>

#include <coroutine>
#include <exception>
#include <iostream>
#include <stop_token>
#include <string>

using temporalio::coro::run_task_sync;

// -- Activity definitions --

// A simple greeting activity that returns a formatted string.
temporalio::coro::Task<std::string> greet(std::string name) {
    co_return "Hello, " + name + "!";
}

// A no-arg activity that returns a fixed identifier string.
temporalio::coro::Task<std::string> get_worker_info() {
    co_return "activity-worker-example";
}

// -- Workflow definition --

// A workflow that calls the greet activity (placeholder - the actual
// activity invocation goes through WorkflowOutboundInterceptor in a
// real worker; this demonstrates the definition pattern).
class GreetingWorkflow {
public:
    temporalio::coro::Task<std::string> run(std::string name) {
        // In a real workflow, you would use:
        //   co_return co_await Workflow::execute_activity("greet", name);
        // For this example, we just show the definition wiring.
        co_return "Would greet: " + name;
    }
};

// The async entry point.
temporalio::coro::Task<void> run(std::stop_token shutdown_token) {
    namespace client = temporalio::client;
    namespace worker = temporalio::worker;

    // Step 1: Connect to Temporal.
    auto tc = co_await client::TemporalClient::connect(
        client::TemporalClientConnectOptions{
            .connection = {.target_host = "localhost:7233"},
        });

    std::cout << "Connected to Temporal server.\n";

    // Step 2: Build activity definitions.
    auto greet_activity =
        temporalio::activities::ActivityDefinition::create("greet", &greet);

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
    opts.workflows.push_back(greeting_workflow);
    opts.max_concurrent_activities = 10;
    opts.max_concurrent_workflow_tasks = 10;

    std::cout << "Starting worker on task queue: " << opts.task_queue << "\n"
              << "  Activities: " << opts.activities.size() << "\n"
              << "  Workflows:  " << opts.workflows.size() << "\n";

    // Step 5: Create and run the worker.
    worker::TemporalWorker w(tc, opts);
    co_await w.execute_async(shutdown_token);
}

int main() {
    std::cout << "Temporal C++ SDK v" << temporalio::version() << "\n";
    std::cout << "Activity Worker example\n\n";

    // The stop_source allows graceful shutdown (e.g., on SIGINT).
    std::stop_source stop;

    try {
        run_task_sync(run(stop.get_token()));
    } catch (const std::exception& e) {
        std::cerr << "Error: " << e.what() << "\n";
        return 1;
    }

    std::cout << "Worker shut down.\n";
    return 0;
}
