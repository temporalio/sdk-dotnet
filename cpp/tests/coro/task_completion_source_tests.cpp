#include <gtest/gtest.h>

#include <coroutine>
#include <memory>
#include <stdexcept>
#include <string>
#include <thread>

#include "temporalio/coro/task.h"
#include "temporalio/coro/task_completion_source.h"

using namespace temporalio::coro;

// ---------------------------------------------------------------------------
// Helper: synchronously run a Task that may suspend on a TCS.
// Resumes the top-level coroutine handle. If the coroutine suspends waiting
// for a TCS, this returns control so the caller can call set_result on the
// TCS, then re-drive the handle.
// ---------------------------------------------------------------------------
namespace {

// Simple driver that resumes a coroutine once and returns whether it finished.
[[maybe_unused]] bool try_resume(std::coroutine_handle<> h) {
    if (h && !h.done()) {
        h.resume();
        return h.done();
    }
    return true;
}

// Drives a Task<T> by wrapping it in a runner coroutine that stores the result.
template <typename T>
struct SyncRunner {
    std::optional<T> result;
    std::exception_ptr exception;
    Task<void> runner_task;

    static SyncRunner create(Task<T> task) {
        SyncRunner sr;
        sr.runner_task = run(std::move(task), sr);
        return sr;
    }

    static Task<void> run(Task<T> t, SyncRunner& sr) {
        try {
            sr.result.emplace(co_await std::move(t));
        } catch (...) {
            sr.exception = std::current_exception();
        }
    }

    void drive() {
        auto h = runner_task.handle();
        while (h && !h.done()) {
            h.resume();
        }
    }

    T get_result() {
        if (exception) {
            std::rethrow_exception(exception);
        }
        return std::move(*result);
    }
};

}  // namespace

// ===========================================================================
// Basic TaskCompletionSource<T> tests
// ===========================================================================

TEST(TaskCompletionSourceTest, SetResultBeforeAwait) {
    TaskCompletionSource<int> tcs;

    // Set result before anyone awaits
    tcs.set_result(42);
    EXPECT_TRUE(tcs.is_completed());

    // Now get the task and await it -- should return immediately
    auto runner = SyncRunner<int>::create(tcs.task());
    runner.drive();
    EXPECT_EQ(runner.get_result(), 42);
}

TEST(TaskCompletionSourceTest, SetResultAfterAwait) {
    TaskCompletionSource<int> tcs;

    auto runner = SyncRunner<int>::create(tcs.task());

    // Drive the runner - it will suspend at the TCS awaiter
    auto h = runner.runner_task.handle();
    h.resume();  // Start runner coroutine (enters TCS task)
    // The runner should be suspended at TCS await point

    // Now complete the TCS from the "outside"
    tcs.set_result(99);

    // The runner's coroutine should have been resumed by set_result
    // (set_result resumes the waiter)
    EXPECT_EQ(runner.get_result(), 99);
}

TEST(TaskCompletionSourceTest, SetExceptionBeforeAwait) {
    TaskCompletionSource<int> tcs;
    tcs.set_exception(std::make_exception_ptr(std::runtime_error("boom")));

    auto runner = SyncRunner<int>::create(tcs.task());
    runner.drive();
    EXPECT_THROW(runner.get_result(), std::runtime_error);
}

TEST(TaskCompletionSourceTest, SetExceptionAfterAwait) {
    TaskCompletionSource<int> tcs;

    auto runner = SyncRunner<int>::create(tcs.task());
    auto h = runner.runner_task.handle();
    h.resume();

    tcs.set_exception(std::make_exception_ptr(std::logic_error("fail")));

    EXPECT_THROW(runner.get_result(), std::logic_error);
}

// ===========================================================================
// Double-set should throw
// ===========================================================================

TEST(TaskCompletionSourceTest, DoubleSetResultThrows) {
    TaskCompletionSource<int> tcs;
    tcs.set_result(1);
    EXPECT_THROW(tcs.set_result(2), std::logic_error);
}

TEST(TaskCompletionSourceTest, SetResultThenSetExceptionThrows) {
    TaskCompletionSource<int> tcs;
    tcs.set_result(1);
    EXPECT_THROW(
        tcs.set_exception(std::make_exception_ptr(std::runtime_error("x"))),
        std::logic_error);
}

TEST(TaskCompletionSourceTest, SetExceptionThenSetResultThrows) {
    TaskCompletionSource<int> tcs;
    tcs.set_exception(std::make_exception_ptr(std::runtime_error("x")));
    EXPECT_THROW(tcs.set_result(1), std::logic_error);
}

// ===========================================================================
// try_set_result / try_set_exception
// ===========================================================================

