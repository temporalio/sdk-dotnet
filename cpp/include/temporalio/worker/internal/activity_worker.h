#pragma once

/// @file activity_worker.h
/// @brief Internal activity task poller and dispatcher.

#include <any>
#include <atomic>
#include <chrono>
#include <functional>
#include <memory>
#include <mutex>
#include <stop_token>
#include <string>
#include <thread>
#include <unordered_map>
#include <vector>

#include <temporalio/activities/activity.h>
#include <temporalio/activities/activity_context.h>
#include <temporalio/coro/task.h>
#include <temporalio/coro/task_completion_source.h>

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

/// Configuration for the internal ActivityWorker.
struct ActivityWorkerOptions {
    /// Bridge worker for FFI calls.
    bridge::Worker* bridge_worker{nullptr};

    /// Task queue being polled.
    std::string task_queue;

    /// Namespace.
    std::string ns;

    /// Registered activity definitions by name.
    std::unordered_map<std::string,
                       std::shared_ptr<activities::ActivityDefinition>>
        activities;

    /// Dynamic activity definition (if any).
    std::shared_ptr<activities::ActivityDefinition> dynamic_activity;

    /// Data converter.
    std::shared_ptr<converters::DataConverter> data_converter;

    /// Worker interceptors.
    std::vector<std::shared_ptr<interceptors::IWorkerInterceptor>>
        interceptors;

    /// Maximum concurrent activities.
    uint32_t max_concurrent{100};

    /// Graceful shutdown timeout for activities.
    std::chrono::milliseconds graceful_shutdown_timeout{0};
};

/// State of a running activity task.
struct RunningActivity {
    /// Activity execution context.
    std::shared_ptr<activities::ActivityExecutionContext> context;
    /// Cancellation source for this activity.
    std::stop_source cancel_source;
    /// Task token for bridge completion.
    std::vector<uint8_t> task_token;
    /// Whether the server requested cancellation.
    bool server_requested_cancel{false};
    /// Mutex protecting mutable state.
    std::mutex mutex;
    /// Whether the activity has completed.
    bool done{false};
    /// Thread running this activity. Stored to allow joining on shutdown,
    /// preventing use-after-free if the worker is destroyed while the
    /// thread is still running.
    std::jthread thread;

    /// Mark the activity as done and cancel its token.
    void mark_done() {
        std::lock_guard lock(mutex);
        done = true;
        cancel_source.request_stop();
    }
};

/// Internal activity worker that polls activity tasks from the bridge
/// and dispatches them to registered ActivityDefinition handlers.
///
/// This mirrors the C# internal ActivityWorker class.
class ActivityWorker {
public:
    explicit ActivityWorker(ActivityWorkerOptions options);
    ~ActivityWorker();

    // Non-copyable
    ActivityWorker(const ActivityWorker&) = delete;
    ActivityWorker& operator=(const ActivityWorker&) = delete;

    /// Run the poll loop until the bridge signals shutdown.
    /// @return Task that completes when polling stops.
    coro::Task<void> execute_async();

    /// Notify the worker that shutdown is in progress.
    /// This cancels all running activity contexts.
    void notify_shutdown();

private:
    /// Poll for the next activity task from the bridge.
    coro::Task<std::optional<std::vector<uint8_t>>> poll_activity_task();

    /// Complete an activity task via the bridge.
    coro::Task<void> complete_activity_task(
        const std::vector<uint8_t>& completion_bytes);

    /// Start executing an activity from a task start message.
    void start_activity(const std::vector<uint8_t>& task_bytes);

    /// Execute an activity task (runs on a separate thread).
    void execute_activity(std::shared_ptr<RunningActivity> running,
                          std::shared_ptr<activities::ActivityDefinition> defn,
                          std::vector<uint8_t> task_token,
                          std::string token_key,
                          const std::vector<std::any>& args);

    /// Cancel a running activity by task token.
    void cancel_activity(const std::vector<uint8_t>& task_token);

    ActivityWorkerOptions options_;
    std::stop_source shutdown_source_;
    std::atomic<bool> shutdown_requested_{false};

    /// Running activities keyed by task token (serialized as string).
    std::unordered_map<std::string, std::shared_ptr<RunningActivity>>
        running_activities_;
    std::mutex running_activities_mutex_;

    /// TCS signaled when the last activity thread completes.
    /// Used by execute_async() to co_await activity completion without
    /// thread.join() (which would deadlock if called from a poll thread).
    std::shared_ptr<coro::TaskCompletionSource<void>> all_activities_done_tcs_;

    /// Timer thread for delayed cancellation after graceful shutdown timeout.
    /// Using a jthread member ensures it is joined in the destructor,
    /// preventing dangling pointer access to running_activities_mutex_
    /// and running_activities_.
    std::jthread shutdown_timer_thread_;
};

}  // namespace temporalio::worker::internal

