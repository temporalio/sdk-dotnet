#pragma once

/// @file temporal_connection.h
/// @brief gRPC connection to a Temporal server.

#include <temporalio/export.h>
#include <temporalio/coro/task.h>

#include <chrono>
#include <memory>
#include <optional>
#include <string>
#include <utility>
#include <vector>

namespace temporalio::bridge {
class Client;
} // namespace temporalio::bridge

namespace temporalio::runtime {
class TemporalRuntime;
} // namespace temporalio::runtime

namespace temporalio::client {

/// TLS connection options.
struct TlsOptions {
    /// PEM-encoded server root CA certificate(s). If empty, uses system default.
    std::string server_root_ca_cert{};

    /// PEM-encoded client certificate for mTLS.
    std::string client_cert{};

    /// PEM-encoded client private key for mTLS.
    std::string client_private_key{};

    /// Server name override for TLS verification.
    std::optional<std::string> server_name{};

    /// If true, TLS is explicitly disabled even when set.
    bool disabled{false};
};

/// Keep-alive options for a connection.
struct KeepAliveOptions {
    /// Interval for keep-alive pings.
    std::chrono::milliseconds interval{std::chrono::seconds{30}};

    /// Timeout for keep-alive pings.
    std::chrono::milliseconds timeout{std::chrono::seconds{15}};
};

/// HTTP CONNECT proxy options.
struct HttpConnectProxyOptions {
    /// Proxy target (host:port).
    std::string target_host{};

    /// Basic auth user.
    std::optional<std::string> basic_auth_user{};

    /// Basic auth password.
    std::optional<std::string> basic_auth_password{};
};

/// Retry options for RPC calls.
struct RpcRetryOptions {
    /// Initial retry backoff interval.
    std::chrono::milliseconds initial_interval{std::chrono::milliseconds{100}};

    /// Randomization jitter factor.
    double randomization_factor{0.2};

    /// Backoff multiplier.
    double multiplier{1.5};

    /// Maximum retry interval.
    std::chrono::milliseconds max_interval{std::chrono::seconds{5}};

    /// Maximum elapsed time for retries.
    std::chrono::milliseconds max_elapsed_time{std::chrono::seconds{10}};

    /// Maximum number of retries (0 = unlimited).
    int max_retries{10};
};

/// Options for connecting to a Temporal server.
struct TemporalConnectionOptions {
    /// Target host:port to connect to (required).
    std::string target_host{};

    /// TLS options. Must be set (even default) for TLS connections.
    std::optional<TlsOptions> tls{};

    /// Retry options for RPC calls.
    std::optional<RpcRetryOptions> rpc_retry{};

    /// Keep-alive options. Default enabled, set nullopt to disable.
    std::optional<KeepAliveOptions> keep_alive{KeepAliveOptions{}};

    /// HTTP CONNECT proxy options.
    std::optional<HttpConnectProxyOptions> http_connect_proxy{};

    /// gRPC metadata (headers) for all calls.
    std::vector<std::pair<std::string, std::string>> rpc_metadata{};

    /// API key for all calls (Bearer token).
    std::optional<std::string> api_key{};

    /// Identity for this connection. Default: "pid@hostname".
    std::optional<std::string> identity{};

    /// Runtime for this connection. Uses default if not set.
    std::shared_ptr<runtime::TemporalRuntime> runtime{};
};

/// gRPC connection to a Temporal server. Thread-safe and reusable.
class TEMPORALIO_EXPORT TemporalConnection : public std::enable_shared_from_this<TemporalConnection> {
public:
    /// Connect to Temporal asynchronously.
    static coro::Task<std::shared_ptr<TemporalConnection>> connect(
        TemporalConnectionOptions options);

    /// Create a lazy connection that connects on first use.
    static std::shared_ptr<TemporalConnection> create_lazy(
        TemporalConnectionOptions options);

    ~TemporalConnection();

    // Non-copyable
    TemporalConnection(const TemporalConnection&) = delete;
    TemporalConnection& operator=(const TemporalConnection&) = delete;

    /// Check health of the server.
    coro::Task<bool> check_health();

    /// Whether the connection is currently established.
    bool is_connected() const noexcept;

    /// Get the connection options.
    const TemporalConnectionOptions& options() const noexcept {
        return options_;
    }

    /// Get the underlying bridge client, or nullptr if not connected.
    /// Used internally by TemporalWorker to create a bridge worker.
    bridge::Client* bridge_client() const noexcept;

private:
    explicit TemporalConnection(TemporalConnectionOptions options);

    struct Impl;
    std::unique_ptr<Impl> impl_;
    TemporalConnectionOptions options_;
};

} // namespace temporalio::client
