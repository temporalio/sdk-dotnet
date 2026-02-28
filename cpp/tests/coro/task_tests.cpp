#include <gtest/gtest.h>

#include <coroutine>
#include <memory>
#include <stdexcept>
#include <string>
#include <vector>

#include "temporalio/coro/task.h"

using namespace temporalio::coro;

// ---------------------------------------------------------------------------
// Helper: synchronously run a lazy Task<T> to completion.
// This drives the coroutine by repeatedly resuming from the top-level handle
// until the task's underlying coroutine reaches final_suspend.
// Works only for tasks that do not suspend at intermediate points (i.e., no
// external awaits / TaskCompletionSource). Suitable for unit-testing basic
// coroutine mechanics.
// ---------------------------------------------------------------------------
namespace {

template <typename T>
T sync_run(Task<T> task) {
    // The task is lazy: its coroutine is suspended at initial_suspend.
    // We need to wrap it in a "runner" coroutine that co_awaits it,
    // then drive the runner to completion.
    struct RunnerState {
        std::optional<T> result;
        std::exception_ptr exception;
        bool done = false;
    };

    auto state = std::make_shared<RunnerState>();

    // A coroutine that co_awaits the task and stores the result.
    auto runner = [](Task<T> t, std::shared_ptr<RunnerState> s) -> Task<void> {
        try {
            s->result.emplace(co_await std::move(t));
        } catch (...) {
            s->exception = std::current_exception();
        }
        s->done = true;
    }(std::move(task), state);

    // Drive the runner coroutine: resume it until done.
    auto handle = runner.handle();
    while (handle && !handle.done()) {
        handle.resume();
    }

    if (state->exception) {
        std::rethrow_exception(state->exception);
    }
    if (!state->result.has_value()) {
        throw std::logic_error("sync_run: task did not produce a result");
    }
    return std::move(*state->result);
}

// Specialization for void tasks.
void sync_run(Task<void> task) {
    struct RunnerState {
        std::exception_ptr exception;
        bool done = false;
    };

    auto state = std::make_shared<RunnerState>();

    auto runner = [](Task<void> t, std::shared_ptr<RunnerState> s) -> Task<void> {
        try {
            co_await std::move(t);
        } catch (...) {
            s->exception = std::current_exception();
        }
        s->done = true;
    }(std::move(task), state);

    auto handle = runner.handle();
    while (handle && !handle.done()) {
        handle.resume();
    }

    if (state->exception) {
        std::rethrow_exception(state->exception);
    }
}

}  // namespace

// ===========================================================================
// Task<T> basic tests
// ===========================================================================

TEST(TaskTest, ReturnsIntValue) {
    auto task = []() -> Task<int> { co_return 42; }();
    EXPECT_EQ(sync_run(std::move(task)), 42);
}

TEST(TaskTest, ReturnsStringValue) {
    auto task = []() -> Task<std::string> { co_return std::string("hello"); }();
    EXPECT_EQ(sync_run(std::move(task)), "hello");
}

TEST(TaskTest, ReturnsVoid) {
    bool executed = false;
    auto task = [&]() -> Task<void> {
        executed = true;
        co_return;
    }();
    sync_run(std::move(task));
    EXPECT_TRUE(executed);
}

// ===========================================================================
// Task<T> exception propagation
// ===========================================================================

TEST(TaskTest, PropagatesException) {
    auto task = []() -> Task<int> {
        throw std::runtime_error("test error");
        co_return 0;  // unreachable, but needed to make this a coroutine
    }();
    EXPECT_THROW(sync_run(std::move(task)), std::runtime_error);
}

TEST(TaskTest, PropagatesExceptionFromVoidTask) {
    auto task = []() -> Task<void> {
        throw std::logic_error("void error");
        co_return;
    }();
    EXPECT_THROW(sync_run(std::move(task)), std::logic_error);
}

