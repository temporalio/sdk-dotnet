#pragma once

/// @file nexus_worker.h
/// @brief Internal Nexus task poller and dispatcher.
/// WARNING: Nexus support is experimental.

#include <cstdint>
#include <memory>
#include <mutex>
#include <stop_token>
#include <string>
#include <thread>
#include <unordered_map>
#include <vector>

#include <temporalio/coro/task.h>
#include <temporalio/coro/task_completion_source.h>
#include <temporalio/nexus/operation_handler.h>

namespace temporalio::bridge {
class Worker;
}

namespace temporalio::nexus {
class NexusServiceDefinition;
}

namespace temporalio::client {
class TemporalClient;
}

namespace temporalio::worker::internal {

/// Configuration for the internal NexusWorker.
struct NexusWorkerOptions {
    /// Bridge worker for FFI calls.
    bridge::Worker* bridge_worker{nullptr};

    /// Task queue being polled.
    std::string task_queue;

    /// Namespace.
    std::string ns;

    /// Registered Nexus service definitions.
    std::vector<std::shared_ptr<nexus::NexusServiceDefinition>> services;

    /// Temporal client for operation handlers.
    std::shared_ptr<client::TemporalClient> client;
};

/// State of a running Nexus task.
struct RunningNexusTask {
    /// Cancellation source for this task.
    std::stop_source cancel_source;
    /// Task token for bridge completion.
    std::vector<uint8_t> task_token;
    /// Thread running this task. Stored to allow joining on shutdown,
    /// preventing use-after-free if the worker is destroyed while the
    /// thread is still running.
    std::jthread thread;
};

/// Internal Nexus worker that polls Nexus tasks from the bridge
/// and dispatches them to registered Nexus operation handlers.
///
/// This mirrors the C# internal NexusWorker class.
/// WARNING: Nexus support is experimental.
class NexusWorker {
public:
    explicit NexusWorker(NexusWorkerOptions options);
    ~NexusWorker();

    // Non-copyable
    NexusWorker(const NexusWorker&) = delete;
    NexusWorker& operator=(const NexusWorker&) = delete;

    /// Run the poll loop until the bridge signals shutdown.
    /// @return Task that completes when polling stops.
    coro::Task<void> execute_async();

private:
    /// Poll for the next Nexus task from the bridge.
    coro::Task<std::optional<std::vector<uint8_t>>> poll_nexus_task();

    /// Complete a Nexus task via the bridge.
    coro::Task<void> complete_nexus_task(
        const std::vector<uint8_t>& completion_bytes);

    /// Handle a Nexus start operation task.
    /// @param handler Pre-resolved operation handler (must not be null).
    /// @param start_ctx Pre-populated start context with headers, links, etc.
    void handle_start_operation(const std::vector<uint8_t>& task_token,
                                nexus::INexusOperationHandler* handler,
                                const std::vector<uint8_t>& input,
                                nexus::OperationStartContext start_ctx);

    /// Handle a Nexus cancel task.
    void handle_cancel_task(const std::vector<uint8_t>& task_token);

    /// Find a service by name.
    nexus::NexusServiceDefinition* find_service(
        const std::string& name) const;

    NexusWorkerOptions options_;

    /// Services indexed by name for O(1) lookup.
    std::unordered_map<std::string,
                       std::shared_ptr<nexus::NexusServiceDefinition>>
        services_by_name_;

    /// Running Nexus tasks keyed by task token (serialized as string).
    std::unordered_map<std::string, std::shared_ptr<RunningNexusTask>>
        running_tasks_;
    std::mutex running_tasks_mutex_;
};

}  // namespace temporalio::worker::internal

