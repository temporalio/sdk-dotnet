#include <temporalio/client/interceptors/client_interceptor.h>
#include <temporalio/client/temporal_client.h>
#include <temporalio/client/workflow_handle.h>

#include <stdexcept>
#include <utility>

namespace temporalio::client::interceptors {

// ── ClientOutboundInterceptor ───────────────────────────────────────────────

ClientOutboundInterceptor::ClientOutboundInterceptor(
    std::unique_ptr<ClientOutboundInterceptor> next)
    : next_(std::move(next)) {}

ClientOutboundInterceptor::~ClientOutboundInterceptor() = default;

ClientOutboundInterceptor& ClientOutboundInterceptor::next() {
    if (!next_) {
        throw std::logic_error("No next interceptor in chain");
    }
    return *next_;
}

coro::Task<WorkflowHandle> ClientOutboundInterceptor::start_workflow(
    StartWorkflowInput input) {
    co_return co_await next().start_workflow(std::move(input));
}

coro::Task<void> ClientOutboundInterceptor::signal_workflow(
    SignalWorkflowInput input) {
    co_await next().signal_workflow(std::move(input));
}

coro::Task<std::string> ClientOutboundInterceptor::query_workflow(
    QueryWorkflowInput input) {
    co_return co_await next().query_workflow(std::move(input));
}

coro::Task<std::string> ClientOutboundInterceptor::start_workflow_update(
    StartWorkflowUpdateInput input) {
    co_return co_await next().start_workflow_update(std::move(input));
}

coro::Task<std::string>
ClientOutboundInterceptor::start_update_with_start_workflow(
    StartUpdateWithStartWorkflowInput input) {
    co_return co_await next().start_update_with_start_workflow(
        std::move(input));
}

coro::Task<WorkflowExecutionDescription>
ClientOutboundInterceptor::describe_workflow(DescribeWorkflowInput input) {
    co_return co_await next().describe_workflow(std::move(input));
}

coro::Task<void> ClientOutboundInterceptor::cancel_workflow(
    CancelWorkflowInput input) {
    co_await next().cancel_workflow(std::move(input));
}

coro::Task<void> ClientOutboundInterceptor::terminate_workflow(
    TerminateWorkflowInput input) {
    co_await next().terminate_workflow(std::move(input));
}

coro::Task<WorkflowHistoryEventPage>
ClientOutboundInterceptor::fetch_workflow_history_event_page(
    FetchWorkflowHistoryEventPageInput input) {
    co_return co_await next().fetch_workflow_history_event_page(
        std::move(input));
}

coro::Task<WorkflowListPage> ClientOutboundInterceptor::list_workflows(
    ListWorkflowsInput input) {
    co_return co_await next().list_workflows(std::move(input));
}

coro::Task<WorkflowListPage>
ClientOutboundInterceptor::list_workflows_paginated(
    ListWorkflowsPaginatedInput input) {
    co_return co_await next().list_workflows_paginated(std::move(input));
}

coro::Task<WorkflowExecutionCount>
ClientOutboundInterceptor::count_workflows(CountWorkflowsInput input) {
    co_return co_await next().count_workflows(std::move(input));
}

} // namespace temporalio::client::interceptors
