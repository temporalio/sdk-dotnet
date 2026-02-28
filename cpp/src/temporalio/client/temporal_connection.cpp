#include <temporalio/coro/task_completion_source.h>
#include <temporalio/client/temporal_connection.h>
#include <temporalio/runtime/temporal_runtime.h>

#include <coroutine>
#include <memory>
#include <mutex>
#include <stdexcept>
#include <string>
#include <utility>

#include "temporalio/bridge/client.h"
#include "temporalio/bridge/runtime.h"

namespace temporalio::client {

struct TemporalConnection::Impl {
    bool connected{false};
    std::mutex mutex;

    /// The bridge client (null until connected).
    std::unique_ptr<bridge::Client> bridge_client;

    /// The bridge runtime (shared with the client).
    bridge::Runtime* bridge_runtime = nullptr;
};

TemporalConnection::TemporalConnection(TemporalConnectionOptions options)
    : impl_(std::make_unique<Impl>()), options_(std::move(options)) {
    // Set default identity if not provided
    if (!options_.identity) {
        options_.identity = "cpp-sdk";
    }
}

TemporalConnection::~TemporalConnection() = default;

namespace {

/// Convert public connection options to bridge client options.
bridge::ClientOptions to_bridge_options(
    const TemporalConnectionOptions& opts) {
    bridge::ClientOptions bridge_opts;

    // Auto-prepend scheme if missing, matching C# SDK behavior.
    auto host = opts.target_host;
    if (host.substr(0, 7) != "http://" && host.substr(0, 8) != "https://") {
        bool use_tls = opts.tls && !opts.tls->disabled;
        host = (use_tls ? "https://" : "http://") + host;
    }
    bridge_opts.target_url = std::move(host);
    bridge_opts.client_name = "temporal-cpp";
    bridge_opts.client_version = "0.1.0";
    bridge_opts.identity = opts.identity.value_or("cpp-sdk");
    bridge_opts.api_key = opts.api_key.value_or("");
    bridge_opts.metadata = opts.rpc_metadata;

    if (opts.tls && !opts.tls->disabled) {
        bridge::ClientTlsOptions tls;
        tls.server_root_ca_cert = opts.tls->server_root_ca_cert;
        tls.domain = opts.tls->server_name.value_or("");
        tls.client_cert = opts.tls->client_cert;
        tls.client_private_key = opts.tls->client_private_key;
        bridge_opts.tls = std::move(tls);
    }

    if (opts.rpc_retry) {
        bridge::ClientRetryOptions retry;
        retry.initial_interval_millis =
            static_cast<uint64_t>(opts.rpc_retry->initial_interval.count());
        retry.randomization_factor = opts.rpc_retry->randomization_factor;
        retry.multiplier = opts.rpc_retry->multiplier;
        retry.max_interval_millis =
            static_cast<uint64_t>(opts.rpc_retry->max_interval.count());
        retry.max_elapsed_time_millis =
            static_cast<uint64_t>(opts.rpc_retry->max_elapsed_time.count());
        retry.max_retries = static_cast<size_t>(opts.rpc_retry->max_retries);
        bridge_opts.retry = std::move(retry);
    }

    if (opts.keep_alive) {
        bridge::ClientKeepAliveOptions ka;
        ka.interval_millis =
            static_cast<uint64_t>(opts.keep_alive->interval.count());
        ka.timeout_millis =
            static_cast<uint64_t>(opts.keep_alive->timeout.count());
        bridge_opts.keep_alive = std::move(ka);
    }

    if (opts.http_connect_proxy) {
        bridge::ClientHttpConnectProxyOptions proxy;
        proxy.target_host = opts.http_connect_proxy->target_host;
        proxy.username =
            opts.http_connect_proxy->basic_auth_user.value_or("");
        proxy.password =
            opts.http_connect_proxy->basic_auth_password.value_or("");
        bridge_opts.http_connect_proxy = std::move(proxy);
    }

    return bridge_opts;
}

}  // namespace

coro::Task<std::shared_ptr<TemporalConnection>>
TemporalConnection::connect(TemporalConnectionOptions options) {
    auto conn = std::shared_ptr<TemporalConnection>(
        new TemporalConnection(std::move(options)));

    // Get or create the runtime
    auto runtime_ptr = conn->options_.runtime;
    if (!runtime_ptr) {
        runtime_ptr = runtime::TemporalRuntime::default_instance();
    }

    auto* bridge_rt = runtime_ptr->bridge_runtime();
    if (!bridge_rt) {
        throw std::runtime_error(
            "TemporalRuntime has no bridge runtime initialized");
    }

    conn->impl_->bridge_runtime = bridge_rt;

    // Convert options to bridge format
    auto bridge_opts = to_bridge_options(conn->options_);

    // Bridge the async FFI callback to a coroutine via TaskCompletionSource
    auto tcs =
        std::make_shared<coro::TaskCompletionSource<bridge::ClientHandle>>();

    bridge::Client::connect_async(
        *bridge_rt, bridge_opts,
        [tcs](bridge::ClientHandle handle, std::string error) {
            if (!error.empty()) {
                tcs->try_set_exception(std::make_exception_ptr(
                    std::runtime_error(
                        "Failed to connect to Temporal: " + error)));
            } else {
                tcs->try_set_result(std::move(handle));
            }
        });

    // Await the completion of the async connect
    auto client_handle = co_await tcs->task();

    // Create the bridge client wrapper
    conn->impl_->bridge_client = std::make_unique<bridge::Client>(
        *bridge_rt, std::move(client_handle));
    conn->impl_->connected = true;

    co_return conn;
}

std::shared_ptr<TemporalConnection> TemporalConnection::create_lazy(
    TemporalConnectionOptions options) {
    return std::shared_ptr<TemporalConnection>(
        new TemporalConnection(std::move(options)));
}

coro::Task<bool> TemporalConnection::check_health() {
    co_return impl_->connected;
}

bool TemporalConnection::is_connected() const noexcept {
    return impl_->connected;
}

bridge::Client* TemporalConnection::bridge_client() const noexcept {
    return impl_->bridge_client.get();
}

} // namespace temporalio::client
