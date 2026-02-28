#pragma once

/// @file Activity execution context with heartbeat and cancellation support.

#include <temporalio/export.h>

#include <any>
#include <chrono>
#include <cstdint>
#include <functional>
#include <optional>
#include <stdexcept>
#include <stop_token>
#include <string>
#include <vector>

namespace temporalio::activities {

/// Information about the currently executing activity.
struct ActivityInfo {
    /// ID for the activity.
    std::string activity_id;
    /// Type name for the activity.
    std::string activity_type;
    /// Attempt the activity is on (starts at 1).
    int attempt = 1;
    /// When the current attempt was scheduled.
    std::chrono::system_clock::time_point current_attempt_scheduled_time;
    /// Heartbeat timeout set by the caller.
    std::optional<std::chrono::milliseconds> heartbeat_timeout;
    /// Whether the activity is local.
    bool is_local = false;
    /// Namespace this activity is on.
    std::string namespace_;
    /// Schedule to close timeout set by the caller.
    std::optional<std::chrono::milliseconds> schedule_to_close_timeout;
    /// When the activity was scheduled.
    std::chrono::system_clock::time_point scheduled_time;
    /// Start to close timeout set by the caller.
    std::optional<std::chrono::milliseconds> start_to_close_timeout;
    /// When the activity started.
    std::chrono::system_clock::time_point started_time;
    /// Task queue this activity is on.
    std::string task_queue;
    /// Task token uniquely identifying this activity.
    std::vector<uint8_t> task_token;
    /// Workflow ID that started this activity (empty for standalone).
    std::optional<std::string> workflow_id;
    /// Namespace of the workflow that started this activity.
    std::optional<std::string> workflow_namespace;
    /// Workflow run ID that started this activity.
    std::optional<std::string> workflow_run_id;
    /// Workflow type name that started this activity.
    std::optional<std::string> workflow_type;

    /// Whether this activity was started by a workflow (vs standalone).
    bool is_workflow_activity() const noexcept {
        return workflow_id.has_value();
    }
};

/// Ambient execution context for the currently running activity.
/// Accessed via thread_local pointer using current().
///
/// This replaces the C# ActivityExecutionContext with its AsyncLocal<T>.
/// Since C++ activities run on a thread pool, we use thread_local storage.
class TEMPORALIO_EXPORT ActivityExecutionContext {
public:
    ActivityExecutionContext(ActivityInfo info,
                            std::stop_token cancellation_token,
                            std::stop_token worker_shutdown_token);

    /// Check if there is a current activity context.
    static bool has_current() noexcept { return current_ptr_ != nullptr; }

    /// Get the current activity context.
    /// Throws std::runtime_error if no context is available.
    static ActivityExecutionContext& current() {
        if (!current_ptr_) {
            throw std::runtime_error(
                "No current activity execution context");
        }
        return *current_ptr_;
    }

    /// Get the activity info.
    const ActivityInfo& info() const noexcept { return info_; }

    /// Get the cancellation token for this activity.
    std::stop_token cancellation_token() const noexcept {
        return cancellation_token_;
    }

    /// Get the worker shutdown token.
    std::stop_token worker_shutdown_token() const noexcept {
        return worker_shutdown_token_;
    }

    /// Send a heartbeat. The details parameter is optional.
    void heartbeat(const std::any& details = {});

    /// Set the heartbeat callback (called by the worker when heartbeat is
    /// invoked).
    void set_heartbeat_callback(
        std::function<void(const std::any&)> callback) {
        heartbeat_callback_ = std::move(callback);
    }

private:
    friend class ActivityContextScope;

    static thread_local ActivityExecutionContext* current_ptr_;

    ActivityInfo info_;
    std::stop_token cancellation_token_;
    std::stop_token worker_shutdown_token_;
    std::function<void(const std::any&)> heartbeat_callback_;
};

/// RAII scope that sets and restores the thread-local activity context.
class ActivityContextScope {
public:
    explicit ActivityContextScope(ActivityExecutionContext& ctx) noexcept
        : previous_(ActivityExecutionContext::current_ptr_) {
        ActivityExecutionContext::current_ptr_ = &ctx;
    }

    ~ActivityContextScope() noexcept {
        ActivityExecutionContext::current_ptr_ = previous_;
    }

    ActivityContextScope(const ActivityContextScope&) = delete;
    ActivityContextScope& operator=(const ActivityContextScope&) = delete;

private:
    ActivityExecutionContext* previous_;
};

/// Exception thrown to indicate async activity completion.
/// When an activity throws this, the worker will not report completion and
/// instead the activity must be completed asynchronously.
class CompleteAsyncException : public std::exception {
public:
    const char* what() const noexcept override {
        return "Activity completing asynchronously";
    }
};

}  // namespace temporalio::activities

