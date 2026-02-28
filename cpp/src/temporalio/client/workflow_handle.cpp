#include <temporalio/client/workflow_handle.h>
#include <temporalio/client/temporal_client.h>
#include <temporalio/converters/data_converter.h>

#include <cstdint>
#include <stdexcept>
#include <utility>
#include <vector>

#include <temporal/api/workflowservice/v1/request_response.pb.h>
#include <temporal/api/common/v1/message.pb.h>
#include <temporal/api/history/v1/message.pb.h>
#include <temporal/api/enums/v1/workflow.pb.h>

#include "temporalio/client/rpc_helpers.h"

namespace temporalio::client {

namespace {

/// Extract workflow result from a GetWorkflowExecutionHistory response.
/// Returns the result payload if a close event is found.
/// Throws on failure/cancel/terminate/timeout.
/// Returns nullopt if no close event found (caller should continue polling).
struct PollResult {
    std::optional<converters::Payload> result;
    std::string next_page_token;
};

PollResult process_history_response(const std::vector<uint8_t>& response_bytes) {
    temporal::api::workflowservice::v1::
        GetWorkflowExecutionHistoryResponse resp;
    if (!resp.ParseFromArray(response_bytes.data(),
                              static_cast<int>(response_bytes.size()))) {
        throw std::runtime_error(
            "Failed to parse GetWorkflowExecutionHistory response");
    }

    for (const auto& event : resp.history().events()) {
        if (event.has_workflow_execution_completed_event_attributes()) {
            const auto& attrs =
                event.workflow_execution_completed_event_attributes();
            if (attrs.has_result() &&
                attrs.result().payloads_size() > 0) {
                return PollResult{
                    internal::from_proto_payload(attrs.result().payloads(0)),
                    {}};
            }
            // Completed with no result payload - return empty payload
            converters::Payload empty_payload;
            empty_payload.metadata["encoding"] = "binary/null";
            return PollResult{std::move(empty_payload), {}};
        }
        if (event.has_workflow_execution_failed_event_attributes()) {
            const auto& attrs =
                event.workflow_execution_failed_event_attributes();
            std::string msg = "Workflow execution failed";
            if (attrs.has_failure()) {
                msg = attrs.failure().message();
            }
            throw std::runtime_error(msg);
        }
        if (event.has_workflow_execution_canceled_event_attributes()) {
            throw std::runtime_error("Workflow execution cancelled");
        }
        if (event.has_workflow_execution_terminated_event_attributes()) {
            throw std::runtime_error("Workflow execution terminated");
        }
        if (event.has_workflow_execution_timed_out_event_attributes()) {
            throw std::runtime_error("Workflow execution timed out");
        }
    }

    // No close event found
    PollResult pr;
    pr.next_page_token = resp.next_page_token();
    return pr;
}

}  // namespace

WorkflowHandle::WorkflowHandle(std::shared_ptr<TemporalClient> client,
                               std::string id,
                               std::optional<std::string> run_id,
                               std::optional<std::string> first_execution_run_id)
    : client_(std::move(client)),
      id_(std::move(id)),
      run_id_(std::move(run_id)),
      first_execution_run_id_(std::move(first_execution_run_id)) {}

const converters::DataConverter& WorkflowHandle::get_data_converter() const {
    return client_->data_converter();
}

coro::Task<converters::Payload> WorkflowHandle::get_result_payload() {
    auto* bridge = client_->bridge_client();
    if (!bridge) {
        throw std::runtime_error("Not connected to Temporal server");
    }

    // Build GetWorkflowExecutionHistoryRequest
    temporal::api::workflowservice::v1::GetWorkflowExecutionHistoryRequest req;
    req.set_namespace_(client_->ns());
    req.mutable_execution()->set_workflow_id(id_);
    if (run_id_) {
        req.mutable_execution()->set_run_id(*run_id_);
    }
    req.set_history_event_filter_type(
        temporal::api::enums::v1::HISTORY_EVENT_FILTER_TYPE_CLOSE_EVENT);
    req.set_wait_new_event(true);

    std::string next_page_token;

    while (true) {
        if (!next_page_token.empty()) {
            req.set_next_page_token(next_page_token);
        }

        bridge::RpcCallOptions rpc_opts;
        rpc_opts.service = TemporalCoreRpcService::Workflow;
        rpc_opts.rpc = "GetWorkflowExecutionHistory";
        std::string serialized = req.SerializeAsString();
        rpc_opts.request = std::vector<uint8_t>(serialized.begin(),
                                                 serialized.end());
        rpc_opts.retry = true;

        auto response_bytes =
            co_await internal::rpc_call(*bridge, std::move(rpc_opts));

        auto poll_result = process_history_response(response_bytes);
        if (poll_result.result.has_value()) {
            co_return std::move(*poll_result.result);
        }

        next_page_token = std::move(poll_result.next_page_token);
    }
}

coro::Task<void> WorkflowHandle::signal_impl(
    const std::string& signal_name,
    std::vector<converters::Payload> args,
    const WorkflowSignalOptions& /*options*/) {
    co_await client_->signal_workflow_impl(
        id_, signal_name, std::move(args), run_id_);
}

coro::Task<converters::Payload> WorkflowHandle::query_payload(
    const std::string& query_type,
    std::vector<converters::Payload> args,
    const WorkflowQueryOptions& /*options*/) {
    auto* bridge = client_->bridge_client();
    if (!bridge) {
        throw std::runtime_error("Not connected to Temporal server");
    }

    // Build QueryWorkflowRequest
    temporal::api::workflowservice::v1::QueryWorkflowRequest req;
    req.set_namespace_(client_->ns());
    req.mutable_execution()->set_workflow_id(id_);
    if (run_id_) {
        req.mutable_execution()->set_run_id(*run_id_);
    }
    req.mutable_query()->set_query_type(query_type);
    if (!args.empty()) {
        internal::set_proto_payloads(
            req.mutable_query()->mutable_query_args(), args);
    }

    bridge::RpcCallOptions rpc_opts;
    rpc_opts.service = TemporalCoreRpcService::Workflow;
    rpc_opts.rpc = "QueryWorkflow";
    std::string serialized = req.SerializeAsString();
    rpc_opts.request = std::vector<uint8_t>(serialized.begin(),
                                             serialized.end());
    rpc_opts.retry = true;

    auto response = co_await internal::rpc_call(*bridge, std::move(rpc_opts));

    // Parse response and extract result, preserving the server's payload
    // metadata (encoding, etc.) instead of hardcoding "json/plain".
    temporal::api::workflowservice::v1::QueryWorkflowResponse resp;
    resp.ParseFromArray(response.data(), static_cast<int>(response.size()));
    if (resp.has_query_result() && resp.query_result().payloads_size() > 0) {
        co_return internal::from_proto_payload(resp.query_result().payloads(0));
    }

    // No result payload - return empty payload with binary/null encoding
    converters::Payload empty_payload;
    empty_payload.metadata["encoding"] = "binary/null";
    co_return empty_payload;
}

coro::Task<void> WorkflowHandle::cancel(
    const WorkflowCancelOptions& options) {
    co_await client_->cancel_workflow(id_, run_id_);
}

coro::Task<void> WorkflowHandle::terminate(
    const WorkflowTerminateOptions& options) {
    co_await client_->terminate_workflow(id_, options.reason, run_id_);
}

coro::Task<std::string> WorkflowHandle::describe(
    const WorkflowDescribeOptions& options) {
    // TODO: Implement via client RPC
    co_return std::string{};
}

} // namespace temporalio::client
