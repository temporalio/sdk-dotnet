/// @file update_workflow/main.cpp
/// @brief Example: shopping cart workflow with update handlers and validators.
///
/// This example demonstrates:
///   1. Defining a workflow with an update handler and validator.
///   2. Registering query, signal, and update handlers together.
///   3. Using Workflow::wait_condition() for graceful handler draining.
///   4. Connecting to Temporal and interacting with the workflow.
///
/// The shopping cart workflow accepts item updates (with validation),
/// supports querying the current item count, and signals for checkout.
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
#include <stdexcept>
#include <stop_token>
#include <string>
#include <thread>
#include <vector>

using temporalio::coro::run_task_sync;

// -- Workflow definition --
// A shopping cart workflow that demonstrates update handlers with validators,
// query handlers, signal handlers, and graceful handler draining.
class ShoppingCartWorkflow {
public:
    // The main workflow run method. Waits for checkout signal, then
    // waits for all handlers to finish before returning the final items.
    temporalio::coro::Task<std::string> run() {
        // Wait until checkout is signaled.
        co_await temporalio::workflows::Workflow::wait_condition(
            [this]() { return checked_out_; });

        // Wait for all update/signal handlers to finish before completing.
        co_await temporalio::workflows::Workflow::wait_condition(
            []() { return temporalio::workflows::Workflow::all_handlers_finished(); });

        // Build the result: comma-separated list of items.
        std::string result;
        for (size_t i = 0; i < items_.size(); ++i) {
            if (i > 0) {
                result += ", ";
            }
            result += items_[i];
        }
        co_return result;
    }

    // Update validator: rejects empty item names and items added after
    // checkout. Throwing here causes the update to be rejected without
    // executing the handler.
    void validate_add_item(std::string item) {
        if (item.empty()) {
            throw std::invalid_argument("Item name cannot be empty");
        }
        if (checked_out_) {
            throw std::invalid_argument(
                "Cannot add items after checkout");
        }
    }

    // Update handler: adds an item to the cart and returns the new count.
    // Only called if the validator passes.
    temporalio::coro::Task<int> add_item(std::string item) {
        items_.push_back(std::move(item));
        co_return static_cast<int>(items_.size());
    }

    // Query handler: returns the current number of items in the cart.
    int get_item_count() const { return static_cast<int>(items_.size()); }

    // Signal handler: triggers checkout.
    temporalio::coro::Task<void> checkout() {
        checked_out_ = true;
        co_return;
    }

private:
    std::vector<std::string> items_;
    bool checked_out_ = false;
};

// Build the workflow definition using the builder API.
std::shared_ptr<temporalio::workflows::WorkflowDefinition>
make_shopping_cart_definition() {
    return temporalio::workflows::WorkflowDefinition::create<ShoppingCartWorkflow>(
               "ShoppingCart")
        .run(&ShoppingCartWorkflow::run)
        .update("add_item",
                &ShoppingCartWorkflow::add_item,
                &ShoppingCartWorkflow::validate_add_item)
        .query("get_item_count", &ShoppingCartWorkflow::get_item_count)
        .signal("checkout", &ShoppingCartWorkflow::checkout)
        .build();
}

int main() {
    std::cout << "Temporal C++ SDK v" << temporalio::version() << "\n";
    std::cout << "Update Workflow example\n\n";

    // Show the workflow definition (local validation).
    auto def = make_shopping_cart_definition();
    std::cout << "Registered workflow: " << def->name()
              << " (updates: " << def->updates().size()
              << ", queries: " << def->queries().size()
              << ", signals: " << def->signals().size() << ")\n\n";

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
        auto cart_workflow = make_shopping_cart_definition();

        // Step 3: Configure and create the worker.
        worker::TemporalWorkerOptions opts;
        opts.task_queue = "update-example-queue";
        opts.workflows.push_back(cart_workflow);
        opts.max_concurrent_workflow_tasks = 10;

        std::cout << "Starting worker on task queue: " << opts.task_queue << "\n"
                  << "  Workflows: " << opts.workflows.size() << "\n"
                  << "  Updates:   " << cart_workflow->updates().size() << "\n"
                  << "  Queries:   " << cart_workflow->queries().size() << "\n"
                  << "  Signals:   " << cart_workflow->signals().size() << "\n";

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

        // Step 5: Start the workflow.
        client::WorkflowOptions wf_opts;
        wf_opts.id = "shopping-cart-workflow";
        wf_opts.task_queue = "update-example-queue";

        auto handle = run_task_sync(
            tc->start_workflow("ShoppingCart", wf_opts));
        std::cout << "\nStarted workflow: " << handle.id() << "\n";

        // Step 6: Send updates to add items via WorkflowHandle::update<T>().
        auto count1 = run_task_sync(
            handle.update<int>("add_item", std::string("apple")));
        std::cout << "Added apple, item count: " << count1 << "\n";

        auto count2 = run_task_sync(
            handle.update<int>("add_item", std::string("banana")));
        std::cout << "Added banana, item count: " << count2 << "\n";

        // Step 7: Query the item count to verify.
        auto count = run_task_sync(
            handle.query<int>("get_item_count"));
        std::cout << "Queried item count: " << count << "\n";

        // Step 8: Signal checkout to complete the workflow.
        run_task_sync(handle.signal("checkout"));
        std::cout << "Sent checkout signal.\n";

        // Step 9: Get the final result.
        auto result = run_task_sync(handle.get_result<std::string>());
        std::cout << "Workflow result: " << result << "\n";

        // Step 10: Shut down the worker.
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
