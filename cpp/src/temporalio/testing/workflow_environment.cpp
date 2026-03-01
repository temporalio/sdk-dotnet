#include "temporalio/testing/workflow_environment.h"

#include <temporalio/coro/task_completion_source.h>
#include <temporalio/version.h>

#include <chrono>
#include <memory>
#include <stdexcept>
#include <string>
#include <thread>
#include <utility>

#include "temporalio/bridge/ephemeral_server.h"

namespace temporalio::testing {

// ── Impl ────────────────────────────────────────────────────────────────────

struct WorkflowEnvironment::Impl {
    /// The ephemeral server (if this environment started one).
    std::unique_ptr<bridge::EphemeralServer> ephemeral_server;
    bool owns_server{false};
};

// ── WorkflowEnvironment ────────────────────────────────────────────────────

WorkflowEnvironment::WorkflowEnvironment(
    std::shared_ptr<client::TemporalClient> client)
    : client_(std::move(client)), impl_(std::make_unique<Impl>()) {}

WorkflowEnvironment::~WorkflowEnvironment() = default;

WorkflowEnvironment::WorkflowEnvironment(WorkflowEnvironment&&) noexcept =
    default;

WorkflowEnvironment& WorkflowEnvironment::operator=(
    WorkflowEnvironment&&) noexcept = default;

/// Result of starting an ephemeral server (for passing through TCS).
struct EphemeralServerResult {
    bridge::EphemeralServerHandle handle;
    std::string target;
};

coro::Task<std::unique_ptr<WorkflowEnvironment>>
WorkflowEnvironment::start_local(
    WorkflowEnvironmentStartLocalOptions options) {
    // Get or create the runtime
    auto runtime_ptr = options.runtime;
    if (!runtime_ptr) {
        runtime_ptr = runtime::TemporalRuntime::default_instance();
    }

    auto* bridge_rt = runtime_ptr->bridge_runtime();
    if (!bridge_rt) {
        throw std::runtime_error(
            "TemporalRuntime has no bridge runtime initialized");
    }

    // Build dev server options
    bridge::DevServerOptions dev_opts;
    dev_opts.sdk_version = TEMPORALIO_VERSION_STRING;
    dev_opts.download_version = options.download_version;
    dev_opts.ui = options.ui;
    dev_opts.ui_port = static_cast<uint16_t>(options.ui_port);
    dev_opts.log_format = options.log_format;
    dev_opts.log_level = options.log_level;
    if (options.existing_path) {
        dev_opts.existing_path = *options.existing_path;
    }
    if (options.download_directory) {
        dev_opts.download_dest_dir = *options.download_directory;
    }
    if (!options.extra_args.empty()) {
        std::string joined;
        for (size_t i = 0; i < options.extra_args.size(); ++i) {
            if (i > 0) joined.push_back('\n');
            joined.append(options.extra_args[i]);
        }
        dev_opts.extra_args = std::move(joined);
    }

    // Start the dev server via the bridge, bridging callback to coroutine
    auto tcs =
        std::make_shared<coro::TaskCompletionSource<EphemeralServerResult>>();

    bridge::EphemeralServer::start_dev_server_async(
        *bridge_rt, dev_opts,
        [tcs](bridge::EphemeralServerHandle handle,
              std::string target, std::string error) {
            if (!error.empty()) {
                tcs->try_set_exception(std::make_exception_ptr(
                    std::runtime_error(
                        "Failed to start dev server: " + error)));
            } else {
                tcs->try_set_result(EphemeralServerResult{
                    std::move(handle), std::move(target)});
            }
        });

    auto server_result = co_await tcs->task();

    // Create the EphemeralServer wrapper
    auto server = std::make_unique<bridge::EphemeralServer>(
        *bridge_rt, std::move(server_result.handle),
        std::move(server_result.target),
        /*has_test_service=*/false);

    // Connect to the started server
    options.connection.target_host = server->target();
    options.connection.runtime = runtime_ptr;
    auto conn =
        co_await client::TemporalConnection::connect(options.connection);
    auto cli = client::TemporalClient::create(conn, options.client);

    auto env = std::make_unique<WorkflowEnvironment>(cli);
    env->impl_->ephemeral_server = std::move(server);
    env->impl_->owns_server = true;
    co_return std::move(env);
}

coro::Task<std::unique_ptr<WorkflowEnvironment>>
WorkflowEnvironment::start_time_skipping(
    WorkflowEnvironmentStartTimeSkippingOptions options) {
    // Get or create the runtime
    auto runtime_ptr = options.runtime;
    if (!runtime_ptr) {
        runtime_ptr = runtime::TemporalRuntime::default_instance();
    }

    auto* bridge_rt = runtime_ptr->bridge_runtime();
    if (!bridge_rt) {
        throw std::runtime_error(
            "TemporalRuntime has no bridge runtime initialized");
    }

    // Build test server options
    bridge::TestServerOptions test_opts;
    test_opts.sdk_version = TEMPORALIO_VERSION_STRING;
    test_opts.download_version = options.download_version;
    if (options.existing_path) {
        test_opts.existing_path = *options.existing_path;
    }
    if (options.download_directory) {
        test_opts.download_dest_dir = *options.download_directory;
    }
    if (!options.extra_args.empty()) {
        std::string joined;
        for (size_t i = 0; i < options.extra_args.size(); ++i) {
            if (i > 0) joined.push_back('\n');
            joined.append(options.extra_args[i]);
        }
        test_opts.extra_args = std::move(joined);
    }

    // Start the test server via the bridge, bridging callback to coroutine
    auto tcs =
        std::make_shared<coro::TaskCompletionSource<EphemeralServerResult>>();

    bridge::EphemeralServer::start_test_server_async(
        *bridge_rt, test_opts,
        [tcs](bridge::EphemeralServerHandle handle,
              std::string target, std::string error) {
            if (!error.empty()) {
                tcs->try_set_exception(std::make_exception_ptr(
                    std::runtime_error(
                        "Failed to start test server: " + error)));
            } else {
                tcs->try_set_result(EphemeralServerResult{
                    std::move(handle), std::move(target)});
            }
        });

    auto server_result = co_await tcs->task();

    // Create the EphemeralServer wrapper
    auto server = std::make_unique<bridge::EphemeralServer>(
        *bridge_rt, std::move(server_result.handle),
        std::move(server_result.target),
        /*has_test_service=*/true);

    // Connect to the started server
    options.connection.target_host = server->target();
    options.connection.runtime = runtime_ptr;
    auto conn =
        co_await client::TemporalConnection::connect(options.connection);
    auto cli = client::TemporalClient::create(conn, options.client);

    auto env = std::make_unique<WorkflowEnvironment>(cli);
    env->impl_->ephemeral_server = std::move(server);
    env->impl_->owns_server = true;
    co_return std::move(env);
}

coro::Task<void> WorkflowEnvironment::delay(
    std::chrono::milliseconds duration) {
    // Non-time-skipping: async sleep using a background thread + TCS
    auto tcs = std::make_shared<coro::TaskCompletionSource<void>>();

    std::thread([tcs, duration]() {
        std::this_thread::sleep_for(duration);
        tcs->try_set_result();
    }).detach();

    co_await tcs->task();
}

coro::Task<std::chrono::system_clock::time_point>
WorkflowEnvironment::get_current_time() {
    co_return std::chrono::system_clock::now();
}

coro::Task<void> WorkflowEnvironment::shutdown() {
    if (!impl_->owns_server || !impl_->ephemeral_server) {
        co_return;
    }

    auto tcs = std::make_shared<coro::TaskCompletionSource<void>>();

    impl_->ephemeral_server->shutdown_async(
        [tcs](std::string error) {
            if (!error.empty()) {
                tcs->try_set_exception(std::make_exception_ptr(
                    std::runtime_error(
                        "Failed to shutdown server: " + error)));
            } else {
                tcs->try_set_result();
            }
        });

    co_await tcs->task();
    impl_->ephemeral_server.reset();
    impl_->owns_server = false;
}

} // namespace temporalio::testing
