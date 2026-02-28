#pragma once

/// @file workflow_handle.h
/// @brief Handle to a running or completed workflow execution.

#include <temporalio/export.h>
#include <temporalio/coro/task.h>
#include <temporalio/client/workflow_options.h>
#include <temporalio/converters/data_converter.h>

#include <any>
#include <memory>
#include <optional>
#include <string>
#include <typeindex>
#include <vector>

namespace temporalio::client {

class TemporalClient;

/// Handle to a workflow execution, used for signaling, querying, etc.
/// This is a lightweight value type that holds a reference to the client.
///
/// Results and arguments are automatically converted using the client's
/// DataConverter. Use the templated methods for typed access:
/// @code
///   auto result = co_await handle.get_result<std::string>();
///   co_await handle.signal("my_signal", std::string("arg1"), 42);
///   auto count = co_await handle.query<int>("get_count");
/// @endcode
class TEMPORALIO_EXPORT WorkflowHandle {
public:
    /// Construct a workflow handle.
    WorkflowHandle(std::shared_ptr<TemporalClient> client,
                   std::string id,
                   std::optional<std::string> run_id = std::nullopt,
                   std::optional<std::string> first_execution_run_id = std::nullopt);

    /// Workflow ID.
    const std::string& id() const noexcept { return id_; }

    /// Run ID (may be empty for "latest run").
    const std::optional<std::string>& run_id() const noexcept {
        return run_id_;
    }

    /// First execution run ID for continue-as-new following.
    const std::optional<std::string>& first_execution_run_id() const noexcept {
        return first_execution_run_id_;
    }

    /// Get the result of this workflow execution, deserialized to type T.
    /// Waits for the workflow to complete and returns the typed result.
    template <typename T>
    coro::Task<T> get_result() {
        auto payload = co_await get_result_payload();
        co_return std::any_cast<T>(
            get_data_converter().payload_converter->to_value(
                payload, std::type_index(typeid(T))));
    }

    /// Get the raw result payload of this workflow execution.
    /// Waits for the workflow to complete.
    coro::Task<converters::Payload> get_result_payload();

    /// Signal this workflow with typed arguments.
    /// Arguments are serialized using the client's DataConverter.
    template <typename... Args>
    coro::Task<void> signal(const std::string& signal_name,
                              Args&&... args) {
        std::vector<converters::Payload> payloads;
        if constexpr (sizeof...(args) > 0) {
            auto& dc = get_data_converter();
            (payloads.push_back(
                 dc.payload_converter->to_payload(
                     std::any(std::forward<Args>(args)))),
             ...);
        }
        co_await signal_impl(signal_name, std::move(payloads));
    }

    /// Query this workflow and deserialize the result to type T.
    template <typename T>
    coro::Task<T> query(const std::string& query_type) {
        auto payload = co_await query_payload(query_type, {});
        co_return std::any_cast<T>(
            get_data_converter().payload_converter->to_value(
                payload, std::type_index(typeid(T))));
    }

    /// Query this workflow with typed arguments and deserialize the result.
    template <typename T, typename... Args>
    coro::Task<T> query(const std::string& query_type, Args&&... args) {
        std::vector<converters::Payload> payloads;
        auto& dc = get_data_converter();
        (payloads.push_back(
             dc.payload_converter->to_payload(
                 std::any(std::forward<Args>(args)))),
         ...);
        auto payload = co_await query_payload(query_type, std::move(payloads));
        co_return std::any_cast<T>(
            dc.payload_converter->to_value(
                payload, std::type_index(typeid(T))));
    }

    /// Query this workflow and return the raw result payload.
    coro::Task<converters::Payload> query_payload(
        const std::string& query_type,
        std::vector<converters::Payload> args = {},
        const WorkflowQueryOptions& options = {});

    /// Cancel this workflow.
    coro::Task<void> cancel(const WorkflowCancelOptions& options = {});

    /// Terminate this workflow.
    coro::Task<void> terminate(const WorkflowTerminateOptions& options = {});

    /// Describe this workflow.
    coro::Task<std::string> describe(
        const WorkflowDescribeOptions& options = {});

private:
    /// Internal signal implementation with pre-serialized payloads.
    coro::Task<void> signal_impl(
        const std::string& signal_name,
        std::vector<converters::Payload> args,
        const WorkflowSignalOptions& options = {});

    /// Get the data converter from the client.
    const converters::DataConverter& get_data_converter() const;

    std::shared_ptr<TemporalClient> client_;
    std::string id_;
    std::optional<std::string> run_id_;
    std::optional<std::string> first_execution_run_id_;
};

} // namespace temporalio::client
