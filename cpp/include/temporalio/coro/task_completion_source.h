#pragma once

/// @file Thread-safe bridge from callbacks to coroutines.

#include <cassert>
#include <coroutine>
#include <exception>
#include <functional>
#include <memory>
#include <mutex>
#include <optional>
#include <type_traits>
#include <utility>

#include <temporalio/coro/task.h>

namespace temporalio::coro {

/// Callback type for routing coroutine resumption to the correct thread.
/// When set on a TaskCompletionSource, the resume is routed through this
/// callback instead of calling coroutine_handle::resume() directly.
/// This is critical for workflow determinism: FFI callbacks fire on Rust
/// threads, but workflow coroutines must resume on the scheduler thread.
using ResumeCallback = std::function<void(std::coroutine_handle<>)>;

namespace detail {

/// Shared state between TaskCompletionSource and its awaitable Task.
/// Thread-safe: set_result/set_exception may be called from any thread
/// (e.g., Rust FFI callback thread), while await happens on the coroutine's
/// thread.
template <typename T>
class CompletionState {
public:
    CompletionState() = default;

    /// Construct with a resume callback for thread-safe resumption.
    explicit CompletionState(ResumeCallback resume_cb)
        : resume_callback_(std::move(resume_cb)) {}

    // Non-copyable, non-movable (shared via shared_ptr)
    CompletionState(const CompletionState&) = delete;
    CompletionState& operator=(const CompletionState&) = delete;

    /// Try to set the result. Returns true if this was the first completion.
    bool try_set_result(T value) {
        std::coroutine_handle<> to_resume{nullptr};
        ResumeCallback cb;
        {
            std::lock_guard lock(mutex_);
            if (completed_) return false;
            result_.emplace(std::move(value));
            completed_ = true;
            to_resume = std::exchange(waiter_, nullptr);
            cb = resume_callback_;
        }
        if (to_resume) {
            if (cb) {
                cb(to_resume);
            } else {
                to_resume.resume();
            }
        }
        return true;
    }

    /// Try to set an exception. Returns true if this was the first completion.
    bool try_set_exception(std::exception_ptr ex) {
        std::coroutine_handle<> to_resume{nullptr};
        ResumeCallback cb;
        {
            std::lock_guard lock(mutex_);
            if (completed_) return false;
            exception_ = ex;
            completed_ = true;
            to_resume = std::exchange(waiter_, nullptr);
            cb = resume_callback_;
        }
        if (to_resume) {
            if (cb) {
                cb(to_resume);
            } else {
                to_resume.resume();
            }
        }
        return true;
    }

    /// Set the result. Throws if already completed.
    void set_result(T value) {
        if (!try_set_result(std::move(value))) {
            throw std::logic_error(
                "TaskCompletionSource result already set");
        }
    }

    /// Set an exception. Throws if already completed.
    void set_exception(std::exception_ptr ex) {
        if (!try_set_exception(ex)) {
            throw std::logic_error(
                "TaskCompletionSource result already set");
        }
    }

    bool is_completed() const {
        std::lock_guard lock(mutex_);
        return completed_;
    }

    // Awaiter support
    bool await_ready() const {
        std::lock_guard lock(mutex_);
        return completed_;
    }

    bool await_suspend(std::coroutine_handle<> caller) {
        std::lock_guard lock(mutex_);
        if (completed_) {
            // Already completed, don't suspend
            return false;
        }
        // Only one waiter is supported. If waiter_ is already set, it means
        // task() was called multiple times and both Tasks are being awaited.
        // The first awaiter would be silently dropped (never resumed).
        assert(!waiter_ &&
               "CompletionState only supports a single waiter. "
               "Do not call TaskCompletionSource::task() more than once.");
        waiter_ = caller;
        return true;
    }

    T await_resume() {
        // No lock needed: by the time we resume, completed_ is true and the
        // result/exception fields are immutable.
        if (exception_) {
            std::rethrow_exception(exception_);
        }
        return std::move(*result_);
    }

private:
    mutable std::mutex mutex_;
    std::coroutine_handle<> waiter_{nullptr};
    std::optional<T> result_;
    std::exception_ptr exception_;
    bool completed_{false};
    ResumeCallback resume_callback_;
};

/// Specialization for void.
template <>
class CompletionState<void> {
public:
    CompletionState() = default;

    /// Construct with a resume callback for thread-safe resumption.
    explicit CompletionState(ResumeCallback resume_cb)
        : resume_callback_(std::move(resume_cb)) {}

    CompletionState(const CompletionState&) = delete;
    CompletionState& operator=(const CompletionState&) = delete;

    bool try_set_result() {
        std::coroutine_handle<> to_resume{nullptr};
        ResumeCallback cb;
        {
            std::lock_guard lock(mutex_);
            if (completed_) return false;
            completed_ = true;
            to_resume = std::exchange(waiter_, nullptr);
            cb = resume_callback_;
        }
        if (to_resume) {
            if (cb) {
                cb(to_resume);
            } else {
                to_resume.resume();
            }
        }
        return true;
    }

    bool try_set_exception(std::exception_ptr ex) {
        std::coroutine_handle<> to_resume{nullptr};
        ResumeCallback cb;
        {
            std::lock_guard lock(mutex_);
            if (completed_) return false;
            exception_ = ex;
            completed_ = true;
            to_resume = std::exchange(waiter_, nullptr);
            cb = resume_callback_;
        }
        if (to_resume) {
            if (cb) {
                cb(to_resume);
            } else {
                to_resume.resume();
            }
        }
        return true;
    }