TEST(TaskTest, ExceptionMessagePreserved) {
    auto task = []() -> Task<int> {
        throw std::runtime_error("specific message");
        co_return 0;  // unreachable
    }();
    try {
        sync_run(std::move(task));
        FAIL() << "Expected exception was not thrown";
    } catch (const std::runtime_error& e) {
        EXPECT_STREQ(e.what(), "specific message");
    }
}

// ===========================================================================
// Task<T> lazy execution
// ===========================================================================

TEST(TaskTest, IsLazy_DoesNotRunUntilAwaited) {
    bool started = false;
    auto task = [&]() -> Task<int> {
        started = true;
        co_return 1;
    }();

    // Task was created but not awaited yet
    EXPECT_FALSE(started);

    // Now drive it
    sync_run(std::move(task));
    EXPECT_TRUE(started);
}

TEST(TaskTest, IsLazy_VoidDoesNotRunUntilAwaited) {
    bool started = false;
    auto task = [&]() -> Task<void> {
        started = true;
        co_return;
    }();

    EXPECT_FALSE(started);
    sync_run(std::move(task));
    EXPECT_TRUE(started);
}

// ===========================================================================
// Task<T> move semantics
// ===========================================================================

TEST(TaskTest, MoveConstruction) {
    auto task1 = []() -> Task<int> { co_return 99; }();
    Task<int> task2 = std::move(task1);

    // task1 should be empty after move
    EXPECT_FALSE(static_cast<bool>(task1));
    EXPECT_TRUE(static_cast<bool>(task2));

    EXPECT_EQ(sync_run(std::move(task2)), 99);
}

TEST(TaskTest, MoveAssignment) {
    auto task1 = []() -> Task<int> { co_return 10; }();
    auto task2 = []() -> Task<int> { co_return 20; }();

    task2 = std::move(task1);
    EXPECT_FALSE(static_cast<bool>(task1));
    EXPECT_TRUE(static_cast<bool>(task2));

    EXPECT_EQ(sync_run(std::move(task2)), 10);
}

TEST(TaskTest, DefaultConstructedIsEmpty) {
    Task<int> task;
    EXPECT_FALSE(static_cast<bool>(task));
}

TEST(TaskTest, DefaultConstructedVoidIsEmpty) {
    Task<void> task;
    EXPECT_FALSE(static_cast<bool>(task));
}

// ===========================================================================
// Task chaining (co_await another task)
// ===========================================================================

TEST(TaskTest, ChainedTasks) {
    auto inner = []() -> Task<int> { co_return 5; };
    auto outer = [&]() -> Task<int> {
        auto val = co_await inner();
        co_return val * 2;
    }();

    EXPECT_EQ(sync_run(std::move(outer)), 10);
}

TEST(TaskTest, DeepChaining) {
    auto level3 = []() -> Task<int> { co_return 1; };
    auto level2 = [&]() -> Task<int> {
        co_return co_await level3() + 2;
    };
    auto level1 = [&]() -> Task<int> {
        co_return co_await level2() + 4;
    }();

    EXPECT_EQ(sync_run(std::move(level1)), 7);
}

TEST(TaskTest, ExceptionPropagatesFromInnerTask) {
    auto inner = []() -> Task<int> {
        throw std::runtime_error("inner failure");
        co_return 0;  // unreachable
    };
    auto outer = [&]() -> Task<int> {
        auto val = co_await inner();
        co_return val + 1;
    }();

    EXPECT_THROW(sync_run(std::move(outer)), std::runtime_error);
}

// ===========================================================================
// when_all
// ===========================================================================

TEST(TaskTest, WhenAll_IntTasks) {
    auto make_task = [](int v) -> Task<int> { co_return v; };

    auto combined = [&]() -> Task<std::vector<int>> {
        std::vector<Task<int>> tasks;
        tasks.push_back(make_task(1));
        tasks.push_back(make_task(2));
        tasks.push_back(make_task(3));
        co_return co_await when_all(std::move(tasks));
    }();

    auto result = sync_run(std::move(combined));
    ASSERT_EQ(result.size(), 3u);
    EXPECT_EQ(result[0], 1);
    EXPECT_EQ(result[1], 2);
    EXPECT_EQ(result[2], 3);
}

