#pragma once

/// @file Workflow ambient API for accessing workflow context from user code.

#include <any>
#include <chrono>
#include <cstdint>
#include <functional>
#include <optional>
#include <random>
#include <stop_token>
#include <string>
#include <typeindex>
#include <vector>

#include <type_traits>
#include <utility>

#include <temporalio/converters/data_converter.h>
#include <temporalio/coro/cancellation_token.h>
#include <temporalio/coro/task.h>
#include <temporalio/workflows/activity_options.h>
#include <temporalio/workflows/workflow_info.h>

namespace temporalio::workflows {

// Forward declaration of internal context.
class WorkflowContext;

/// Static ambient API accessible from within workflow code.
/// All methods use a thread_local WorkflowContext* for context propagation
/// (since workflow execution is single-threaded via CoroutineScheduler).
///
/// This mirrors the C# Workflow static class.
class Workflow {
public:
    /// Whether code is currently running inside a workflow.
    static bool in_workflow() noexcept;

    /// Get the workflow info. Throws if not in a workflow.
    static const WorkflowInfo& info();

    /// Get the cancellation token for the workflow.
    static std::stop_token cancellation_token();

    /// Whether continue-as-new is suggested by the server.
    static bool continue_as_new_suggested();

    /// Whether all update and signal handlers have finished.
    static bool all_handlers_finished();

    /// Current deterministic UTC time.
    static std::chrono::system_clock::time_point utc_now();

    /// Get a deterministic random number generator.
    static std::mt19937& random();

    /// Get the current history length.
    static int current_history_length();

    /// Get the current history size in bytes.
    static int current_history_size();

    /// Whether the current activation is a replay.
    static bool is_replaying();

    /// Create a timer that resolves after the given duration.
    static coro::Task<void> delay(
        std::chrono::milliseconds duration,
        std::stop_token ct = {});

    /// Wait for a condition to become true.
    /// Optionally with a timeout. Returns false if the timeout expired.
    static coro::Task<bool> wait_condition(
        std::function<bool()> condition,
        std::optional<std::chrono::milliseconds> timeout = std::nullopt,
        std::stop_token ct = {});

    /// Check if a patch is present (for versioning/migration).
    static bool patched(const std::string& patch_id);

    /// Deprecate a patch (signal that old code path is no longer needed).
    static void deprecate_patch(const std::string& patch_id);

    /// Get the current update info (only valid inside an update handler).
    static const WorkflowUpdateInfo* current_update_info();

    /// Execute an activity by name with multiple arguments.
    /// Returns the activity result as std::any.
    static coro::Task<std::any> execute_activity(
        const std::string& activity_type,
        std::vector<std::any> args,
        const ActivityOptions& options);

    /// Execute an activity by name with a single argument.
    static coro::Task<std::any> execute_activity(
        const std::string& activity_type,
        std::any arg,
        const ActivityOptions& options);

    /// Execute an activity by name with no arguments.
    static coro::Task<std::any> execute_activity(
        const std::string& activity_type,
        const ActivityOptions& options);

    /// Decode a std::any value that may contain either a converters::Payload
    /// (from the inbound protobuf pipeline) or a direct typed value
    /// (from tests or internal usage). Uses the DataConverter from the
    /// current WorkflowContext if available.
    /// Defined after WorkflowContext to avoid incomplete-type errors on GCC.
    template <typename T>
    static T decode_value(const std::any& val);

    /// Type-safe execute_activity: converts typed arguments to std::any,
    /// invokes the activity, and casts the result back to R.
    /// Usage: auto result = co_await Workflow::execute_activity<int>(
    ///     "add", options, 1, 2);
    template <typename R, typename... Args>
    static coro::Task<R> execute_activity(
        const std::string& activity_type,
        const ActivityOptions& options,
        Args&&... args) {
        std::vector<std::any> any_args;
        any_args.reserve(sizeof...(Args));
        (any_args.push_back(std::any(std::forward<Args>(args))), ...);
        auto result_any = co_await execute_activity(
            activity_type, std::move(any_args), options);
        if constexpr (std::is_void_v<R>) {
            co_return;
        } else {
            co_return decode_value<R>(result_any);
        }
    }

