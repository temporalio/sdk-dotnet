#pragma once

/// @file client_interceptor.h
/// @brief Client-side interceptor chain (IClientInterceptor, ClientOutboundInterceptor).

#include <temporalio/coro/task.h>
#include <temporalio/client/workflow_options.h>

#include <memory>
#include <optional>
#include <string>
#include <unordered_map>
#include <vector>

namespace temporalio::client {
class WorkflowHandle;
struct WorkflowExecution;
struct WorkflowExecutionCount;
struct WorkflowExecutionDescription;
} // namespace temporalio::client

namespace temporalio::client::interceptors {

// ── Input types ─────────────────────────────────────────────────────────────

/// Input for starting a workflow.
struct StartWorkflowInput {
    /// Workflow type name to start.
    std::string workflow;

    /// Serialized arguments for the workflow.
    std::vector<std::string> args;

    /// Options passed in to start.
    WorkflowOptions options;

    /// Headers to include. Encoded using the codec before sent to the server.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for signaling a workflow.
struct SignalWorkflowInput {
    /// Workflow ID.
    std::string id;

    /// Workflow run ID if any.
    std::optional<std::string> run_id;

    /// Signal name.
    std::string signal;

    /// Signal arguments (serialized).
    std::vector<std::string> args;

    /// Options if any.
    std::optional<WorkflowSignalOptions> options;

    /// Headers if any. Encoded using the codec before sent to the server.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for querying a workflow.
struct QueryWorkflowInput {
    /// Workflow ID.
    std::string id;

    /// Workflow run ID if any.
    std::optional<std::string> run_id;

    /// Query name.
    std::string query;

    /// Query arguments (serialized).
    std::vector<std::string> args;

    /// Options if any.
    std::optional<WorkflowQueryOptions> options;

    /// Headers if any. Encoded using the codec before sent to the server.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for starting a workflow update.
struct StartWorkflowUpdateInput {
    /// Workflow ID.
    std::string id;

    /// Workflow run ID if any.
    std::optional<std::string> run_id;

    /// Workflow first execution run ID if any.
    std::optional<std::string> first_execution_run_id;

    /// Update name.
    std::string update;

    /// Update arguments (serialized).
    std::vector<std::string> args;

    /// Headers if any. Encoded using the codec before sent to the server.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for starting an update with a workflow start.
struct StartUpdateWithStartWorkflowInput {
    /// Update name.
    std::string update;

    /// Update arguments (serialized).
    std::vector<std::string> args;

    /// Headers if any for the update. Encoded using the codec before sent to
    /// the server. Note these are the update headers, start headers are in the
    /// start operation.
    std::unordered_map<std::string, std::string> headers;
};

/// Input for describing a workflow.
struct DescribeWorkflowInput {
    /// Workflow ID.
    std::string id;

    /// Workflow run ID if any.
    std::optional<std::string> run_id;

    /// Options passed in to describe.
    std::optional<WorkflowDescribeOptions> options;
};

/// Input for canceling a workflow.
struct CancelWorkflowInput {
    /// Workflow ID.
    std::string id;

    /// Workflow run ID if any.
    std::optional<std::string> run_id;

    /// Run that started the workflow chain to cancel.
    std::optional<std::string> first_execution_run_id;

    /// Options passed in to cancel.
    std::optional<WorkflowCancelOptions> options;
};

/// Input for terminating a workflow.
struct TerminateWorkflowInput {
    /// Workflow ID.
    std::string id;

    /// Workflow run ID if any.
    std::optional<std::string> run_id;

    /// Run that started the workflow chain to terminate.
    std::optional<std::string> first_execution_run_id;

    /// Reason for termination.
    std::optional<std::string> reason;

    /// Options passed in to terminate.
    std::optional<WorkflowTerminateOptions> options;
};

/// Input for fetching a page of workflow history events.
struct FetchWorkflowHistoryEventPageInput {
    /// ID of the workflow.
    std::string id;

    /// Optional run ID of the workflow.
    std::optional<std::string> run_id;

    /// Optional page size.
    std::optional<int> page_size;

    /// Next page token if any to continue pagination.
    std::optional<std::vector<uint8_t>> next_page_token;

    /// If true, wait for new events before returning.
    bool wait_new_event{false};

    /// If true, skips archival when fetching.
    bool skip_archival{false};
};

/// Input for listing workflows.
struct ListWorkflowsInput {
    /// Visibility query string.
    std::string query;