    void set_result() {
        if (!try_set_result()) {
            throw std::logic_error(
                "TaskCompletionSource result already set");
        }
    }

    void set_exception(std::exception_ptr ex) {
        if (!try_set_exception(ex)) {
            throw std::logic_error(
                "TaskCompletionSource result already set");
        }
    }

    bool is_completed() const {
        std::lock_guard lock(mutex_);
        return completed_;
    }

    bool await_ready() const {
        std::lock_guard lock(mutex_);
        return completed_;
    }

    bool await_suspend(std::coroutine_handle<> caller) {
        std::lock_guard lock(mutex_);
        if (completed_) {
            return false;
        }
        // Only one waiter is supported. See CompletionState<T> for details.
        assert(!waiter_ &&
               "CompletionState only supports a single waiter. "
               "Do not call TaskCompletionSource::task() more than once.");
        waiter_ = caller;
        return true;
    }

    void await_resume() {
        if (exception_) {
            std::rethrow_exception(exception_);
        }
    }

private:
    mutable std::mutex mutex_;
    std::coroutine_handle<> waiter_{nullptr};
    std::exception_ptr exception_;
    bool completed_{false};
    ResumeCallback resume_callback_;
};

}  // namespace detail

/// Bridges FFI callbacks to coroutines (replaces C# TaskCompletionSource<T>).
///
/// Usage:
///   TaskCompletionSource<int> tcs;
///   // On FFI callback thread:
///   tcs.set_result(42);
///   // On coroutine thread:
///   int value = co_await tcs.task();
///
/// Thread-safe: set_result / set_exception can be called from any thread.
/// By default, the awaiting coroutine is resumed on the thread that calls
/// set_result/set_exception. When a ResumeCallback is provided, the resume
/// is routed through the callback instead, allowing the coroutine to be
/// scheduled back onto the correct thread (e.g., the workflow scheduler).
template <typename T>
class TaskCompletionSource {
public:
    TaskCompletionSource()
        : state_(std::make_shared<detail::CompletionState<T>>()) {}

    /// Construct with a resume callback for thread-safe resumption.
    /// The callback will be invoked instead of calling resume() directly
    /// when the result is set from any thread.
    explicit TaskCompletionSource(ResumeCallback resume_cb)
        : state_(std::make_shared<detail::CompletionState<T>>(
              std::move(resume_cb))) {}

    /// Returns an awaitable Task that will complete when set_result or
    /// set_exception is called.
    ///
    /// WARNING: Must only be called ONCE. Unlike C# TaskCompletionSource.Task
    /// (which returns the same Task), each call creates a new coroutine.
    /// The underlying CompletionState only stores one waiter handle, so a
    /// second call would overwrite the first waiter, leaving it hung forever.
    /// A debug assert fires if this contract is violated.
    Task<T> task() {
        auto state = state_;
        co_return co_await Awaiter{state};
    }

    /// Set the result value. Resumes the awaiting coroutine.
    /// Throws std::logic_error if already completed.
    void set_result(T value) { state_->set_result(std::move(value)); }

    /// Set an exception. Resumes the awaiting coroutine which will rethrow.
    /// Throws std::logic_error if already completed.
    void set_exception(std::exception_ptr ex) { state_->set_exception(ex); }

    /// Try to set the result. Returns false if already completed.
    bool try_set_result(T value) {
        return state_->try_set_result(std::move(value));
    }

    /// Try to set an exception. Returns false if already completed.
    bool try_set_exception(std::exception_ptr ex) {
        return state_->try_set_exception(ex);
    }

    /// Check if the source has been completed.
    bool is_completed() const { return state_->is_completed(); }

private:
    struct Awaiter {
        std::shared_ptr<detail::CompletionState<T>> state;

        bool await_ready() const { return state->await_ready(); }

        bool await_suspend(std::coroutine_handle<> caller) {
            return state->await_suspend(caller);
        }

        T await_resume() { return state->await_resume(); }
    };

    std::shared_ptr<detail::CompletionState<T>> state_;
};

/// Specialization for void.
template <>
class TaskCompletionSource<void> {
public:
    TaskCompletionSource()
        : state_(std::make_shared<detail::CompletionState<void>>()) {}

    /// Construct with a resume callback for thread-safe resumption.
    explicit TaskCompletionSource(ResumeCallback resume_cb)
        : state_(std::make_shared<detail::CompletionState<void>>(
              std::move(resume_cb))) {}

    /// Returns an awaitable Task that completes when set_result or
    /// set_exception is called.
    ///
    /// WARNING: Must only be called ONCE. See TaskCompletionSource<T>::task().
    Task<void> task() {
        auto state = state_;
        co_await Awaiter{state};
    }

    void set_result() { state_->set_result(); }

    void set_exception(std::exception_ptr ex) { state_->set_exception(ex); }

    bool try_set_result() { return state_->try_set_result(); }

    bool try_set_exception(std::exception_ptr ex) {
        return state_->try_set_exception(ex);
    }

    bool is_completed() const { return state_->is_completed(); }

private:
    struct Awaiter {
        std::shared_ptr<detail::CompletionState<void>> state;

        bool await_ready() const { return state->await_ready(); }

        bool await_suspend(std::coroutine_handle<> caller) {
            return state->await_suspend(caller);
        }

        void await_resume() { state->await_resume(); }
    };

    std::shared_ptr<detail::CompletionState<void>> state_;
};

}  // namespace temporalio::coro