    // Workflow cannot be instantiated.
    Workflow() = delete;
};

/// Internal workflow context holding all per-execution state.
/// Set as thread_local during workflow activation processing.
class WorkflowContext {
public:
    virtual ~WorkflowContext() = default;

    virtual const WorkflowInfo& info() const = 0;
    virtual std::stop_token cancellation_token() const = 0;
    virtual bool continue_as_new_suggested() const = 0;
    virtual bool all_handlers_finished() const = 0;
    virtual std::chrono::system_clock::time_point utc_now() const = 0;
    virtual std::mt19937& random() = 0;
    virtual int current_history_length() const = 0;
    virtual int current_history_size() const = 0;
    virtual bool is_replaying() const = 0;
    virtual const WorkflowUpdateInfo* current_update_info() const = 0;
    virtual bool patched(const std::string& patch_id) = 0;
    virtual void deprecate_patch(const std::string& patch_id) = 0;

    /// Create a timer that resolves after the given duration.
    /// Returns a Task<void> that completes when the timer fires.
    /// The optional stop_token allows cancellation of the timer.
    virtual coro::Task<void> start_timer(
        std::chrono::milliseconds duration,
        std::stop_token ct) = 0;

    /// Register a condition predicate and return a Task<bool> that completes
    /// when the condition becomes true or the optional timeout expires.
    /// Returns true if the condition was met, false if timeout expired.
    virtual coro::Task<bool> register_condition(
        std::function<bool()> condition,
        std::optional<std::chrono::milliseconds> timeout,
        std::stop_token ct) = 0;

    /// Schedule an activity and return a Task that completes when the activity
    /// finishes. The returned std::any holds the activity result on success.
    /// Throws ActivityFailureException on failure or CanceledFailureException
    /// on cancellation.
    virtual coro::Task<std::any> schedule_activity(
        const std::string& activity_type,
        std::vector<std::any> args,
        const ActivityOptions& options) = 0;

    /// Get the DataConverter for this workflow. Used to decode inbound
    /// payloads (workflow args, signal args, activity results, etc.).
    /// Returns nullptr if no DataConverter is set (tests may not set one).
    virtual const converters::DataConverter* data_converter() const {
        return nullptr;
    }

    /// Get the current workflow context. Returns nullptr if not in workflow.
    static WorkflowContext* current() noexcept { return current_; }

    /// Set the current context (called by WorkflowInstance).
    static void set_current(WorkflowContext* ctx) noexcept { current_ = ctx; }

private:
    static thread_local WorkflowContext* current_;
};

/// RAII scope for setting and restoring the workflow context.
class WorkflowContextScope {
public:
    explicit WorkflowContextScope(WorkflowContext* ctx) noexcept
        : previous_(WorkflowContext::current()) {
        WorkflowContext::set_current(ctx);
    }

    ~WorkflowContextScope() noexcept {
        WorkflowContext::set_current(previous_);
    }

    WorkflowContextScope(const WorkflowContextScope&) = delete;
    WorkflowContextScope& operator=(const WorkflowContextScope&) = delete;

private:
    WorkflowContext* previous_;
};

// Out-of-line definition now that WorkflowContext is complete.
template <typename T>
T Workflow::decode_value(const std::any& val) {
    auto* ctx = WorkflowContext::current();
    const converters::IPayloadConverter* converter = nullptr;
    if (ctx && ctx->data_converter() &&
        ctx->data_converter()->payload_converter) {
        converter = ctx->data_converter()->payload_converter.get();
    }
    return converters::decode_payload_value<T>(val, converter);
}

}  // namespace temporalio::workflows

