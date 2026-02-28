#pragma once

/// @file activity_environment.h
/// @brief Test environment for running activities in isolation.

#include <temporalio/export.h>
#include <temporalio/activities/activity.h>
#include <temporalio/activities/activity_context.h>
#include <temporalio/client/temporal_client.h>

#include <any>
#include <functional>
#include <memory>
#include <stop_token>
#include <string>
#include <vector>

namespace temporalio::testing {

/// Info for configuring a test activity environment.
struct ActivityEnvironmentInfo {
    /// Activity info to use in the test context.
    activities::ActivityInfo info{};
};

/// Environment for running activities in isolation for testing.
///
/// This sets up an ActivityExecutionContext with configurable info,
/// heartbeat callbacks, and cancellation support. Activities under test
/// can call ActivityExecutionContext::current() to access the context.
class TEMPORALIO_EXPORT ActivityEnvironment {
public:
    /// Create a default activity environment.
    ActivityEnvironment();

    ~ActivityEnvironment();

    // Non-copyable
    ActivityEnvironment(const ActivityEnvironment&) = delete;
    ActivityEnvironment& operator=(const ActivityEnvironment&) = delete;

    /// Set the client for the environment (if activities need it).
    void set_client(std::shared_ptr<client::TemporalClient> client);

    /// Set the heartbeat callback.
    void set_heartbeater(
        std::function<void(std::vector<std::any>)> fn);

    /// Set the activity info for the test context.
    void set_info(activities::ActivityInfo info) { info_ = std::move(info); }

    /// Get the current activity info.
    const activities::ActivityInfo& info() const noexcept { return info_; }

    /// Cancel the activity (triggers the cancellation token).
    void cancel();

    /// Whether the activity has been cancelled.
    bool is_cancelled() const noexcept {
        return cancel_source_.stop_requested();
    }

    /// Get the cancellation stop source (for advanced test control).
    std::stop_source& cancel_source() noexcept { return cancel_source_; }

    /// Get the worker shutdown stop source (for advanced test control).
    std::stop_source& worker_shutdown_source() noexcept {
        return shutdown_source_;
    }

    /// Run an activity function synchronously in this environment.
    /// Sets up the activity execution context with the configured info,
    /// cancellation token, and heartbeat callback before calling fn.
    template <typename F>
    auto run(F&& fn) -> decltype(fn()) {
        activities::ActivityExecutionContext ctx(
            info_, cancel_source_.get_token(),
            shutdown_source_.get_token());
        // Wire heartbeat callback to forward to the configured heartbeater
        ctx.set_heartbeat_callback([this](const std::any& details) {
            if (heartbeater_) {
                std::vector<std::any> args;
                if (details.has_value()) {
                    args.push_back(details);
                }
                heartbeater_(std::move(args));
            }
        });
        activities::ActivityContextScope scope(ctx);
        return fn();
    }

private:
    activities::ActivityInfo info_{};
    std::function<void(std::vector<std::any>)> heartbeater_;
    std::shared_ptr<client::TemporalClient> client_;
    std::stop_source cancel_source_;
    std::stop_source shutdown_source_;
};

/// Default activity info for testing.
inline activities::ActivityInfo default_activity_info() {
    activities::ActivityInfo info;
    info.activity_id = "test-activity-id";
    info.activity_type = "TestActivity";
    info.attempt = 1;
    info.namespace_ = "default";
    info.task_queue = "test-task-queue";
    info.workflow_id = "test-workflow-id";
    return info;
}

} // namespace temporalio::testing