    /// Options passed in to list.
    std::optional<WorkflowListOptions> options;
};

/// Input for listing workflows with pagination.
struct ListWorkflowsPaginatedInput {
    /// Visibility query string.
    std::string query;

    /// Next page token from a previous response. Empty if first page.
    std::optional<std::vector<uint8_t>> next_page_token;

    /// Options passed in to list.
    std::optional<WorkflowListOptions> options;
};

/// Input for counting workflows.
struct CountWorkflowsInput {
    /// Count query string.
    std::string query;

    /// Options passed in to count.
    std::optional<WorkflowCountOptions> options;
};

/// A page of workflow history events.
struct WorkflowHistoryEventPage {
    /// Serialized events on this page.
    std::vector<std::string> events;

    /// Token for the next page, empty if no more pages.
    std::vector<uint8_t> next_page_token;
};

/// A page of workflow list results.
struct WorkflowListPage {
    /// Workflow executions on this page.
    std::vector<WorkflowExecution> workflows;

    /// Token for the next page, empty if no more pages.
    std::vector<uint8_t> next_page_token;
};

// ── ClientOutboundInterceptor ───────────────────────────────────────────────

/// Base class for client outbound interceptors.
/// Each virtual method defaults to delegating to the next interceptor in the
/// chain. Override specific methods to intercept calls.
class ClientOutboundInterceptor {
public:
    /// Construct with next interceptor in the chain.
    explicit ClientOutboundInterceptor(
        std::unique_ptr<ClientOutboundInterceptor> next);

    virtual ~ClientOutboundInterceptor();

    // Non-copyable
    ClientOutboundInterceptor(const ClientOutboundInterceptor&) = delete;
    ClientOutboundInterceptor& operator=(const ClientOutboundInterceptor&) =
        delete;

    // Movable
    ClientOutboundInterceptor(ClientOutboundInterceptor&&) noexcept = default;
    ClientOutboundInterceptor& operator=(ClientOutboundInterceptor&&) noexcept =
        default;

    /// Intercept start workflow calls.
    virtual coro::Task<WorkflowHandle> start_workflow(
        StartWorkflowInput input);

    /// Intercept signal workflow calls.
    virtual coro::Task<void> signal_workflow(SignalWorkflowInput input);

    /// Intercept query workflow calls.
    virtual coro::Task<std::string> query_workflow(
        QueryWorkflowInput input);

    /// Intercept start workflow update calls.
    virtual coro::Task<std::string> start_workflow_update(
        StartWorkflowUpdateInput input);

    /// Intercept start update with start workflow calls.
    virtual coro::Task<std::string> start_update_with_start_workflow(
        StartUpdateWithStartWorkflowInput input);

    /// Intercept describe workflow calls.
    virtual coro::Task<WorkflowExecutionDescription> describe_workflow(
        DescribeWorkflowInput input);

    /// Intercept cancel workflow calls.
    virtual coro::Task<void> cancel_workflow(CancelWorkflowInput input);

    /// Intercept terminate workflow calls.
    virtual coro::Task<void> terminate_workflow(
        TerminateWorkflowInput input);

    /// Intercept a history event page fetch.
    virtual coro::Task<WorkflowHistoryEventPage>
    fetch_workflow_history_event_page(
        FetchWorkflowHistoryEventPageInput input);

    /// Intercept listing workflows.
    virtual coro::Task<WorkflowListPage> list_workflows(
        ListWorkflowsInput input);

    /// Intercept page fetch for list workflows calls.
    virtual coro::Task<WorkflowListPage> list_workflows_paginated(
        ListWorkflowsPaginatedInput input);

    /// Intercept counting workflows.
    virtual coro::Task<WorkflowExecutionCount> count_workflows(
        CountWorkflowsInput input);

protected:
    /// Get the next interceptor in the chain.
    ClientOutboundInterceptor& next();

private:
    std::unique_ptr<ClientOutboundInterceptor> next_;
};

// ── IClientInterceptor ──────────────────────────────────────────────────────

/// Interface for client interceptor factories.
/// Implement this to create interceptors that wrap the outbound call chain.
class IClientInterceptor {
public:
    virtual ~IClientInterceptor() = default;

    /// Create a client outbound interceptor to intercept calls.
    /// @param next The next interceptor in the chain to wrap.
    /// @return Created interceptor wrapping the next interceptor.
    virtual std::unique_ptr<ClientOutboundInterceptor> intercept_client(
        std::unique_ptr<ClientOutboundInterceptor> next) = 0;
};

} // namespace temporalio::client::interceptors
