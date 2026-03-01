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
#include <temporal/api/workflow/v1/message.pb.h>
#include <temporal/api/update/v1/message.pb.h>
#include <temporal/api/enums/v1/update.pb.h>
#include <google/protobuf/timestamp.pb.h>

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

/// Convert a protobuf Timestamp to a system_clock time_point.
std::chrono::system_clock::time_point timestamp_to_time_point(
    const google::protobuf::Timestamp& ts) {
    auto secs = std::chrono::seconds(ts.seconds());
    auto nanos = std::chrono::nanoseconds(ts.nanos());
    return std::chrono::system_clock::time_point(
        std::chrono::duration_cast<std::chrono::system_clock::duration>(
            secs + nanos));
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

coro::Task<converters::Payload> WorkflowHandle::update_impl(
    const std::string& update_name,
    std::vector<converters::Payload> args,
    const WorkflowUpdateOptions& options) {
    auto* bridge = client_->bridge_client();
    if (!bridge) {
        throw std::runtime_error("Not connected to Temporal server");
    }

    // Build UpdateWorkflowExecutionRequest
    temporal::api::workflowservice::v1::UpdateWorkflowExecutionRequest req;
    req.set_namespace_(client_->ns());
    req.mutable_workflow_execution()->set_workflow_id(id_);
    if (run_id_) {
        req.mutable_workflow_execution()->set_run_id(*run_id_);
    }

    // Set wait policy
    auto* wait_policy = req.mutable_wait_policy();
    wait_policy->set_lifecycle_stage(
        static_cast<temporal::api::enums::v1::
            UpdateWorkflowExecutionLifecycleStage>(
            static_cast<int>(options.wait_stage)));

    // Build the update request
    auto* update_req = req.mutable_request();
    auto* meta = update_req->mutable_meta();
    meta->set_update_id(options.update_id.value_or(""));
    auto* input = update_req->mutable_input();
    input->set_name(update_name);
    if (!args.empty()) {
        internal::set_proto_payloads(input->mutable_args(), args);
    }

    bridge::RpcCallOptions rpc_opts;
    rpc_opts.service = TemporalCoreRpcService::Workflow;
    rpc_opts.rpc = "UpdateWorkflowExecution";
    std::string serialized = req.SerializeAsString();
    rpc_opts.request = std::vector<uint8_t>(serialized.begin(),
                                             serialized.end());
    rpc_opts.retry = true;

    auto response = co_await internal::rpc_call(*bridge, std::move(rpc_opts));

    // Parse response
    temporal::api::workflowservice::v1::UpdateWorkflowExecutionResponse resp;
    if (!resp.ParseFromArray(response.data(),
                              static_cast<int>(response.size()))) {
        throw std::runtime_error(
            "Failed to parse UpdateWorkflowExecution response");
    }

    // Extract outcome
    if (resp.has_outcome()) {
        const auto& outcome = resp.outcome();
        if (outcome.has_success() &&
            outcome.success().payloads_size() > 0) {
            co_return internal::from_proto_payload(
                outcome.success().payloads(0));
        }
        if (outcome.has_failure()) {
            throw std::runtime_error(
                "Update failed: " + outcome.failure().message());
        }
        // Success with no payload
        converters::Payload empty_payload;
        empty_payload.metadata["encoding"] = "binary/null";
        co_return empty_payload;
    }

    // No outcome yet - need to poll
    if (!resp.has_update_ref()) {
        throw std::runtime_error(
            "UpdateWorkflowExecution returned neither outcome nor update_ref");
    }

    // Poll for the update result
    temporal::api::workflowservice::v1::
        PollWorkflowExecutionUpdateRequest poll_req;
    poll_req.set_namespace_(client_->ns());
    auto* poll_ref = poll_req.mutable_update_ref();
    poll_ref->mutable_workflow_execution()->set_workflow_id(id_);
    if (run_id_) {
        poll_ref->mutable_workflow_execution()->set_run_id(*run_id_);
    }
    poll_ref->set_update_id(resp.update_ref().update_id());

    bridge::RpcCallOptions poll_rpc_opts;
    poll_rpc_opts.service = TemporalCoreRpcService::Workflow;
    poll_rpc_opts.rpc = "PollWorkflowExecutionUpdate";
    std::string poll_serialized = poll_req.SerializeAsString();
    poll_rpc_opts.request = std::vector<uint8_t>(
        poll_serialized.begin(), poll_serialized.end());
    poll_rpc_opts.retry = true;

    auto poll_response =
        co_await internal::rpc_call(*bridge, std::move(poll_rpc_opts));

    temporal::api::workflowservice::v1::
        PollWorkflowExecutionUpdateResponse poll_resp;
    if (!poll_resp.ParseFromArray(
            poll_response.data(),
            static_cast<int>(poll_response.size()))) {
        throw std::runtime_error(
            "Failed to parse PollWorkflowExecutionUpdate response");
    }

    if (poll_resp.has_outcome()) {
        const auto& outcome = poll_resp.outcome();
        if (outcome.has_success() &&
            outcome.success().payloads_size() > 0) {
            co_return internal::from_proto_payload(
                outcome.success().payloads(0));
        }
        if (outcome.has_failure()) {
            throw std::runtime_error(
                "Update failed: " + outcome.failure().message());
        }
    }

    // No result payload
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

coro::Task<WorkflowExecutionDescription> WorkflowHandle::describe(
    const WorkflowDescribeOptions& options) {
    auto* bridge = client_->bridge_client();
    if (!bridge) {
        throw std::runtime_error("Not connected to Temporal server");
    }

    // Build DescribeWorkflowExecutionRequest
    temporal::api::workflowservice::v1::DescribeWorkflowExecutionRequest req;
    req.set_namespace_(client_->ns());
    req.mutable_execution()->set_workflow_id(id_);
    if (run_id_) {
        req.mutable_execution()->set_run_id(*run_id_);
    }

    bridge::RpcCallOptions rpc_opts;
    rpc_opts.service = TemporalCoreRpcService::Workflow;
    rpc_opts.rpc = "DescribeWorkflowExecution";
    std::string serialized = req.SerializeAsString();
    rpc_opts.request = std::vector<uint8_t>(serialized.begin(),
                                             serialized.end());
    rpc_opts.retry = true;

    auto response = co_await internal::rpc_call(*bridge, std::move(rpc_opts));

    // Parse response
    temporal::api::workflowservice::v1::DescribeWorkflowExecutionResponse resp;
    if (!resp.ParseFromArray(response.data(),
                              static_cast<int>(response.size()))) {
        throw std::runtime_error(
            "Failed to parse DescribeWorkflowExecution response");
    }

    WorkflowExecutionDescription desc;

    if (resp.has_workflow_execution_info()) {
        const auto& info = resp.workflow_execution_info();

        if (info.has_execution()) {
            desc.workflow_id = info.execution().workflow_id();
            desc.run_id = info.execution().run_id();
        }
        if (info.has_type()) {
            desc.workflow_type = info.type().name();
        }

        desc.status = static_cast<common::WorkflowExecutionStatus>(
            static_cast<int>(info.status()));
        desc.task_queue = info.task_queue();
        desc.history_length = info.history_length();
        desc.history_size_bytes = info.history_size_bytes();
        desc.state_transition_count = info.state_transition_count();

        if (info.has_start_time()) {
            desc.start_time = timestamp_to_time_point(info.start_time());
        }
        if (info.has_close_time()) {
            desc.close_time = timestamp_to_time_point(info.close_time());
        }
        if (info.has_execution_time()) {
            desc.execution_time =
                timestamp_to_time_point(info.execution_time());
        }
        if (info.has_parent_execution()) {
            desc.parent_workflow_id =
                info.parent_execution().workflow_id();
            desc.parent_run_id = info.parent_execution().run_id();
        }
    }

    co_return desc;
}

} // namespace temporalio::client
