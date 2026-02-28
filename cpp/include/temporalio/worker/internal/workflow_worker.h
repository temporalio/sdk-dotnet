#pragma once

/// @file workflow_worker.h
/// @brief Internal workflow task poller and dispatcher.

#include <chrono>
#include <memory>
#include <string>
#include <unordered_map>
#include <vector>

#include <temporalio/coro/task.h>
#include <temporalio/coro/task_completion_source.h>
#include <temporalio/worker/workflow_instance.h>
#include <temporalio/workflows/workflow_definition.h>

namespace coresdk::workflow_activation {
class InitializeWorkflow;
}

namespace temporalio::bridge {
class Worker;
}

namespace temporalio::converters {
struct DataConverter;
}

namespace temporalio::worker::interceptors {
class IWorkerInterceptor;
}

namespace temporalio::worker::internal {

/// Configuration for the internal WorkflowWorker.
struct WorkflowWorkerOptions {
    /// Bridge worker for FFI calls.
    bridge::Worker* bridge_worker{nullptr};

    /// Task queue being polled.
    std::string task_queue;

    /// Namespace.
    std::string ns;

    /// Registered workflow definitions by name.
    std::unordered_map<std::string,
                       std::shared_ptr<workflows::WorkflowDefinition>>
        workflows;

    /// Dynamic workflow definition (if any).
    std::shared_ptr<workflows::WorkflowDefinition> dynamic_workflow;

    /// Data converter.
    std::shared_ptr<converters::DataConverter> data_converter;

    /// Worker interceptors.
    std::vector<std::shared_ptr<interceptors::IWorkerInterceptor>>
        interceptors;

    /// Whether to run in debug mode (disables deadlock detection).
    bool debug_mode{false};
};

/// Internal workflow worker that polls workflow activations from the
/// bridge and dispatches them to WorkflowInstance objects.
///
/// This mirrors the C# internal WorkflowWorker class.
class WorkflowWorker {
public:
    explicit WorkflowWorker(WorkflowWorkerOptions options);
    ~WorkflowWorker();

    // Non-copyable
    WorkflowWorker(const WorkflowWorker&) = delete;
    WorkflowWorker& operator=(const WorkflowWorker&) = delete;

    /// Run the poll loop until the bridge signals shutdown.
    /// @return Task that completes when polling stops.
    coro::Task<void> execute_async();

private:
    /// Handle a single workflow activation (poll result).
    /// Looks up or creates the WorkflowInstance, dispatches, and sends
    /// the completion to the bridge.
    coro::Task<void> handle_activation(
        const std::vector<uint8_t>& activation_bytes);

    /// Handle a cache eviction job and send the completion.
    coro::Task<void> handle_cache_eviction(const std::string& run_id,
                                              const std::string& message);

    /// Create a new workflow instance for the given workflow type (basic).
    std::unique_ptr<WorkflowInstance> create_instance(
        const std::string& workflow_type, const std::string& run_id);

    /// Create a new workflow instance with full info from InitializeWorkflow.
    std::unique_ptr<WorkflowInstance> create_instance(
        const std::string& workflow_type, const std::string& run_id,
        const coresdk::workflow_activation::InitializeWorkflow& init);

    /// Poll the bridge for a workflow activation and return the result.
    /// Returns nullopt on shutdown (no error, no data).
    coro::Task<std::optional<std::vector<uint8_t>>> poll_activation();

    /// Complete a workflow activation via the bridge.
    coro::Task<void> complete_activation(
        const std::vector<uint8_t>& completion_bytes);

    WorkflowWorkerOptions options_;

    /// Running workflow instances keyed by run ID.
    std::unordered_map<std::string, std::unique_ptr<WorkflowInstance>>
        running_workflows_;

    /// Deadlock timeout for workflow activations.
    std::chrono::seconds deadlock_timeout_{2};
};

}  // namespace temporalio::worker::internal

