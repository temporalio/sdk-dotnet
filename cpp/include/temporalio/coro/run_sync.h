#pragma once

/// @file run_sync.h
/// @brief Synchronous blocking runner for lazy Task<T> coroutines.
///
/// Provides run_task_sync() to drive a lazy coroutine to completion on the
/// current thread, blocking until done. Handles the case where the inner
/// coroutine suspends on a TaskCompletionSource that is completed from
/// another thread (e.g., a Rust FFI callback).

#include <temporalio/coro/task.h>

#include <condition_variable>
#include <coroutine>
#include <exception>
#include <mutex>
#include <optional>
#include <utility>

namespace temporalio::coro {

namespace detail {

/// A blocking coroutine wrapper whose final_suspend signals a condition
/// variable, allowing the calling thread to block until the inner Task
/// completes.
///
/// The problem: Task<T> is a lazy coroutine (initial_suspend = suspend_always)
/// that may internally co_await a TaskCompletionSource. When it does, the
/// coroutine suspends and is later resumed from an external thread (e.g., a
/// Rust FFI callback). A naive "resume once and read result" approach fails
/// because the coroutine isn't done yet when we try to read the result.
///
/// The solution: Use a BlockingRunner coroutine type whose promise stores a
/// mutex + condition variable. The runner co_awaits the inner Task (becoming
/// its caller via symmetric transfer). When the inner Task's final_suspend
/// resumes us, our final_suspend signals the CV. The calling thread blocks
/// on the CV until the runner coroutine is done, then extracts the result.
///
/// This is analogous to C#'s Task.GetAwaiter().GetResult() or Python's
/// asyncio.run() -- it bridges sync and async worlds on a single thread.
template <typename T>
struct BlockingRunnerPromise;

template <typename T>
struct BlockingRunner {
    using promise_type = BlockingRunnerPromise<T>;
    std::coroutine_handle<promise_type> handle_;

    explicit BlockingRunner(std::coroutine_handle<promise_type> h) : handle_(h) {}
    ~BlockingRunner() {
        if (handle_) handle_.destroy();
    }
    BlockingRunner(BlockingRunner&& o) noexcept : handle_(o.handle_) {
        o.handle_ = nullptr;
    }
    BlockingRunner(const BlockingRunner&) = delete;
    BlockingRunner& operator=(const BlockingRunner&) = delete;
};

template <typename T>
struct BlockingRunnerPromise {
    std::mutex mtx;
    std::condition_variable cv;
    bool done{false};
    std::optional<T> result;
    std::exception_ptr exception;

    BlockingRunner<T> get_return_object() {
        return BlockingRunner<T>{
            std::coroutine_handle<BlockingRunnerPromise>::from_promise(*this)};
    }

    std::suspend_always initial_suspend() noexcept { return {}; }

    struct FinalAwaiter {
        bool await_ready() const noexcept { return false; }
        void await_suspend(
            std::coroutine_handle<BlockingRunnerPromise> h) noexcept {
            auto& p = h.promise();
            std::lock_guard lock(p.mtx);
            p.done = true;
            p.cv.notify_one();
        }
        void await_resume() noexcept {}
    };

    FinalAwaiter final_suspend() noexcept { return {}; }

    void return_value(T value) { result.emplace(std::move(value)); }
    void unhandled_exception() { exception = std::current_exception(); }
};

/// Specialization for void.
template <>
struct BlockingRunnerPromise<void> {
    std::mutex mtx;
    std::condition_variable cv;
    bool done{false};
    std::exception_ptr exception;

    BlockingRunner<void> get_return_object() {
        return BlockingRunner<void>{
            std::coroutine_handle<BlockingRunnerPromise>::from_promise(*this)};
    }

    std::suspend_always initial_suspend() noexcept { return {}; }

    struct FinalAwaiter {
        bool await_ready() const noexcept { return false; }
        void await_suspend(
            std::coroutine_handle<BlockingRunnerPromise> h) noexcept {
            auto& p = h.promise();
            std::lock_guard lock(p.mtx);
            p.done = true;
            p.cv.notify_one();
        }
        void await_resume() noexcept {}
    };

    FinalAwaiter final_suspend() noexcept { return {}; }

    void return_void() noexcept {}
    void unhandled_exception() { exception = std::current_exception(); }
};

}  // namespace detail

/// Run a lazy Task<T> to completion synchronously, blocking the current
/// thread until the coroutine finishes (even if it suspends internally
/// on a TaskCompletionSource that is completed from another thread).
template <typename T>
T run_task_sync(Task<T> task) {
    auto runner = [](Task<T> t) -> detail::BlockingRunner<T> {
        co_return co_await std::move(t);
    }(std::move(task));

    auto& promise = runner.handle_.promise();

    // Start the runner (and transitively the inner task via symmetric transfer).
    runner.handle_.resume();

    // Block until the runner's final_suspend signals completion.
    {
        std::unique_lock lock(promise.mtx);
        promise.cv.wait(lock, [&] { return promise.done; });
    }

    // Propagate exception or return result.
    if (promise.exception) {
        std::rethrow_exception(promise.exception);
    }
    return std::move(*promise.result);
}

/// Specialization for void tasks.
inline void run_task_sync(Task<void> task) {
    auto runner = [](Task<void> t) -> detail::BlockingRunner<void> {
        co_await std::move(t);
    }(std::move(task));

    auto& promise = runner.handle_.promise();

    runner.handle_.resume();

    {
        std::unique_lock lock(promise.mtx);
        promise.cv.wait(lock, [&] { return promise.done; });
    }

    if (promise.exception) {
        std::rethrow_exception(promise.exception);
    }
}

}  // namespace temporalio::coro