TEST(TaskCompletionSourceTest, TrySetResultFirstTime) {
    TaskCompletionSource<int> tcs;
    EXPECT_TRUE(tcs.try_set_result(42));
    EXPECT_TRUE(tcs.is_completed());
}

TEST(TaskCompletionSourceTest, TrySetResultSecondTimeFails) {
    TaskCompletionSource<int> tcs;
    EXPECT_TRUE(tcs.try_set_result(42));
    EXPECT_FALSE(tcs.try_set_result(99));
}

TEST(TaskCompletionSourceTest, TrySetExceptionFirstTime) {
    TaskCompletionSource<int> tcs;
    EXPECT_TRUE(
        tcs.try_set_exception(std::make_exception_ptr(std::runtime_error("x"))));
    EXPECT_TRUE(tcs.is_completed());
}

TEST(TaskCompletionSourceTest, TrySetExceptionAfterResultFails) {
    TaskCompletionSource<int> tcs;
    tcs.set_result(1);
    EXPECT_FALSE(
        tcs.try_set_exception(std::make_exception_ptr(std::runtime_error("x"))));
}

// ===========================================================================
// TaskCompletionSource<void> tests
// ===========================================================================

TEST(TaskCompletionSourceVoidTest, SetResultBeforeAwait) {
    TaskCompletionSource<void> tcs;
    tcs.set_result();
    EXPECT_TRUE(tcs.is_completed());

    // Wrap in a runner that co_awaits the void task
    struct VoidRunner {
        bool completed = false;
        std::exception_ptr exception;
        Task<void> runner_task;

        static VoidRunner create(Task<void> t) {
            VoidRunner vr;
            vr.runner_task = run(std::move(t), vr);
            return vr;
        }
        static Task<void> run(Task<void> t, VoidRunner& vr) {
            try {
                co_await std::move(t);
                vr.completed = true;
            } catch (...) {
                vr.exception = std::current_exception();
            }
        }
        void drive() {
            auto h = runner_task.handle();
            while (h && !h.done()) {
                h.resume();
            }
        }
    };

    auto vr = VoidRunner::create(tcs.task());
    vr.drive();
    EXPECT_TRUE(vr.completed);
}

TEST(TaskCompletionSourceVoidTest, SetExceptionBeforeAwait) {
    TaskCompletionSource<void> tcs;
    tcs.set_exception(std::make_exception_ptr(std::runtime_error("void boom")));

    struct VoidRunner {
        std::exception_ptr exception;
        Task<void> runner_task;

        static VoidRunner create(Task<void> t) {
            VoidRunner vr;
            vr.runner_task = run(std::move(t), vr);
            return vr;
        }
        static Task<void> run(Task<void> t, VoidRunner& vr) {
            try {
                co_await std::move(t);
            } catch (...) {
                vr.exception = std::current_exception();
            }
        }
        void drive() {
            auto h = runner_task.handle();
            while (h && !h.done()) {
                h.resume();
            }
        }
        void check() {
            if (exception) std::rethrow_exception(exception);
        }
    };

    auto vr = VoidRunner::create(tcs.task());
    vr.drive();
    EXPECT_THROW(vr.check(), std::runtime_error);
}

TEST(TaskCompletionSourceVoidTest, DoubleSetResultThrows) {
    TaskCompletionSource<void> tcs;
    tcs.set_result();
    EXPECT_THROW(tcs.set_result(), std::logic_error);
}

TEST(TaskCompletionSourceVoidTest, TrySetResult) {
    TaskCompletionSource<void> tcs;
    EXPECT_TRUE(tcs.try_set_result());
    EXPECT_FALSE(tcs.try_set_result());
}

// ===========================================================================
// is_completed
// ===========================================================================

TEST(TaskCompletionSourceTest, IsCompletedInitiallyFalse) {
    TaskCompletionSource<int> tcs;
    EXPECT_FALSE(tcs.is_completed());
}

TEST(TaskCompletionSourceTest, IsCompletedAfterSetResult) {
    TaskCompletionSource<int> tcs;
    tcs.set_result(1);
    EXPECT_TRUE(tcs.is_completed());
}

TEST(TaskCompletionSourceTest, IsCompletedAfterSetException) {
    TaskCompletionSource<int> tcs;
    tcs.set_exception(std::make_exception_ptr(std::runtime_error("x")));
    EXPECT_TRUE(tcs.is_completed());
}

// ===========================================================================
// String type
// ===========================================================================

TEST(TaskCompletionSourceTest, StringResult) {
    TaskCompletionSource<std::string> tcs;
    tcs.set_result("hello world");

    auto runner = SyncRunner<std::string>::create(tcs.task());
    runner.drive();
    EXPECT_EQ(runner.get_result(), "hello world");
}

// ===========================================================================
// Thread safety: set_result from another thread
// ===========================================================================