TEST(TaskTest, WhenAll_EmptyVector) {
    auto combined = []() -> Task<std::vector<int>> {
        std::vector<Task<int>> tasks;
        co_return co_await when_all(std::move(tasks));
    }();

    auto result = sync_run(std::move(combined));
    EXPECT_TRUE(result.empty());
}

TEST(TaskTest, WhenAll_VoidTasks) {
    int counter = 0;
    auto make_task = [&]() -> Task<void> {
        ++counter;
        co_return;
    };

    auto combined = [&]() -> Task<void> {
        std::vector<Task<void>> tasks;
        tasks.push_back(make_task());
        tasks.push_back(make_task());
        tasks.push_back(make_task());
        co_await when_all(std::move(tasks));
    }();

    sync_run(std::move(combined));
    EXPECT_EQ(counter, 3);
}

TEST(TaskTest, WhenAll_PropagatesFirstException) {
    auto good_task = []() -> Task<int> { co_return 1; };
    auto bad_task = []() -> Task<int> {
        throw std::runtime_error("when_all failure");
        co_return 0;  // unreachable
    };

    auto combined = [&]() -> Task<std::vector<int>> {
        std::vector<Task<int>> tasks;
        tasks.push_back(good_task());
        tasks.push_back(bad_task());
        tasks.push_back(good_task());
        co_return co_await when_all(std::move(tasks));
    }();

    EXPECT_THROW(sync_run(std::move(combined)), std::runtime_error);
}

// ===========================================================================
// when_any
// ===========================================================================

TEST(TaskTest, WhenAny_ReturnsFirstResult) {
    auto make_task = [](int v) -> Task<int> { co_return v; };

    auto combined = [&]() -> Task<WhenAnyResult<int>> {
        std::vector<Task<int>> tasks;
        tasks.push_back(make_task(10));
        tasks.push_back(make_task(20));
        tasks.push_back(make_task(30));
        co_return co_await when_any(std::move(tasks));
    }();

    auto result = sync_run(std::move(combined));
    // Since these are lazy, the first one to be awaited (index 0) completes
    EXPECT_EQ(result.index, 0u);
    EXPECT_EQ(result.value, 10);
}

TEST(TaskTest, WhenAny_EmptyThrows) {
    auto combined = []() -> Task<WhenAnyResult<int>> {
        std::vector<Task<int>> tasks;
        co_return co_await when_any(std::move(tasks));
    }();

    EXPECT_THROW(sync_run(std::move(combined)), std::logic_error);
}

// ===========================================================================
// Task<T> with different value types
// ===========================================================================

TEST(TaskTest, ReturnsDouble) {
    auto task = []() -> Task<double> { co_return 3.14; }();
    EXPECT_DOUBLE_EQ(sync_run(std::move(task)), 3.14);
}

TEST(TaskTest, ReturnsBool) {
    auto task = []() -> Task<bool> { co_return true; }();
    EXPECT_TRUE(sync_run(std::move(task)));
}

TEST(TaskTest, ReturnsVector) {
    auto task = []() -> Task<std::vector<int>> {
        co_return std::vector<int>{1, 2, 3};
    }();
    auto result = sync_run(std::move(task));
    EXPECT_EQ(result, (std::vector<int>{1, 2, 3}));
}

TEST(TaskTest, ReturnsMoveOnlyType) {
    auto task = []() -> Task<std::unique_ptr<int>> {
        co_return std::make_unique<int>(42);
    }();
    auto result = sync_run(std::move(task));
    ASSERT_NE(result, nullptr);
    EXPECT_EQ(*result, 42);
}
