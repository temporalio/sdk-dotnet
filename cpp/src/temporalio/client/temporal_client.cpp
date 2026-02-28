#include <temporalio/client/temporal_client.h>
#include <temporalio/client/temporal_connection.h>
#include <temporalio/converters/data_converter.h>

#include <cstdint>
#include <iomanip>
#include <memory>
#include <random>
#include <sstream>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

#include <temporal/api/workflowservice/v1/request_response.pb.h>
#include <temporal/api/taskqueue/v1/message.pb.h>
#include <temporal/api/workflow/v1/message.pb.h>
#include <temporal/api/enums/v1/workflow.pb.h>
#include <temporal/api/failure/v1/message.pb.h>
#include <temporal/api/update/v1/message.pb.h>
#include <google/protobuf/duration.pb.h>

#include "temporalio/client/rpc_helpers.h"

namespace temporalio::client {

namespace {

/// Set a google::protobuf::Duration from a std::chrono::milliseconds value.
void set_duration(google::protobuf::Duration* d, std::chrono::milliseconds ms) {
    auto secs = std::chrono::duration_cast<std::chrono::seconds>(ms);
    auto nanos = std::chrono::duration_cast<std::chrono::nanoseconds>(ms - secs);
    d->set_seconds(secs.count());
    d->set_nanos(static_cast<int32_t>(nanos.count()));
}

}  // namespace

// ── Impl ────────────────────────────────────────────────────────────────────

struct TemporalClient::Impl {
    std::shared_ptr<TemporalConnection> connection;
    TemporalClientOptions options;
    converters::DataConverter data_converter;
};

TemporalClient::TemporalClient(std::shared_ptr<TemporalConnection> connection,
                               TemporalClientOptions options)
    : impl_(std::make_unique<Impl>()) {
    impl_->connection = std::move(connection);
    impl_->data_converter = options.data_converter.has_value()
        ? std::move(*options.data_converter)
        : converters::DataConverter::default_instance();
    options.data_converter.reset();
    impl_->options = std::move(options);
}

TemporalClient::~TemporalClient() = default;

// rpc_call() and payload conversion helpers are in rpc_helpers.h

// ── Factory methods ─────────────────────────────────────────────────────────

coro::Task<std::shared_ptr<TemporalClient>> TemporalClient::connect(
    TemporalClientConnectOptions options) {
    auto conn =
        co_await TemporalConnection::connect(std::move(options.connection));
    co_return TemporalClient::create(std::move(conn),
                                     std::move(options.client));
}

std::shared_ptr<TemporalClient> TemporalClient::create(
    std::shared_ptr<TemporalConnection> connection,
    TemporalClientOptions options) {
    return std::shared_ptr<TemporalClient>(
        new TemporalClient(std::move(connection), std::move(options)));
}

// ── Accessors ───────────────────────────────────────────────────────────────

std::shared_ptr<TemporalConnection> TemporalClient::connection()
    const noexcept {
    return impl_->connection;
}

const std::string& TemporalClient::ns() const noexcept {
    return impl_->options.ns;
}

bridge::Client* TemporalClient::bridge_client() const noexcept {
    return impl_->connection ? impl_->connection->bridge_client() : nullptr;
}

const converters::DataConverter& TemporalClient::data_converter()
    const noexcept {
    return impl_->data_converter;
}

// ── Workflow operations ─────────────────────────────────────────────────────

coro::Task<WorkflowHandle> TemporalClient::start_workflow_impl(
    const std::string& workflow_type,
    std::vector<converters::Payload> args,
    const WorkflowOptions& options) {
    if (options.id.empty()) {
        throw std::invalid_argument("Workflow ID is required");
    }
    if (options.task_queue.empty()) {
        throw std::invalid_argument("Task queue is required");
    }

    auto* client = bridge_client();
    if (!client) {
        throw std::runtime_error("Not connected to Temporal server");
    }

    // Build StartWorkflowExecutionRequest using generated protobuf types
    temporal::api::workflowservice::v1::StartWorkflowExecutionRequest req;
    req.set_namespace_(impl_->options.ns);
    req.set_workflow_id(options.id);
    req.mutable_workflow_type()->set_name(workflow_type);
    req.mutable_task_queue()->set_name(options.task_queue);

    // Input payloads from DataConverter
    if (!args.empty()) {
        internal::set_proto_payloads(req.mutable_input(), args);
    }

    // Timeouts
    if (options.execution_timeout) {
        set_duration(req.mutable_workflow_execution_timeout(),
                     *options.execution_timeout);
    }
    if (options.run_timeout) {
        set_duration(req.mutable_workflow_run_timeout(),
                     *options.run_timeout);
    }
    if (options.task_timeout) {
        set_duration(req.mutable_workflow_task_timeout(),
                     *options.task_timeout);
    }

    // Identity
    req.set_identity(impl_->connection->options().identity.value_or("cpp-sdk"));

    // Request ID
    if (options.request_id) {
        req.set_request_id(*options.request_id);
    }

    // Workflow ID reuse policy
    if (options.id_reuse_policy !=
        common::WorkflowIdReusePolicy::kUnspecified) {
        req.set_workflow_id_reuse_policy(
            static_cast<temporal::api::enums::v1::WorkflowIdReusePolicy>(
                static_cast<int>(options.id_reuse_policy)));
    }

    // Cron schedule
    if (options.cron_schedule) {
        req.set_cron_schedule(*options.cron_schedule);
    }

    bridge::RpcCallOptions rpc_opts;
    rpc_opts.service = TemporalCoreRpcService::Workflow;
    rpc_opts.rpc = "StartWorkflowExecution";
    std::string serialized = req.SerializeAsString();
    rpc_opts.request = std::vector<uint8_t>(serialized.begin(), serialized.end());
    rpc_opts.retry = true;

    auto response = co_await internal::rpc_call(*client, std::move(rpc_opts));

    // Parse response
    temporal::api::workflowservice::v1::StartWorkflowExecutionResponse resp;
    resp.ParseFromArray(response.data(), static_cast<int>(response.size()));
    std::string run_id = resp.run_id();

    co_return WorkflowHandle(shared_from_this(), options.id,
                              run_id.empty() ? std::nullopt
                                             : std::optional(run_id),
                              run_id.empty() ? std::nullopt
                                             : std::optional(run_id));
}

WorkflowHandle TemporalClient::get_workflow_handle(
    const std::string& workflow_id,
    std::optional<std::string> run_id) {
    return WorkflowHandle(shared_from_this(), workflow_id, std::move(run_id));
}

coro::Task<void> TemporalClient::signal_workflow_impl(
    const std::string& workflow_id, const std::string& signal_name,
    std::vector<converters::Payload> args,
    std::optional<std::string> run_id) {
    auto* client = bridge_client();
    if (!client) {
        throw std::runtime_error("Not connected to Temporal server");
    }

    // Build SignalWorkflowExecutionRequest using generated protobuf types
    temporal::api::workflowservice::v1::SignalWorkflowExecutionRequest req;
    req.set_namespace_(impl_->options.ns);
    req.mutable_workflow_execution()->set_workflow_id(workflow_id);
    if (run_id) {
        req.mutable_workflow_execution()->set_run_id(*run_id);
    }
    req.set_signal_name(signal_name);
    if (!args.empty()) {
        internal::set_proto_payloads(req.mutable_input(), args);
    }
    req.set_identity(impl_->connection->options().identity.value_or("cpp-sdk"));

    bridge::RpcCallOptions rpc_opts;
    rpc_opts.service = TemporalCoreRpcService::Workflow;
    rpc_opts.rpc = "SignalWorkflowExecution";
    std::string serialized = req.SerializeAsString();
    rpc_opts.request = std::vector<uint8_t>(serialized.begin(), serialized.end());
    rpc_opts.retry = true;

    co_await internal::rpc_call(*client, std::move(rpc_opts));
}

coro::Task<std::string> TemporalClient::query_workflow(
    const std::string& workflow_id, const std::string& query_type,
    std::vector<converters::Payload> args,
    std::optional<std::string> run_id) {
    auto* client = bridge_client();
    if (!client) {
        throw std::runtime_error("Not connected to Temporal server");
    }

    // Build QueryWorkflowRequest using generated protobuf types
    temporal::api::workflowservice::v1::QueryWorkflowRequest req;
    req.set_namespace_(impl_->options.ns);
    req.mutable_execution()->set_workflow_id(workflow_id);
    if (run_id) {
        req.mutable_execution()->set_run_id(*run_id);
    }
    req.mutable_query()->set_query_type(query_type);
    if (!args.empty()) {
        internal::set_proto_payloads(req.mutable_query()->mutable_query_args(), args);
    }

    bridge::RpcCallOptions rpc_opts;
    rpc_opts.service = TemporalCoreRpcService::Workflow;
    rpc_opts.rpc = "QueryWorkflow";
    std::string serialized = req.SerializeAsString();
    rpc_opts.request = std::vector<uint8_t>(serialized.begin(), serialized.end());
    rpc_opts.retry = true;

    auto response = co_await internal::rpc_call(*client, std::move(rpc_opts));

    // Parse response and extract result
    temporal::api::workflowservice::v1::QueryWorkflowResponse resp;
    resp.ParseFromArray(response.data(), static_cast<int>(response.size()));
    if (resp.has_query_result() && resp.query_result().payloads_size() > 0) {
        const auto& payload = resp.query_result().payloads(0);
        co_return std::string(payload.data().begin(), payload.data().end());
    }
    co_return std::string(response.begin(), response.end());
}

coro::Task<void> TemporalClient::cancel_workflow(
    const std::string& workflow_id, std::optional<std::string> run_id) {
    auto* client = bridge_client();
    if (!client) {
        throw std::runtime_error("Not connected to Temporal server");
    }

    // Build RequestCancelWorkflowExecutionRequest using generated protobuf types
    temporal::api::workflowservice::v1::RequestCancelWorkflowExecutionRequest req;
    req.set_namespace_(impl_->options.ns);
    req.mutable_workflow_execution()->set_workflow_id(workflow_id);
    if (run_id) {
        req.mutable_workflow_execution()->set_run_id(*run_id);
    }
    req.set_identity(impl_->connection->options().identity.value_or("cpp-sdk"));

    bridge::RpcCallOptions rpc_opts;
    rpc_opts.service = TemporalCoreRpcService::Workflow;
    rpc_opts.rpc = "RequestCancelWorkflowExecution";
    std::string serialized = req.SerializeAsString();
    rpc_opts.request = std::vector<uint8_t>(serialized.begin(), serialized.end());
    rpc_opts.retry = true;

    co_await internal::rpc_call(*client, std::move(rpc_opts));
}

coro::Task<void> TemporalClient::terminate_workflow(
    const std::string& workflow_id, std::optional<std::string> reason,
    std::optional<std::string> run_id) {
    auto* client = bridge_client();
    if (!client) {
        throw std::runtime_error("Not connected to Temporal server");
    }

    // Build TerminateWorkflowExecutionRequest using generated protobuf types
    temporal::api::workflowservice::v1::TerminateWorkflowExecutionRequest req;
    req.set_namespace_(impl_->options.ns);
    req.mutable_workflow_execution()->set_workflow_id(workflow_id);
    if (run_id) {
        req.mutable_workflow_execution()->set_run_id(*run_id);
    }
    if (reason) {
        req.set_reason(*reason);
    }
    req.set_identity(impl_->connection->options().identity.value_or("cpp-sdk"));

    bridge::RpcCallOptions rpc_opts;
    rpc_opts.service = TemporalCoreRpcService::Workflow;
    rpc_opts.rpc = "TerminateWorkflowExecution";
    std::string serialized = req.SerializeAsString();
    rpc_opts.request = std::vector<uint8_t>(serialized.begin(), serialized.end());
    rpc_opts.retry = true;

    co_await internal::rpc_call(*client, std::move(rpc_opts));
}

coro::Task<std::vector<WorkflowExecution>> TemporalClient::list_workflows(
    const WorkflowListOptions& options) {
    auto* client = bridge_client();
    if (!client) {
        throw std::runtime_error("Not connected to Temporal server");
    }

    // Build ListWorkflowExecutionsRequest using generated protobuf types
    temporal::api::workflowservice::v1::ListWorkflowExecutionsRequest req;
    req.set_namespace_(impl_->options.ns);
    if (options.page_size) {
        req.set_page_size(*options.page_size);
    }
    if (options.query) {
        req.set_query(*options.query);
    }

    bridge::RpcCallOptions rpc_opts;
    rpc_opts.service = TemporalCoreRpcService::Workflow;
    rpc_opts.rpc = "ListWorkflowExecutions";
    std::string serialized = req.SerializeAsString();
    rpc_opts.request = std::vector<uint8_t>(serialized.begin(), serialized.end());
    rpc_opts.retry = true;

    auto response = co_await internal::rpc_call(*client, std::move(rpc_opts));

    temporal::api::workflowservice::v1::ListWorkflowExecutionsResponse resp;
    resp.ParseFromArray(response.data(), static_cast<int>(response.size()));

    std::vector<WorkflowExecution> results;
    for (const auto& exec_info : resp.executions()) {
        WorkflowExecution exec;
        if (exec_info.has_execution()) {
            exec.workflow_id = exec_info.execution().workflow_id();
            exec.run_id = exec_info.execution().run_id();
        }
        if (exec_info.has_type()) {
            exec.workflow_type = exec_info.type().name();
        }
        results.push_back(std::move(exec));
    }
    co_return results;
}

coro::Task<WorkflowExecutionCount> TemporalClient::count_workflows(
    const WorkflowCountOptions& options) {
    auto* client = bridge_client();
    if (!client) {
        throw std::runtime_error("Not connected to Temporal server");
    }

    // Build CountWorkflowExecutionsRequest using generated protobuf types
    temporal::api::workflowservice::v1::CountWorkflowExecutionsRequest req;
    req.set_namespace_(impl_->options.ns);
    if (options.query) {
        req.set_query(*options.query);
    }

    bridge::RpcCallOptions rpc_opts;
    rpc_opts.service = TemporalCoreRpcService::Workflow;
    rpc_opts.rpc = "CountWorkflowExecutions";
    std::string serialized = req.SerializeAsString();
    rpc_opts.request = std::vector<uint8_t>(serialized.begin(), serialized.end());
    rpc_opts.retry = true;

    auto response = co_await internal::rpc_call(*client, std::move(rpc_opts));

    temporal::api::workflowservice::v1::CountWorkflowExecutionsResponse resp;
    resp.ParseFromArray(response.data(), static_cast<int>(response.size()));

    WorkflowExecutionCount result;
    result.count = resp.count();
    co_return result;
}

} // namespace temporalio::client