TEST(TaskCompletionSourceTest, SetResultFromAnotherThread) {
    TaskCompletionSource<int> tcs;

    auto runner = SyncRunner<int>::create(tcs.task());
    auto h = runner.runner_task.handle();
    h.resume();  // Start, will suspend at TCS

    // Complete from another thread
    std::thread t([&]() { tcs.set_result(777); });
    t.join();

    EXPECT_EQ(runner.get_result(), 777);
}

// ===========================================================================
// ResumeCallback tests
// ===========================================================================

TEST(TaskCompletionSourceTest, ResumeCallbackInvokedOnSetResult) {
    std::coroutine_handle<> captured_handle{nullptr};
    ResumeCallback cb = [&](std::coroutine_handle<> h) {
        captured_handle = h;
    };

    TaskCompletionSource<int> tcs(cb);

    auto runner = SyncRunner<int>::create(tcs.task());
    auto h = runner.runner_task.handle();
    h.resume();  // Start, will suspend at TCS

    // Set result -- should invoke callback instead of resume()
    tcs.set_result(42);

    // The callback captured the handle but did NOT resume it
    EXPECT_NE(captured_handle, nullptr);
    EXPECT_FALSE(runner.runner_task.handle().done());

    // Now manually resume via the captured handle
    captured_handle.resume();
    EXPECT_EQ(runner.get_result(), 42);
}

TEST(TaskCompletionSourceTest, ResumeCallbackInvokedOnSetException) {
    std::coroutine_handle<> captured_handle{nullptr};
    ResumeCallback cb = [&](std::coroutine_handle<> h) {
        captured_handle = h;
    };

    TaskCompletionSource<int> tcs(cb);

    auto runner = SyncRunner<int>::create(tcs.task());
    auto h = runner.runner_task.handle();
    h.resume();

    tcs.set_exception(std::make_exception_ptr(std::runtime_error("cb boom")));

    EXPECT_NE(captured_handle, nullptr);
    captured_handle.resume();
    EXPECT_THROW(runner.get_result(), std::runtime_error);
}

TEST(TaskCompletionSourceTest, ResumeCallbackNotCalledWhenSetBeforeAwait) {
    bool callback_called = false;
    ResumeCallback cb = [&](std::coroutine_handle<> h) {
        callback_called = true;
        h.resume();
    };

    TaskCompletionSource<int> tcs(cb);
    tcs.set_result(42);  // Set before anyone awaits -- no waiter to resume

    EXPECT_FALSE(callback_called);

    auto runner = SyncRunner<int>::create(tcs.task());
    runner.drive();
    EXPECT_EQ(runner.get_result(), 42);
    // Callback was never called because there was no waiter at set_result time
    EXPECT_FALSE(callback_called);
}

TEST(TaskCompletionSourceVoidTest, ResumeCallbackInvokedOnSetResult) {
    std::coroutine_handle<> captured_handle{nullptr};
    ResumeCallback cb = [&](std::coroutine_handle<> h) {
        captured_handle = h;
    };

    TaskCompletionSource<void> tcs(cb);

    struct VoidRunner {
        bool completed = false;
        std::exception_ptr exception;
        Task<void> runner_task;

        static VoidRunner create(Task<void> t) {
            VoidRunner vr;
            vr.runner_task = run(std::move(t), vr);
            return vr;
        }
        static Task<void> run(Task<void> t, VoidRunner& vr) {
            try {
                co_await std::move(t);
                vr.completed = true;
            } catch (...) {
                vr.exception = std::current_exception();
            }
        }
    };

    auto vr = VoidRunner::create(tcs.task());
    auto h = vr.runner_task.handle();
    h.resume();  // Start, will suspend at TCS

    tcs.set_result();

    EXPECT_NE(captured_handle, nullptr);
    EXPECT_FALSE(vr.completed);

    captured_handle.resume();
    EXPECT_TRUE(vr.completed);
}

TEST(TaskCompletionSourceTest, ResumeCallbackFromAnotherThread) {
    std::vector<std::coroutine_handle<>> external_queue;
    std::mutex mu;

    ResumeCallback cb = [&](std::coroutine_handle<> h) {
        std::lock_guard lock(mu);
        external_queue.push_back(h);
    };

    TaskCompletionSource<int> tcs(cb);

    auto runner = SyncRunner<int>::create(tcs.task());
    auto h = runner.runner_task.handle();
    h.resume();  // Suspend at TCS

    // Complete from another thread -- callback enqueues instead of resuming
    std::thread t([&]() { tcs.set_result(999); });
    t.join();

    // Verify the handle was enqueued, not directly resumed
    {
        std::lock_guard lock(mu);
        ASSERT_EQ(external_queue.size(), 1u);
    }

    // Now drain the queue on the "correct" thread
    external_queue[0].resume();
    EXPECT_EQ(runner.get_result(), 999);
}
