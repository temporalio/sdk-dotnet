#pragma once

/// @file TemporalWorker that polls task queues and dispatches to workflows/activities.

#include <temporalio/export.h>

#include <atomic>
#include <chrono>
#include <cstdint>
#include <memory>
#include <optional>
#include <stop_token>
#include <string>
#include <unordered_map>
#include <vector>

#include <temporalio/activities/activity.h>
#include <temporalio/coro/task.h>
#include <temporalio/workflows/workflow_definition.h>

namespace temporalio::bridge {
class Worker;
}

namespace temporalio::client {
class TemporalClient;
}

namespace temporalio::converters {
struct DataConverter;
}

namespace temporalio::nexus {
class NexusServiceDefinition;
}

namespace temporalio::worker {

// Forward declarations
namespace internal {
class WorkflowWorker;
class ActivityWorker;
class NexusWorker;
}  // namespace internal

// Forward declarations for interceptors
namespace interceptors {
class IWorkerInterceptor;
}

/// Configuration options for a TemporalWorker.
struct TemporalWorkerOptions {
    /// Task queue to poll from.
    std::string task_queue;

    /// Namespace to use. If empty, defaults to the client's namespace.
    std::string ns;

    /// Workflow definitions to register.
    std::vector<std::shared_ptr<workflows::WorkflowDefinition>> workflows;

    /// Activity definitions to register.
    std::vector<std::shared_ptr<activities::ActivityDefinition>> activities;

    /// Nexus service definitions to register (experimental).
    std::vector<std::shared_ptr<nexus::NexusServiceDefinition>> nexus_services;

    /// Worker interceptors.
    std::vector<std::shared_ptr<interceptors::IWorkerInterceptor>>
        interceptors;

    /// Data converter. If null, uses the default converter.
    std::shared_ptr<converters::DataConverter> data_converter;

    /// Maximum number of concurrent workflow task executions.
    uint32_t max_concurrent_workflow_tasks = 100;

    /// Maximum number of concurrent activity executions.
    uint32_t max_concurrent_activities = 100;

    /// Maximum number of concurrent local activity executions.
    uint32_t max_concurrent_local_activities = 100;

    /// Maximum number of concurrent Nexus tasks (experimental).
    uint32_t max_concurrent_nexus_tasks = 100;

    /// Maximum number of concurrent poll workflow task requests. Default 5.
    uint32_t max_concurrent_workflow_task_polls = 5;

    /// Maximum number of concurrent poll activity task requests. Default 5.
    uint32_t max_concurrent_activity_task_polls = 5;

    /// Maximum number of concurrent poll Nexus task requests. Default 5.
    uint32_t max_concurrent_nexus_task_polls = 5;

    /// Number of workflows cached for sticky task queue use. 0 disables
    /// sticky queues. Default 10000.
    uint32_t max_cached_workflows = 10000;

    /// How long a workflow task may sit on the sticky queue before
    /// timing out. Default 10s.
    std::chrono::milliseconds sticky_queue_schedule_to_start_timeout{10000};

    /// Amount of time after shutdown that activities are given to complete
    /// before cancellation. Default 0 (no grace period).
    std::chrono::milliseconds graceful_shutdown_timeout{0};

    /// Whether to disable eager activity dispatch.
    bool disable_eager_activity_dispatch = false;

    /// Whether this worker only handles workflows and local activities.
    bool local_activity_worker_only = false;

    /// Whether this worker opts into worker versioning (deprecated, use
    /// deployment options instead).
    bool use_worker_versioning = false;

    /// Build ID for versioning.
    std::string build_id;

    /// Identity string for this worker. If empty, defaults to client identity.
    std::string identity;

    /// Ratio of non-sticky to sticky pollers. Default 0.2.
    float non_sticky_to_sticky_poll_ratio = 0.2f;

    /// Maximum activities per second this worker will process.
    std::optional<double> max_activities_per_second;

    /// Maximum activities per second the task queue will dispatch (server).
    std::optional<double> max_task_queue_activities_per_second;

    /// Whether to enable debug mode (disables deadlock detection).
    bool debug_mode = false;

    /// Helper: add a workflow definition.
    template <typename T>
    TemporalWorkerOptions& add_workflow(
        std::shared_ptr<workflows::WorkflowDefinition> def) {
        workflows.push_back(std::move(def));
        return *this;
    }

    /// Helper: add an activity definition.
    TemporalWorkerOptions& add_activity(
        std::shared_ptr<activities::ActivityDefinition> def) {
        activities.push_back(std::move(def));
        return *this;
    }

    /// Helper: add a Nexus service definition (experimental).
    TemporalWorkerOptions& add_nexus_service(
        std::shared_ptr<nexus::NexusServiceDefinition> def) {
        nexus_services.push_back(std::move(def));
        return *this;
    }
};

/// Worker that polls a Temporal task queue and dispatches to
/// registered workflows and activities.
///
/// Usage:
///   TemporalWorkerOptions opts;
///   opts.task_queue = "my-queue";
///   opts.workflows.push_back(my_workflow_def);
///   opts.activities.push_back(my_activity_def);
///   TemporalWorker worker(client, opts);
///   co_await worker.execute_async(shutdown_token);
class TEMPORALIO_EXPORT TemporalWorker {
public:
    TemporalWorker(std::shared_ptr<client::TemporalClient> client,
                   TemporalWorkerOptions options);

    ~TemporalWorker();

    // Non-copyable, non-movable
    TemporalWorker(const TemporalWorker&) = delete;
    TemporalWorker& operator=(const TemporalWorker&) = delete;

    /// Run the worker until the shutdown token is triggered.
    /// This polls for workflow and activity tasks and dispatches them.
    coro::Task<void> execute_async(std::stop_token shutdown_token);

    /// Get the worker options.
    const TemporalWorkerOptions& options() const noexcept { return options_; }

    /// Get the bridge worker (for internal use).
    bridge::Worker* bridge_worker() const noexcept {
        return bridge_worker_.get();
    }

private:
    std::shared_ptr<client::TemporalClient> client_;
    TemporalWorkerOptions options_;

    // Bridge worker (owned)
    std::unique_ptr<bridge::Worker> bridge_worker_;

    // Internal workflow/activity name lookup maps built at construction
    std::unordered_map<std::string,
                       std::shared_ptr<workflows::WorkflowDefinition>>
        workflow_map_;
    std::unordered_map<std::string,
                       std::shared_ptr<activities::ActivityDefinition>>
        activity_map_;

    // Dynamic (unnamed) workflow definition, if any.
    std::shared_ptr<workflows::WorkflowDefinition> dynamic_workflow_;

    // Sub-workers (created during execute_async)
    std::unique_ptr<internal::WorkflowWorker> workflow_worker_;
    std::unique_ptr<internal::ActivityWorker> activity_worker_;
    std::unique_ptr<internal::NexusWorker> nexus_worker_;

    // Whether the worker has been started (prevents double-start)
    std::atomic<bool> started_{false};
};

}  // namespace temporalio::worker

