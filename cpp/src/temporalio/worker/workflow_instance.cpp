#include "temporalio/worker/workflow_instance.h"

#include <algorithm>
#include <stdexcept>
#include <utility>

#include "temporalio/exceptions/temporal_exception.h"

namespace temporalio::worker {

WorkflowInstance::WorkflowInstance(Config config)
    : definition_(std::move(config.definition)),
      info_(std::move(config.info)),
      data_converter_(std::move(config.data_converter)),
      random_(static_cast<std::mt19937::result_type>(config.randomness_seed)) {}

WorkflowInstance::~WorkflowInstance() = default;

std::vector<WorkflowInstance::Command> WorkflowInstance::activate(
    const std::vector<Job>& jobs,
    const ActivationMetadata& metadata) {
    current_time_ = metadata.timestamp;
    is_replaying_ = metadata.is_replaying;
    current_history_length_ = metadata.history_length;
    current_history_size_ = static_cast<int>(metadata.history_size_bytes);
    continue_as_new_suggested_ = metadata.continue_as_new_suggested;
    return activate(jobs);
}

std::vector<WorkflowInstance::Command> WorkflowInstance::activate(
    const std::vector<Job>& jobs) {
    commands_.clear();
    current_activation_exception_ = nullptr;

    // Set this instance as the current workflow context
    workflows::WorkflowContextScope scope(this);

    try {
        // Process each job
        for (const auto& job : jobs) {
            switch (job.type) {
                case JobType::kStartWorkflow:
                    handle_start_workflow(job);
                    break;
                case JobType::kFireTimer:
                    handle_fire_timer(job);
                    break;
                case JobType::kResolveActivity:
                    handle_resolve_activity(job);
                    break;
                case JobType::kSignalWorkflow:
                    handle_signal_workflow(job);
                    break;
                case JobType::kQueryWorkflow:
                    handle_query_workflow(job);
                    break;
                case JobType::kResolveChildWorkflow:
                    handle_resolve_child_workflow(job);
                    break;
                case JobType::kCancelWorkflow:
                    handle_cancel_workflow(job);
                    break;
                case JobType::kDoUpdate:
                    handle_do_update(job);
                    break;
                case JobType::kResolveSignalExternalWorkflow:
                    handle_resolve_signal_external_workflow(job);
                    break;
                case JobType::kResolveRequestCancelExternalWorkflow:
                    handle_resolve_request_cancel_external_workflow(job);
                    break;
                case JobType::kNotifyHasPatch:
                    handle_notify_has_patch(job);
                    break;
                case JobType::kResolveNexusOperation:
                    handle_resolve_nexus_operation(job);
                    break;
                case JobType::kUpdateWorkflow:
                    // Legacy update type, handled through kDoUpdate
                    break;
            }
        }

        // Modern event loop: initialize the workflow AFTER all jobs are
        // applied (not during handle_start_workflow). This matches C# where
        // InitializeWorkflow() is called after the job loop.
        if (!workflow_initialized_ && workflow_started_) {
            initialize_workflow();
        }

        // Run the event loop (modern mode: all jobs applied first, then drain)
        bool has_non_query =
            std::any_of(jobs.begin(), jobs.end(), [](const Job& j) {
                return j.type != JobType::kQueryWorkflow;
            });
        run_once(has_non_query);
    } catch (const std::exception& e) {
        // Activation-level failure
        Command fail_cmd;
        fail_cmd.type = CommandType::kFailWorkflow;
        fail_cmd.data = std::string(e.what());
        commands_.push_back(std::move(fail_cmd));
    }

    return std::move(commands_);
}

void WorkflowInstance::run_once(bool check_conds) {
    // Mirrors C# RunOnce(): outer loop keeps going as long as the scheduler
    // has work. Checking conditions may complete TCS's which enqueue more
    // coroutines, so the outer loop ensures chain reactions are fully drained.
    bool has_work = true;
    while (has_work) {
        // Inner loop: drain all queued coroutines
        while (scheduler_.drain()) {
            if (current_activation_exception_) {
                std::rethrow_exception(current_activation_exception_);
            }
        }

        // Check conditions if requested. Completing a condition's TCS may
        // schedule new tasks, which will cause the outer loop to continue.
        has_work = false;
        if (check_conds && !conditions_.empty()) {
            // In modern event loop logic, break after the first condition
            // is resolved (matching C# behavior). The outer loop will re-drain
            // and re-check, ensuring one condition is resolved per iteration.
            auto it = conditions_.begin();
            while (it != conditions_.end()) {
                auto& [pred, tcs] = *it;
                if (pred()) {
                    tcs->try_set_result(true);

                    // If this condition had a timeout timer, cancel it.
                    auto ct_it = condition_to_timeout_.find(tcs.get());
                    if (ct_it != condition_to_timeout_.end()) {
                        uint32_t timeout_seq = ct_it->second;
                        pending_timers_.erase(timeout_seq);
                        timeout_to_condition_.erase(timeout_seq);
                        // Emit a CancelTimer command.
                        Command cancel_cmd;
                        cancel_cmd.type = CommandType::kCancelTimer;
                        cancel_cmd.seq = timeout_seq;
                        cancel_cmd.data = CancelTimerData{timeout_seq};
                        commands_.push_back(std::move(cancel_cmd));
                        condition_to_timeout_.erase(ct_it);
                    }

                    it = conditions_.erase(it);
                    // A condition was met -- its TCS completion may have
                    // enqueued new tasks. Break to re-drain before checking
                    // more conditions (modern event loop behavior).
                    has_work = true;
                    break;
                } else {
                    ++it;
                }
            }
        }
    }
}

std::stop_token WorkflowInstance::cancellation_token() const {
    return cancellation_source_.token();
}

bool WorkflowInstance::all_handlers_finished() const {
    return in_progress_handler_count_ == 0;
}

const workflows::WorkflowUpdateInfo*
WorkflowInstance::current_update_info() const {
    return current_update_info_ ? &*current_update_info_ : nullptr;
}

bool WorkflowInstance::patched(const std::string& patch_id) {
    // Check memoized first
    auto memo_it = patches_memoized_.find(patch_id);
    if (memo_it != patches_memoized_.end()) {
        return memo_it->second;
    }

    // If notified, it's patched
    bool is_patched = patches_notified_.count(patch_id) > 0;
    if (!is_patched && !is_replaying_) {
        // During non-replay, patched() returns true and emits a command
        is_patched = true;
        Command cmd;
        cmd.type = CommandType::kSetPatchMarker;
        cmd.data = patch_id;
        commands_.push_back(std::move(cmd));
    }
    patches_memoized_[patch_id] = is_patched;
    return is_patched;
}

void WorkflowInstance::deprecate_patch(const std::string& patch_id) {
    // During non-replay, emit the marker
    if (!is_replaying_) {
        Command cmd;
        cmd.type = CommandType::kSetPatchMarker;
        cmd.data = patch_id;
        commands_.push_back(std::move(cmd));
    }
    patches_memoized_[patch_id] = true;
}

coro::Task<void> WorkflowInstance::start_timer(
    std::chrono::milliseconds duration,
    std::stop_token ct) {
    // Default to the workflow's cancellation token if none provided.
    if (!ct.stop_possible()) {
        ct = cancellation_source_.token();
    }

    // Allocate a sequence number for this timer.
    uint32_t seq = ++timer_counter_;

    // Create a TCS with resume callback routed through the scheduler.
    auto tcs = std::make_shared<coro::TaskCompletionSource<std::any>>(
        make_resume_callback());
    pending_timers_[seq] = tcs;

    // Emit the StartTimer command.
    Command cmd;
    cmd.type = CommandType::kStartTimer;
    cmd.seq = seq;
    cmd.data = StartTimerData{seq, duration};
    commands_.push_back(std::move(cmd));

    // If a cancellation token was provided, register a callback to cancel
    // the timer when cancellation is requested.
    std::optional<std::stop_callback<std::function<void()>>> cancel_cb;
    if (ct.stop_possible()) {
        cancel_cb.emplace(ct, std::function<void()>([this, seq, tcs]() {
            // Remove from pending and cancel the TCS.
            pending_timers_.erase(seq);
            tcs->try_set_exception(std::make_exception_ptr(
                exceptions::CanceledFailureException("Timer cancelled")));

            // Emit a CancelTimer command so the server knows.
            Command cancel_cmd;
            cancel_cmd.type = CommandType::kCancelTimer;
            cancel_cmd.seq = seq;
            cancel_cmd.data = CancelTimerData{seq};
            commands_.push_back(std::move(cancel_cmd));
        }));
    }

    // Suspend until the timer fires (handle_fire_timer resolves the TCS)
    // or cancellation triggers.
    co_await tcs->task();
}

coro::Task<std::any> WorkflowInstance::schedule_activity(
    const std::string& activity_type,
    std::vector<std::any> args,
    const workflows::ActivityOptions& options) {
    // Allocate a sequence number for this activity.
    uint32_t seq = ++activity_counter_;

    // Create a TCS with resume callback routed through the scheduler.
    auto tcs = std::make_shared<coro::TaskCompletionSource<std::any>>(
        make_resume_callback());
    pending_activities_[seq] = tcs;

    // Build the ScheduleActivity command data.
    ScheduleActivityData data;
    data.seq = seq;
    data.activity_type = activity_type;
    data.task_queue = options.task_queue.value_or(info_.task_queue);
    data.args = args;
    data.schedule_to_close_timeout = options.schedule_to_close_timeout;
    data.schedule_to_start_timeout = options.schedule_to_start_timeout;
    data.start_to_close_timeout = options.start_to_close_timeout;
    data.heartbeat_timeout = options.heartbeat_timeout;
    data.retry_policy = options.retry_policy;
    data.cancellation_type = options.cancellation_type;
    data.activity_id = options.activity_id;

    // Emit the ScheduleActivity command.
    Command cmd;
    cmd.type = CommandType::kScheduleActivity;
    cmd.seq = seq;
    cmd.data = std::move(data);
    commands_.push_back(std::move(cmd));

    // If a cancellation token was provided, register a callback to cancel
    // the activity when cancellation is requested.
    std::stop_token ct = options.cancellation_token.value_or(cancellation_source_.token());
    std::optional<std::stop_callback<std::function<void()>>> cancel_cb;
    if (ct.stop_possible()) {
        cancel_cb.emplace(ct, std::function<void()>([this, seq, tcs]() {
            // Remove from pending and cancel the TCS.
            pending_activities_.erase(seq);
            tcs->try_set_exception(std::make_exception_ptr(
                exceptions::CanceledFailureException("Activity cancelled")));

            // Emit a RequestCancelActivity command so the server knows.
            Command cancel_cmd;
            cancel_cmd.type = CommandType::kRequestCancelActivity;
            cancel_cmd.seq = seq;
            cancel_cmd.data = RequestCancelActivityData{seq};
            commands_.push_back(std::move(cancel_cmd));
        }));
    }

    // Suspend until the activity resolves (handle_resolve_activity resolves
    // the TCS) or cancellation triggers.
    auto resolution_any = co_await tcs->task();

    // Inspect the resolution and return result or throw.
    const auto& resolution =
        std::any_cast<const ActivityResolution&>(resolution_any);
    switch (resolution.status) {
        case ResolutionStatus::kCompleted:
            co_return resolution.result;
        case ResolutionStatus::kFailed:
            throw exceptions::ActivityFailureException(
                resolution.failure, activity_type,
                std::to_string(seq));
        case ResolutionStatus::kCancelled:
            throw exceptions::CanceledFailureException(
                resolution.failure.empty() ? "Activity cancelled"
                                           : resolution.failure);
    }

    // Should not reach here
    throw std::runtime_error("Unknown activity resolution status");
}

coro::Task<bool> WorkflowInstance::register_condition(
    std::function<bool()> condition,
    std::optional<std::chrono::milliseconds> timeout,
    std::stop_token ct) {
    // Default to the workflow's cancellation token if none provided.
    if (!ct.stop_possible()) {
        ct = cancellation_source_.token();
    }

    // If the condition is already true, return immediately.
    if (condition && condition()) {
        co_return true;
    }

    // Create a TCS for the condition with resume callback routed through
    // the scheduler to maintain deterministic execution order.
    auto cond_tcs = std::make_shared<coro::TaskCompletionSource<bool>>(
        make_resume_callback());

    // Register the condition in the conditions list. The run_once() loop
    // checks these conditions after each scheduler drain.
    conditions_.push_back({condition, cond_tcs});

    // If a timeout is specified, start a timer. When the timer fires,
    // handle_fire_timer() will check timeout_to_condition_ and resolve
    // the condition TCS with false.
    std::optional<uint32_t> timeout_timer_seq;
    if (timeout.has_value()) {
        uint32_t seq = ++timer_counter_;
        timeout_timer_seq = seq;

        // Create a timer TCS. Nobody co_awaits this directly; it exists
        // so handle_fire_timer() can find and resolve it, which triggers
        // the timeout_to_condition_ lookup to resolve the condition TCS.
        auto timer_tcs = std::make_shared<coro::TaskCompletionSource<std::any>>(
            make_resume_callback());
        pending_timers_[seq] = timer_tcs;

        // Bidirectional mapping between timeout timer and condition.
        timeout_to_condition_[seq] = cond_tcs;
        condition_to_timeout_[cond_tcs.get()] = seq;

        // Emit the StartTimer command for the timeout.
        Command cmd;
        cmd.type = CommandType::kStartTimer;
        cmd.seq = seq;
        cmd.data = StartTimerData{seq, *timeout};
        commands_.push_back(std::move(cmd));
    }

    // If a cancellation token was provided, register a callback to cancel
    // the condition wait and any associated timeout timer.
    std::optional<std::stop_callback<std::function<void()>>> cancel_cb;
    if (ct.stop_possible()) {
        cancel_cb.emplace(ct, std::function<void()>([this, cond_tcs, timeout_timer_seq]() {
            // Remove the condition from the deque.
            for (auto it = conditions_.begin(); it != conditions_.end(); ++it) {
                if (it->second == cond_tcs) {
                    conditions_.erase(it);
                    break;
                }
            }
            // Cancel the timeout timer if any.
            if (timeout_timer_seq.has_value()) {
                pending_timers_.erase(*timeout_timer_seq);
                timeout_to_condition_.erase(*timeout_timer_seq);
                condition_to_timeout_.erase(cond_tcs.get());

                Command cancel_cmd;
                cancel_cmd.type = CommandType::kCancelTimer;
                cancel_cmd.seq = *timeout_timer_seq;
                cancel_cmd.data = CancelTimerData{*timeout_timer_seq};
                commands_.push_back(std::move(cancel_cmd));
            }
            cond_tcs->try_set_exception(std::make_exception_ptr(
                exceptions::CanceledFailureException("Wait condition cancelled")));
        }));
    }

    // Suspend until either:
    // - The condition becomes true (run_once resolves cond_tcs with true)
    // - The timeout timer fires (handle_fire_timer resolves cond_tcs with false)
    // - Cancellation triggers (cancel_cb resolves cond_tcs with exception)
    bool result = co_await cond_tcs->task();
    co_return result;
}

void WorkflowInstance::handle_start_workflow(const Job& job) {
    if (workflow_started_) {
        return;
    }
    workflow_started_ = true;

    // Extract workflow arguments from the job data.
    // The workflow_worker's convert_jobs() stores args as
    // std::vector<std::any> (each containing a protobuf Payload).
    try {
        workflow_args_ =
            std::any_cast<std::vector<std::any>>(job.data);
    } catch (const std::bad_any_cast&) {
        // No args or unexpected type -- leave empty
    }

    // Create the workflow instance but do NOT start the run coroutine yet.
    // In modern event loop mode, InitializeWorkflow() is called AFTER all
    // jobs are applied, not during handle_start_workflow(). This matches the
    // C# pattern where instance creation and args decoding happen here, but
    // the actual coroutine start happens in InitializeWorkflow().
    instance_ = definition_->create_instance();
    if (!instance_) {
        throw std::runtime_error(
            "Failed to create workflow instance for type: " +
            definition_->name());
    }
}

void WorkflowInstance::initialize_workflow() {
    if (workflow_initialized_) {
        throw std::runtime_error("Workflow unexpectedly initialized");
    }
    workflow_initialized_ = true;

    // Start the run coroutine
    auto& run_func = definition_->run_func();
    if (!run_func) {
        throw std::runtime_error("Workflow definition missing run function");
    }

    // Wrap in run_top_level to catch workflow exceptions and convert
    // to commands (ContinueAsNew, Fail, Cancel).
    // is_handler=false: the main workflow run does not count as a handler.
    auto task =
        run_top_level(run_func, instance_.get(),
                      std::move(workflow_args_), /*is_handler=*/false);
    // Schedule the task's initial suspension point
    scheduler_.schedule(task.handle());
    // Store the task to keep the coroutine alive (prevents UAF when
    // the scheduler holds a handle to the coroutine frame).
    running_tasks_.push_back(std::move(task));
}

void WorkflowInstance::handle_fire_timer(const Job& job) {
    auto seq = std::any_cast<uint32_t>(job.data);
    auto it = pending_timers_.find(seq);
    if (it != pending_timers_.end()) {
        it->second->try_set_result(std::any{});
        pending_timers_.erase(it);
    }

    // Check if this timer was a timeout for a wait_condition.
    // If so, resolve the condition TCS with false (timeout expired) and
    // remove the condition from the conditions deque.
    auto cond_it = timeout_to_condition_.find(seq);
    if (cond_it != timeout_to_condition_.end()) {
        auto& cond_tcs = cond_it->second;
        // Remove the associated condition from the conditions deque.
        for (auto dit = conditions_.begin(); dit != conditions_.end(); ++dit) {
            if (dit->second == cond_tcs) {
                conditions_.erase(dit);
                break;
            }
        }
        // Clean up the reverse mapping.
        condition_to_timeout_.erase(cond_tcs.get());
        // Resolve with false = timeout expired.
        cond_tcs->try_set_result(false);
        timeout_to_condition_.erase(cond_it);
    }
}

void WorkflowInstance::handle_resolve_activity(const Job& job) {
    // Accept both ActivityResolution (new) and uint32_t (legacy tests)
    uint32_t seq;
    std::any resolution_data;

    try {
        const auto& resolution =
            std::any_cast<const ActivityResolution&>(job.data);
        seq = resolution.seq;
        resolution_data = std::any(resolution);
    } catch (const std::bad_any_cast&) {
        seq = std::any_cast<uint32_t>(job.data);
        // Legacy path: wrap in a completed resolution
        ActivityResolution res;
        res.seq = seq;
        res.status = ResolutionStatus::kCompleted;
        resolution_data = std::any(res);
    }

    auto it = pending_activities_.find(seq);
    if (it != pending_activities_.end()) {
        it->second->try_set_result(std::move(resolution_data));
        pending_activities_.erase(it);
    }
}

void WorkflowInstance::handle_signal_workflow(const Job& job) {
    // Get signal name and args from job data
    auto& signal_data =
        std::any_cast<const std::pair<std::string, std::vector<std::any>>&>(
            job.data);
    const auto& signal_name = signal_data.first;
    const auto& args = signal_data.second;

    // Look up signal handler.
    // Signal handlers return Task<void> so we wrap them through
    // run_top_level to handle exceptions properly.
    auto& signals = definition_->signals();
    auto it = signals.find(signal_name);

    // Adapter: wraps a Task<void> handler as Task<any> for run_top_level
    auto wrap_void_handler =
        [](std::function<coro::Task<void>(void*, std::vector<std::any>)> fn)
        -> std::function<coro::Task<std::any>(void*, std::vector<std::any>)> {
        return [fn = std::move(fn)](void* inst,
                                    std::vector<std::any> a)
            -> coro::Task<std::any> {
            co_await fn(inst, std::move(a));
            co_return std::any{};
        };
    };

    if (it != signals.end()) {
        ++in_progress_handler_count_;
        auto task = run_top_level(
            wrap_void_handler(it->second.handler), instance_.get(), args,
            /*is_handler=*/true);
        scheduler_.schedule(task.handle());
        running_tasks_.push_back(std::move(task));
    } else if (definition_->dynamic_signal()) {
        ++in_progress_handler_count_;
        auto task = run_top_level(
            wrap_void_handler(definition_->dynamic_signal()->handler),
            instance_.get(), args, /*is_handler=*/true);
        scheduler_.schedule(task.handle());
        running_tasks_.push_back(std::move(task));
    }
    // Else: signal is buffered/ignored
}

void WorkflowInstance::handle_query_workflow(const Job& job) {
    // Queries are handled synchronously -- they don't go through the
    // scheduler. Results use kRespondQuery / kRespondQueryFailed and
    // are routed to the query response section of the activation
    // completion, not the workflow command list.
    //
    // Each response carries a QueryId for correlation with the server.

    // Accept both QueryWorkflowData (new) and pair (legacy tests)
    const QueryWorkflowData* query_ptr = nullptr;
    QueryWorkflowData query_from_pair;

    try {
        query_ptr = &std::any_cast<const QueryWorkflowData&>(job.data);
    } catch (const std::bad_any_cast&) {
        // Legacy pair format for backward compatibility
        auto& pair_data =
            std::any_cast<const std::pair<std::string, std::vector<std::any>>&>(
                job.data);
        query_from_pair.query_name = pair_data.first;
        query_from_pair.args = pair_data.second;
        query_ptr = &query_from_pair;
    }

    const auto& query_id = query_ptr->query_id;
    const auto& query_name = query_ptr->query_name;
    const auto& args = query_ptr->args;

    auto& queries = definition_->queries();
    auto it = queries.find(query_name);
    if (it != queries.end()) {
        try {
            auto result = it->second.handler(instance_.get(), args);
            Command cmd;
            cmd.type = CommandType::kRespondQuery;
            cmd.data = QueryResponseData{query_id, std::move(result), {}};
            commands_.push_back(std::move(cmd));
        } catch (const std::exception& e) {
            Command cmd;
            cmd.type = CommandType::kRespondQueryFailed;
            cmd.data = QueryResponseData{query_id, {}, e.what()};
            commands_.push_back(std::move(cmd));
        }
    } else if (definition_->dynamic_query()) {
        try {
            auto result =
                definition_->dynamic_query()->handler(instance_.get(), args);
            Command cmd;
            cmd.type = CommandType::kRespondQuery;
            cmd.data = QueryResponseData{query_id, std::move(result), {}};
            commands_.push_back(std::move(cmd));
        } catch (const std::exception& e) {
            Command cmd;
            cmd.type = CommandType::kRespondQueryFailed;
            cmd.data = QueryResponseData{query_id, {}, e.what()};
            commands_.push_back(std::move(cmd));
        }
    } else {
        // Unknown query -- respond with failure
        Command cmd;
        cmd.type = CommandType::kRespondQueryFailed;
        cmd.data = QueryResponseData{
            query_id, {},
            "Query handler for '" + query_name +
                "' is not registered on this workflow"};
        commands_.push_back(std::move(cmd));
    }
}

void WorkflowInstance::handle_cancel_workflow(const Job& /*job*/) {
    cancellation_source_.cancel();
}

void WorkflowInstance::handle_resolve_child_workflow(const Job& job) {
    // Accept both ChildWorkflowResolution (new) and uint32_t (legacy tests)
    uint32_t seq;
    std::any resolution_data;

    try {
        const auto& resolution =
            std::any_cast<const ChildWorkflowResolution&>(job.data);
        seq = resolution.seq;
        resolution_data = std::any(resolution);
    } catch (const std::bad_any_cast&) {
        seq = std::any_cast<uint32_t>(job.data);
        // Legacy path: wrap in a completed resolution
        ChildWorkflowResolution res;
        res.seq = seq;
        res.status = ResolutionStatus::kCompleted;
        resolution_data = std::any(res);
    }

    auto it = pending_child_workflows_.find(seq);
    if (it != pending_child_workflows_.end()) {
        it->second->try_set_result(std::move(resolution_data));
        pending_child_workflows_.erase(it);
    }
}

void WorkflowInstance::handle_do_update(const Job& job) {
    // Try DoUpdateData first (new format), fall back to pair (legacy tests)
    const DoUpdateData* update_ptr = nullptr;
    DoUpdateData update_from_pair;

    try {
        update_ptr = &std::any_cast<const DoUpdateData&>(job.data);
    } catch (const std::bad_any_cast&) {
        // Fall back to legacy pair format for backward compatibility
        auto& pair_data =
            std::any_cast<const std::pair<std::string, std::vector<std::any>>&>(
                job.data);
        update_from_pair.name = pair_data.first;
        update_from_pair.args = pair_data.second;
        update_from_pair.run_validator = true;
        update_ptr = &update_from_pair;
    }

    const auto& update = *update_ptr;
    const auto& update_name = update.name;
    const auto& args = update.args;

    // Look up update definition
    auto& updates = definition_->updates();
    auto it = updates.find(update_name);
    const workflows::WorkflowUpdateDefinition* defn = nullptr;

    if (it != updates.end()) {
        defn = &it->second;
    } else if (definition_->dynamic_update()) {
        defn = &*definition_->dynamic_update();
    }

    if (!defn) {
        // Unknown update -- reject
        Command cmd;
        cmd.type = CommandType::kUpdateRejected;
        UpdateResponseData resp;
        resp.protocol_instance_id = update.protocol_instance_id;
        resp.update_name = update_name;
        resp.error = "Update handler for " + update_name +
                     " expected but not found";
        cmd.data = std::move(resp);
        commands_.push_back(std::move(cmd));
        return;
    }

    // Run validator if requested and validator exists
    if (update.run_validator && defn->validator) {
        try {
            defn->validator(instance_.get(), args);
        } catch (const std::exception& e) {
            // Validation failure -- reject
            Command cmd;
            cmd.type = CommandType::kUpdateRejected;
            UpdateResponseData resp;
            resp.protocol_instance_id = update.protocol_instance_id;
            resp.update_name = update_name;
            resp.error = e.what();
            cmd.data = std::move(resp);
            commands_.push_back(std::move(cmd));
            return;
        }
    }

    // Accepted
    {
        Command cmd;
        cmd.type = CommandType::kUpdateAccepted;
        UpdateResponseData resp;
        resp.protocol_instance_id = update.protocol_instance_id;
        resp.update_name = update_name;
        cmd.data = std::move(resp);
        commands_.push_back(std::move(cmd));
    }

    // Run the handler asynchronously. Wrap it to emit completion/rejection
    // response commands when the handler finishes.
    auto handler_func = defn->handler;
    std::string proto_id = update.protocol_instance_id;
    std::string uname = update_name;

    auto update_wrapper =
        [this, handler_func, proto_id = std::move(proto_id),
         uname = std::move(uname)](
            void* inst, std::vector<std::any> a) -> coro::Task<std::any> {
        try {
            auto result = co_await handler_func(inst, std::move(a));
            // Success -- emit completed response
            Command cmd;
            cmd.type = CommandType::kUpdateCompleted;
            UpdateResponseData resp;
            resp.protocol_instance_id = proto_id;
            resp.update_name = uname;
            resp.result = std::move(result);
            cmd.data = std::move(resp);
            commands_.push_back(std::move(cmd));
            co_return std::any{};
        } catch (const std::exception& e) {
            // Handler failure -- emit rejected response
            Command cmd;
            cmd.type = CommandType::kUpdateRejected;
            UpdateResponseData resp;
            resp.protocol_instance_id = proto_id;
            resp.update_name = uname;
            resp.error = e.what();
            cmd.data = std::move(resp);
            commands_.push_back(std::move(cmd));
            co_return std::any{};
        }
    };

    ++in_progress_handler_count_;
    auto task = run_top_level(update_wrapper, instance_.get(), args,
                              /*is_handler=*/true);
    scheduler_.schedule(task.handle());
    running_tasks_.push_back(std::move(task));
}

void WorkflowInstance::handle_resolve_signal_external_workflow(
    const Job& job) {
    auto seq = std::any_cast<uint32_t>(job.data);
    auto it = pending_external_signals_.find(seq);
    if (it != pending_external_signals_.end()) {
        it->second->try_set_result(std::any{});
        pending_external_signals_.erase(it);
    }
}

void WorkflowInstance::handle_resolve_request_cancel_external_workflow(
    const Job& job) {
    auto seq = std::any_cast<uint32_t>(job.data);
    auto it = pending_external_cancels_.find(seq);
    if (it != pending_external_cancels_.end()) {
        it->second->try_set_result(std::any{});
        pending_external_cancels_.erase(it);
    }
}

void WorkflowInstance::handle_notify_has_patch(const Job& job) {
    auto patch_id = std::any_cast<std::string>(job.data);
    patches_notified_.insert(std::move(patch_id));
}

void WorkflowInstance::handle_resolve_nexus_operation(const Job& job) {
    auto seq = std::any_cast<uint32_t>(job.data);
    auto it = pending_nexus_operations_.find(seq);
    if (it != pending_nexus_operations_.end()) {
        it->second->try_set_result(std::any{});
        pending_nexus_operations_.erase(it);
    }
}

coro::ResumeCallback WorkflowInstance::make_resume_callback() {
    // Capture a pointer to the scheduler. This is safe because the
    // WorkflowInstance (and thus the scheduler) outlives all TCS objects
    // created through this callback.
    auto* sched = &scheduler_;
    return [sched](std::coroutine_handle<> h) {
        // Route the coroutine resumption through the scheduler instead of
        // resuming inline. This ensures deterministic execution order:
        // the coroutine will be resumed during the next scheduler drain()
        // in the order determined by the scheduler (FIFO).
        sched->schedule(h);
    };
}

coro::Task<std::any> WorkflowInstance::run_top_level(
    std::function<coro::Task<std::any>(void*, std::vector<std::any>)> func,
    void* instance, std::vector<std::any> args, bool is_handler) {
    // Mirrors C# WorkflowInstance.RunTopLevelAsync().
    // Catches workflow-level exceptions and converts them to commands
    // instead of letting them crash the instance.
    try {
        try {
            auto result = co_await func(instance, std::move(args));
            // Only emit complete for the main workflow run, not for
            // signal/update handlers
            if (!is_handler) {
                Command cmd;
                cmd.type = CommandType::kCompleteWorkflow;
                cmd.data = std::move(result);
                commands_.push_back(std::move(cmd));
            }
        } catch (const exceptions::ContinueAsNewException& e) {
            // Workflow wants to continue as new
            Command cmd;
            cmd.type = CommandType::kContinueAsNew;
            cmd.data = std::any(e);
            commands_.push_back(std::move(cmd));
        } catch (const std::exception& e) {
            if (cancellation_source_.is_cancellation_requested() &&
                exceptions::TemporalException::is_canceled_exception(
                    std::current_exception())) {
                // Cancellation requested and this is a cancel exception --
                // emit a cancel command instead of failing the workflow.
                Command cmd;
                cmd.type = CommandType::kCancelWorkflow;
                commands_.push_back(std::move(cmd));
            } else {
                // Workflow failure -- emit a fail command.
                Command cmd;
                cmd.type = CommandType::kFailWorkflow;
                cmd.data = std::string(e.what());
                commands_.push_back(std::move(cmd));
            }
        }
    } catch (const std::exception&) {
        // Unexpected failure (e.g., failure converter itself threw).
        // This becomes an activation-level exception.
        current_activation_exception_ = std::current_exception();
    }

    // Only decrement handler count for signal/update handlers, not
    // for the main workflow run (which never incremented it).
    if (is_handler && in_progress_handler_count_ > 0) {
        --in_progress_handler_count_;
    }

    co_return std::any{};
}

}  // namespace temporalio::worker
