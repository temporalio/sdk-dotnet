#include "temporalio/workflows/workflow.h"

#include <stdexcept>

namespace temporalio::workflows {

// Thread-local context pointer
thread_local WorkflowContext* WorkflowContext::current_ = nullptr;

static WorkflowContext& require_context() {
    auto* ctx = WorkflowContext::current();
    if (!ctx) {
        throw std::runtime_error(
            "Not in a workflow context. Workflow static methods can only be "
            "called from within a workflow.");
    }
    return *ctx;
}

bool Workflow::in_workflow() noexcept {
    return WorkflowContext::current() != nullptr;
}

const WorkflowInfo& Workflow::info() {
    return require_context().info();
}

std::stop_token Workflow::cancellation_token() {
    return require_context().cancellation_token();
}

bool Workflow::continue_as_new_suggested() {
    return require_context().continue_as_new_suggested();
}

bool Workflow::all_handlers_finished() {
    return require_context().all_handlers_finished();
}

std::chrono::system_clock::time_point Workflow::utc_now() {
    return require_context().utc_now();
}

std::mt19937& Workflow::random() {
    return require_context().random();
}

int Workflow::current_history_length() {
    return require_context().current_history_length();
}

int Workflow::current_history_size() {
    return require_context().current_history_size();
}

bool Workflow::is_replaying() {
    return require_context().is_replaying();
}

bool Workflow::patched(const std::string& patch_id) {
    return require_context().patched(patch_id);
}

void Workflow::deprecate_patch(const std::string& patch_id) {
    require_context().deprecate_patch(patch_id);
}

const WorkflowUpdateInfo* Workflow::current_update_info() {
    return require_context().current_update_info();
}

coro::Task<void> Workflow::delay(std::chrono::milliseconds duration,
                                   std::stop_token ct) {
    co_await require_context().start_timer(duration, std::move(ct));
}

coro::Task<bool> Workflow::wait_condition(
    std::function<bool()> condition,
    std::optional<std::chrono::milliseconds> timeout,
    std::stop_token ct) {
    co_return co_await require_context().register_condition(
        std::move(condition), timeout, std::move(ct));
}

coro::Task<std::any> Workflow::execute_activity(
    const std::string& activity_type,
    std::vector<std::any> args,
    const ActivityOptions& options) {
    co_return co_await require_context().schedule_activity(
        activity_type, std::move(args), options);
}

coro::Task<std::any> Workflow::execute_activity(
    const std::string& activity_type,
    std::any arg,
    const ActivityOptions& options) {
    std::vector<std::any> args;
    args.push_back(std::move(arg));
    co_return co_await require_context().schedule_activity(
        activity_type, std::move(args), options);
}

coro::Task<std::any> Workflow::execute_activity(
    const std::string& activity_type,
    const ActivityOptions& options) {
    co_return co_await require_context().schedule_activity(
        activity_type, {}, options);
}

}  // namespace temporalio::workflows
