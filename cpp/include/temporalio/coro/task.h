#pragma once

/// @file Lazy coroutine Task<T> type.
///
/// FinalAwaiter uses direct resume (void await_suspend) rather than
/// symmetric transfer (returning coroutine_handle<>). This trades
/// tail-call optimization for portability: GCC 13 has a coroutine
/// codegen bug where symmetric transfer produces broken code for
/// certain coroutine frame layouts. Direct resume adds ~2 stack
/// frames per co_await level, which is negligible for this SDK's
/// shallow coroutine chains.

#include <coroutine>
#include <exception>
#include <optional>
#include <stdexcept>
#include <type_traits>
#include <utility>
#include <vector>

namespace temporalio::coro {

// Forward declarations
template <typename T>
class Task;

namespace detail {

// Base promise type shared between Task<T> and Task<void>.
// Stores exception, caller handle, and manages lazy suspension.
class PromiseBase {
public:
    std::suspend_always initial_suspend() noexcept { return {}; }

    struct FinalAwaiter {
        bool await_ready() const noexcept { return false; }

        // Direct resume: resumes the caller coroutine from within
        // await_suspend. This avoids symmetric transfer (returning
        // coroutine_handle<>) which triggers a GCC 13 codegen bug.
        template <typename Promise>
        void await_suspend(
            std::coroutine_handle<Promise> h) noexcept {
            auto& base = static_cast<PromiseBase&>(h.promise());
            if (base.caller_) {
                base.caller_.resume();
            }
        }

        void await_resume() noexcept {}
    };

    FinalAwaiter final_suspend() noexcept { return {}; }

    void unhandled_exception() noexcept {
        exception_ = std::current_exception();
    }

    void set_caller(std::coroutine_handle<> caller) noexcept {
        caller_ = caller;
    }

    std::exception_ptr exception() const noexcept { return exception_; }

private:
    std::coroutine_handle<> caller_;
    std::exception_ptr exception_;
};

}  // namespace detail

/// Lazy coroutine task. Does NOT execute until awaited.
/// Move-only type; co_await resumes the coroutine and returns the result.
template <typename T>
class Task {
public:
    struct promise_type : detail::PromiseBase {
        Task get_return_object() {
            return Task{
                std::coroutine_handle<promise_type>::from_promise(*this)};
        }

        void return_value(T value) noexcept(
            std::is_nothrow_move_constructible_v<T>) {
            result_.emplace(std::move(value));
        }

        std::optional<T> result_;
    };

    Task() noexcept : handle_(nullptr) {}

    explicit Task(std::coroutine_handle<promise_type> h) noexcept
        : handle_(h) {}

    ~Task() {
        if (handle_) {
            handle_.destroy();
        }
    }

    // Move-only
    Task(const Task&) = delete;
    Task& operator=(const Task&) = delete;

    Task(Task&& other) noexcept : handle_(other.handle_) {
        other.handle_ = nullptr;
    }

    Task& operator=(Task&& other) noexcept {
        if (this != &other) {
            if (handle_) {
                handle_.destroy();
            }
            handle_ = other.handle_;
            other.handle_ = nullptr;
        }
        return *this;
    }

    // Awaitable interface
    bool await_ready() const noexcept { return false; }

    std::coroutine_handle<> await_suspend(
        std::coroutine_handle<> caller) noexcept {
        handle_.promise().set_caller(caller);
        return handle_;
    }

    T await_resume() {
        auto& promise = handle_.promise();
        if (promise.exception()) {
            std::rethrow_exception(promise.exception());
        }
        return std::move(*promise.result_);
    }

    /// Returns true if the task has a valid coroutine handle.
    explicit operator bool() const noexcept { return handle_ != nullptr; }

    /// Returns the underlying coroutine handle (for scheduler use).
    std::coroutine_handle<> handle() const noexcept { return handle_; }

private:
    std::coroutine_handle<promise_type> handle_;
};

/// Specialization for void return type.
template <>
class Task<void> {
public:
    struct promise_type : detail::PromiseBase {
        Task get_return_object() {
            return Task{
                std::coroutine_handle<promise_type>::from_promise(*this)};
        }

        void return_void() noexcept {}
    };

    Task() noexcept : handle_(nullptr) {}

    explicit Task(std::coroutine_handle<promise_type> h) noexcept
        : handle_(h) {}

    ~Task() {
        if (handle_) {
            handle_.destroy();
        }
    }

    // Move-only
    Task(const Task&) = delete;
    Task& operator=(const Task&) = delete;

    Task(Task&& other) noexcept : handle_(other.handle_) {
        other.handle_ = nullptr;
    }

    Task& operator=(Task&& other) noexcept {
        if (this != &other) {
            if (handle_) {
                handle_.destroy();
            }
            handle_ = other.handle_;
            other.handle_ = nullptr;
        }
        return *this;
    }

    // Awaitable interface
    bool await_ready() const noexcept { return false; }

    std::coroutine_handle<> await_suspend(
        std::coroutine_handle<> caller) noexcept {
        handle_.promise().set_caller(caller);
        return handle_;
    }

    void await_resume() {
        auto& promise = handle_.promise();
        if (promise.exception()) {
            std::rethrow_exception(promise.exception());
        }
    }

    explicit operator bool() const noexcept { return handle_ != nullptr; }

    std::coroutine_handle<> handle() const noexcept { return handle_; }

private:
    std::coroutine_handle<promise_type> handle_;
};

/// Await all tasks, returning a vector of results.
/// If any task throws, the exception from the first throwing task is propagated.
template <typename T>
Task<std::vector<T>> when_all(std::vector<Task<T>> tasks) {
    std::vector<T> results;
    results.reserve(tasks.size());
    for (auto& t : tasks) {
        results.push_back(co_await std::move(t));
    }
    co_return results;
}

/// Await all void tasks. If any throws, the first exception is propagated.
///
/// Note: this is an `inline` coroutine (non-template) defined in a header.
/// The `inline` keyword is required to avoid ODR violations when this header
/// is included from multiple translation units.
inline Task<void> when_all(std::vector<Task<void>> tasks) {
    for (auto& t : tasks) {
        co_await std::move(t);
    }
}

/// Result of when_any: which task completed and its value.
template <typename T>
struct WhenAnyResult {
    size_t index;
    T value;
};

/// Await the first task to complete and return its index and result.
///
/// IMPORTANT: Because Task<T> is a lazy coroutine (initial_suspend =
/// suspend_always), this function evaluates tasks sequentially, not
/// concurrently. Each task starts only when the previous one completes.
/// For purely lazy tasks, this always returns index 0.
///
/// This function is useful when tasks are backed by TaskCompletionSource,
/// where the coroutine body immediately suspends waiting for an external
/// signal. In that pattern, the first task to have its TCS completed
/// resumes and "wins" the race.
///
/// For true concurrent racing, use multiple poll loops with a shared
/// shutdown signal (as done in TemporalWorker::execute_async).
template <typename T>
Task<WhenAnyResult<T>> when_any(std::vector<Task<T>> tasks) {
    for (size_t i = 0; i < tasks.size(); ++i) {
        auto result = co_await std::move(tasks[i]);
        co_return WhenAnyResult<T>{i, std::move(result)};
    }
    // Should not reach here if tasks is non-empty
    throw std::logic_error("when_any called with empty task vector");
}

}  // namespace temporalio::coro

