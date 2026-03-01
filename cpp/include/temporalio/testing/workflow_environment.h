#pragma once

/// @file workflow_environment.h
/// @brief Test environment for running workflows against a local or remote
/// Temporal server.

#include <temporalio/export.h>
#include <temporalio/coro/task.h>
#include <temporalio/client/temporal_client.h>
#include <temporalio/client/temporal_connection.h>
#include <temporalio/runtime/temporal_runtime.h>

#include <chrono>
#include <memory>
#include <optional>
#include <string>
#include <vector>

namespace temporalio::testing {

/// Options for starting a local dev server environment.
struct WorkflowEnvironmentStartLocalOptions {
    /// Connection options for the client.
    client::TemporalConnectionOptions connection{};

    /// Client options.
    client::TemporalClientOptions client{};

    /// Path to an existing dev server binary (skip download).
    std::optional<std::string> existing_path{};

    /// Download directory for the dev server binary.
    /// Default is the OS temporary directory.
    std::optional<std::string> download_directory{};

    /// Version of the dev server to download.
    /// Can be a semantic version, "latest", or "default".
    /// Default is "default" (best for this SDK version).
    std::string download_version{"default"};

    /// Whether to start the UI with the server.
    bool ui{false};

    /// UI port if ui is true.
    int ui_port{0};

    /// Log format for the dev server (e.g., "pretty", "json").
    std::string log_format{"pretty"};

    /// Log level for the dev server (e.g., "warn", "info", "debug").
    std::string log_level{"warn"};

    /// Runtime to use. Uses default if not set.
    std::shared_ptr<runtime::TemporalRuntime> runtime{};

    /// Extra arguments to pass to the dev server.
    std::vector<std::string> extra_args{};
};

/// Options for starting a time-skipping test server environment.
struct WorkflowEnvironmentStartTimeSkippingOptions {
    /// Connection options for the client.
    client::TemporalConnectionOptions connection{};

    /// Client options.
    client::TemporalClientOptions client{};

    /// Path to an existing test server binary (skip download).
    std::optional<std::string> existing_path{};

    /// Download directory for the test server binary.
    std::optional<std::string> download_directory{};

    /// Version of the test server to download.
    /// Can be a semantic version, "latest", or "default".
    /// Default is "default" (best for this SDK version).
    std::string download_version{"default"};

    /// Runtime to use. Uses default if not set.
    std::shared_ptr<runtime::TemporalRuntime> runtime{};

    /// Extra arguments to pass to the test server.
    std::vector<std::string> extra_args{};
};

/// Workflow environment for testing with Temporal.
///
/// This class is NOT thread safe and not safe for use by concurrent tests.
/// Time skipping is locked/unlocked at an environment level, so while reuse
/// is supported, independent concurrent use can have side effects.
class TEMPORALIO_EXPORT WorkflowEnvironment {
public:
    /// Create an environment wrapping an existing client (no time skipping).
    explicit WorkflowEnvironment(
        std::shared_ptr<client::TemporalClient> client);

    virtual ~WorkflowEnvironment();

    // Non-copyable
    WorkflowEnvironment(const WorkflowEnvironment&) = delete;
    WorkflowEnvironment& operator=(const WorkflowEnvironment&) = delete;

    // Movable
    WorkflowEnvironment(WorkflowEnvironment&&) noexcept;
    WorkflowEnvironment& operator=(WorkflowEnvironment&&) noexcept;

    /// Start a local dev server with full Temporal capabilities but no time
    /// skipping. By default this lazily downloads the server if not present.
    static coro::Task<std::unique_ptr<WorkflowEnvironment>> start_local(
        WorkflowEnvironmentStartLocalOptions options = {});

    /// Start a local test server with time skipping but limited Temporal
    /// capabilities. By default this lazily downloads the server if not
    /// present.
    static coro::Task<std::unique_ptr<WorkflowEnvironment>>
    start_time_skipping(
        WorkflowEnvironmentStartTimeSkippingOptions options = {});

    /// Get the client for this workflow environment.
    std::shared_ptr<client::TemporalClient> client() const noexcept {
        return client_;
    }

    /// Whether this environment supports time skipping.
    virtual bool supports_time_skipping() const noexcept { return false; }

    /// Sleep in this environment. In time-skipping environments this may
    /// fast forward time instead of actually sleeping.
    virtual coro::Task<void> delay(std::chrono::milliseconds duration);

    /// Get the current time for the environment. For time-skipping
    /// environments this may return a different (fast-forwarded) time.
    virtual coro::Task<std::chrono::system_clock::time_point>
    get_current_time();

    /// Shutdown this environment. No-op for environments created with a
    /// simple client.
    virtual coro::Task<void> shutdown();

protected:
    std::shared_ptr<client::TemporalClient> client_;

private:
    struct Impl;
    std::unique_ptr<Impl> impl_;
};

} // namespace temporalio::testing
