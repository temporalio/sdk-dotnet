#pragma once

/// @file temporal_client.h
/// @brief TemporalClient - workflow CRUD, schedule management.

#include <temporalio/export.h>
#include <temporalio/coro/task.h>
#include <temporalio/client/temporal_connection.h>
#include <temporalio/client/workflow_handle.h>
#include <temporalio/client/workflow_options.h>
#include <temporalio/converters/data_converter.h>

#include <any>
#include <memory>
#include <optional>
#include <string>
#include <vector>

namespace temporalio::bridge {
class Client;
} // namespace temporalio::bridge

namespace temporalio::runtime {
class TemporalRuntime;
} // namespace temporalio::runtime

namespace temporalio::client {

class TemporalConnection;

namespace interceptors {
class IClientInterceptor;
} // namespace interceptors

/// Options for creating a TemporalClient.
struct TemporalClientOptions {
    /// Namespace to use. Default: "default".
    std::string ns{"default"};

    /// Client interceptors. Earlier interceptors wrap later ones.
    std::vector<std::shared_ptr<interceptors::IClientInterceptor>>
        interceptors{};

    /// Data converter for serializing/deserializing arguments and results.
    /// If not set, the default DataConverter is used.
    std::optional<converters::DataConverter> data_converter{};
};

/// Options for connecting and creating a TemporalClient.
struct TemporalClientConnectOptions {
    /// Connection options.
    TemporalConnectionOptions connection{};

    /// Client options.
    TemporalClientOptions client{};
};

/// Information about a workflow execution.
struct WorkflowExecution {
    /// Workflow ID.
    std::string workflow_id{};

    /// Run ID.
    std::string run_id{};

    /// Workflow type name.
    std::optional<std::string> workflow_type{};
};

/// Count of workflow executions.
struct WorkflowExecutionCount {
    /// Total count.
    int64_t count{0};
};

/// Main client for interacting with a Temporal server.
/// Thread-safe and designed to be shared (std::shared_ptr).
///
/// Arguments to workflow operations are automatically serialized using
/// the DataConverter. Results can be deserialized to typed values using
/// the templated methods on WorkflowHandle (e.g., get_result<T>()).
///
/// Example:
/// @code
///   auto handle = co_await tc->start_workflow(
///       "MyWorkflow", opts, std::string("arg1"), 42);
///   auto result = co_await handle.get_result<std::string>();
/// @endcode
class TEMPORALIO_EXPORT TemporalClient : public std::enable_shared_from_this<TemporalClient> {
public:
    /// Connect to Temporal and create a client.
    static coro::Task<std::shared_ptr<TemporalClient>> connect(
        TemporalClientConnectOptions options);

    /// Create a client from an existing connection.
    static std::shared_ptr<TemporalClient> create(
        std::shared_ptr<TemporalConnection> connection,
        TemporalClientOptions options = {});

    ~TemporalClient();

    // Non-copyable
    TemporalClient(const TemporalClient&) = delete;
    TemporalClient& operator=(const TemporalClient&) = delete;

    /// Get the connection this client uses.
    std::shared_ptr<TemporalConnection> connection() const noexcept;

    /// Get the namespace this client uses.
    const std::string& ns() const noexcept;

    /// Get the underlying bridge client, or nullptr if not connected.
    /// Used internally by TemporalWorker to create a bridge worker.
    bridge::Client* bridge_client() const noexcept;

    /// Get the data converter used by this client.
    const converters::DataConverter& data_converter() const noexcept;

    /// Start a workflow execution with typed arguments.
    /// Arguments are serialized using the client's DataConverter.
    /// @param workflow_type The workflow type name.
    /// @param options Workflow start options (id, task_queue, etc.).
    /// @param args Typed arguments to pass to the workflow.
    /// @return A handle to the started workflow execution.
    template <typename... Args>
    coro::Task<WorkflowHandle> start_workflow(
        const std::string& workflow_type,
        const WorkflowOptions& options,
        Args&&... args) {
        std::vector<converters::Payload> payloads;
        if constexpr (sizeof...(args) > 0) {
            payloads = convert_args(std::forward<Args>(args)...);
        }
        co_return co_await start_workflow_impl(
            workflow_type, std::move(payloads), options);
    }

    /// Get a handle to an existing workflow.
    WorkflowHandle get_workflow_handle(
        const std::string& workflow_id,
        std::optional<std::string> run_id = std::nullopt);

    /// Signal a workflow with typed arguments.
    /// Arguments are serialized using the client's DataConverter.
    template <typename... Args>
    coro::Task<void> signal_workflow(
        const std::string& workflow_id,
        const std::string& signal_name,
        std::optional<std::string> run_id,
        Args&&... args) {
        std::vector<converters::Payload> payloads;
        if constexpr (sizeof...(args) > 0) {
            payloads = convert_args(std::forward<Args>(args)...);
        }
        co_await signal_workflow_impl(
            workflow_id, signal_name, std::move(payloads), std::move(run_id));
    }

    /// Signal a workflow with no arguments.
    coro::Task<void> signal_workflow(
        const std::string& workflow_id,
        const std::string& signal_name) {
        co_await signal_workflow_impl(
            workflow_id, signal_name, {}, std::nullopt);
    }

    /// Query a workflow (returns raw payload bytes as string).
    /// For typed results, use WorkflowHandle::query<T>().
    coro::Task<std::string> query_workflow(
        const std::string& workflow_id,
        const std::string& query_type,
        std::vector<converters::Payload> args = {},
        std::optional<std::string> run_id = std::nullopt);

    /// Cancel a workflow.
    coro::Task<void> cancel_workflow(
        const std::string& workflow_id,
        std::optional<std::string> run_id = std::nullopt);

    /// Terminate a workflow.
    coro::Task<void> terminate_workflow(
        const std::string& workflow_id,
        std::optional<std::string> reason = std::nullopt,
        std::optional<std::string> run_id = std::nullopt);

    /// List workflows.
    coro::Task<std::vector<WorkflowExecution>> list_workflows(
        const WorkflowListOptions& options = {});

    /// Count workflows.
    coro::Task<WorkflowExecutionCount> count_workflows(
        const WorkflowCountOptions& options = {});

private:
    friend class WorkflowHandle;

    TemporalClient(std::shared_ptr<TemporalConnection> connection,
                   TemporalClientOptions options);

    /// Convert variadic args to a vector of Payloads via DataConverter.
    template <typename... Args>
    std::vector<converters::Payload> convert_args(Args&&... args) {
        std::vector<converters::Payload> payloads;
        payloads.reserve(sizeof...(args));
        (payloads.push_back(
             data_converter().payload_converter->to_payload(
                 std::any(std::forward<Args>(args)))),
         ...);
        return payloads;
    }

    /// Internal start_workflow implementation with pre-serialized payloads.
    coro::Task<WorkflowHandle> start_workflow_impl(
        const std::string& workflow_type,
        std::vector<converters::Payload> args,
        const WorkflowOptions& options);

    /// Internal signal_workflow implementation with pre-serialized payloads.
    coro::Task<void> signal_workflow_impl(
        const std::string& workflow_id,
        const std::string& signal_name,
        std::vector<converters::Payload> args,
        std::optional<std::string> run_id);

    struct Impl;
    std::unique_ptr<Impl> impl_;
};

} // namespace temporalio::client
